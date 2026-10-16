// =====================================================================================
//  WebServer.cs – lokaler Webserver für Steuerseite, Einrichtung und OBS-Overlay
// =====================================================================================
//  • Liefert die HTML-Seiten (aus der exe eingebaut oder mit --web aus einem Ordner)
//    und die Schriften aus.
//  • /events ist ein dauerhaft offener Datenstrom (Server-Sent Events, SSE): Jede
//    Änderung im Tracker wird sofort als "data: {json}" an alle offenen Seiten geschickt.
//  • /api/... nimmt Klicks der Steuerseite entgegen (Haken, Gebiet, Reset …).
//
//  Sicherheitsprüfungen bei JEDER Anfrage (Reihenfolge in Handle):
//    1. Absender muss der eigene Rechner sein (Loopback-Adresse).
//    2. Host-Header muss localhost/127.0.0.1/[::1] mit unserem Port sein → schützt
//       gegen DNS-Rebinding (fremde Webseite, deren Domain plötzlich auf 127.0.0.1 zeigt).
//    3. Ändernde Anfragen (POST) nur von unseren eigenen Seiten → schützt gegen CSRF
//       (fremde Webseite schickt im Hintergrund Klicks an localhost).
//  Dazu Sicherheits-Header auf allen Antworten (AddSecurityHeaders).
// =====================================================================================

using System.Net;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace DSRTracker;

/// <summary>
/// Kleiner lokaler Webserver: Overlay, Steuerseite, Live-Updates per Server-Sent Events.
/// Sicherheit: nur Anfragen vom eigenen Rechner (Loopback), nur Host "localhost"/"127.0.0.1" (gegen DNS-Rebinding),
/// keine CORS-Freigaben, ändernde Anfragen nur per POST von der eigenen Seite (gegen CSRF), keine Pfade aus Anfragen.
/// </summary>
public sealed class WebServer
{
    private readonly HttpListener _listener = new();          // eingebauter HTTP-Server von .NET
    private readonly Tracker _tracker;
    private readonly List<StreamWriter> _clients = new();     // offene /events-Verbindungen (Seiten in OBS/Browser)
    private readonly object _clientsLock = new();
    private readonly string? _webOverrideDir;                 // --web <ordner>: HTML aus diesem Ordner statt aus der exe
    private readonly HashSet<string> _allowedHosts;           // erlaubte Host-Header (mit Port)
    private readonly HashSet<string> _allowedOrigins;         // erlaubte Origin-Header bei POST
    // Nur einfache Dateinamen wie "cinzel-latin-400.woff2" – keine Pfade, kein "..", keine anderen Endungen
    private static readonly Regex AssetName = new(@"^[a-z0-9-]+\.(woff2|css|txt)$", RegexOptions.CultureInvariant);

    public WebServer(Tracker tracker, int port, string? webOverrideDir)
    {
        _tracker = tracker;
        _webOverrideDir = webOverrideDir;
        _allowedHosts = new(StringComparer.OrdinalIgnoreCase) { $"localhost:{port}", $"127.0.0.1:{port}", $"[::1]:{port}" };
        _allowedOrigins = new(StringComparer.OrdinalIgnoreCase) { $"http://localhost:{port}", $"http://127.0.0.1:{port}", $"http://[::1]:{port}" };
        // "localhost" statt "+"/"*": braucht keine Admin-Rechte bzw. URL-ACL

        _listener.Prefixes.Add($"http://localhost:{port}/");
        _tracker.Broadcast += Send; // jede Nachricht des Trackers an alle offenen Seiten weiterreichen
    }

    /// <summary>Startet den Server und zwei Hintergrund-Schleifen: Anfragen annehmen und Keep-Alive.</summary>
    public void Start(CancellationToken ct)
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoop(ct), ct);
        _ = Task.Run(() => HeartbeatLoop(ct), ct);
    }

    /// <summary>Nimmt Anfragen an und bearbeitet jede in einem eigenen Task (damit /events die anderen nicht blockiert).</summary>
    private async Task AcceptLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try { ctx = await _listener.GetContextAsync(); }
            catch { break; }
            _ = Task.Run(() => Handle(ctx));
        }
    }

    /// <summary>
    /// Schickt alle 15 s eine Kommentarzeile ": ping" in alle Datenströme. Hält die Verbindungen offen
    /// und räumt dabei geschlossene Seiten auf (Schreibfehler → aus der Liste entfernen).
    /// </summary>
    private async Task HeartbeatLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(15000, ct).ContinueWith(_ => { });
            Write(": ping\n\n");
        }
    }

    /// <summary>Bearbeitet eine Anfrage: Sicherheitsprüfungen, dann Verteilung nach Methode + Pfad.</summary>
    private void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
        if (path == "") path = "/";

        try
        {
            // 1) nur vom eigenen Rechner
            if (req.RemoteEndPoint == null || !IPAddress.IsLoopback(req.RemoteEndPoint.Address))
            {
                WriteText(res, "Nur lokal erreichbar", "text/plain", 403);
                return;
            }
            // 2) Host-Header prüfen (DNS-Rebinding)
            var host = req.Headers["Host"] ?? "";
            if (!_allowedHosts.Contains(host))
            {
                WriteText(res, "Unbekannter Host", "text/plain", 403);
                return;
            }
            // 3) Ändernde Anfragen nur von der eigenen Seite (CSRF): fremde Origin oder Cross-Site-Anfrage abweisen
            if (req.HttpMethod != "GET" && req.HttpMethod != "HEAD")
            {
                var origin = req.Headers["Origin"];
                var site = req.Headers["Sec-Fetch-Site"];
                bool badOrigin = !string.IsNullOrEmpty(origin) && !_allowedOrigins.Contains(origin);
                bool crossSite = site != null && site != "same-origin" && site != "none";
                if (badOrigin || crossSite)
                {
                    WriteText(res, "Fremde Herkunft", "text/plain", 403);
                    return;
                }
            }

            // Schriften (feste Positivliste über Dateinamen-Muster, keine Unterordner)
            if (req.HttpMethod == "GET" && (path.StartsWith("/fonts/") || path.StartsWith("/web/fonts/")))
            {
                ServeAsset(res, path[(path.LastIndexOf('/') + 1)..]);
                return;
            }

            // Alle Adressen, die es gibt. Alles andere: 404. Ändernde Aktionen nur per POST.
            switch (req.HttpMethod, path)
            {
                case ("GET", "/"):
                case ("GET", "/control"):
                    ServeFile(res, "control.html");
                    break;
                case ("GET", "/setup"):
                    ServeFile(res, "setup.html");
                    break;
                case ("GET", "/favicon.ico"):
                    res.StatusCode = 204; res.OutputStream.Close();
                    break;
                case ("GET", "/guide"):
                    ServeFile(res, "guide.html");
                    break;
                case ("POST", "/api/onboarding-done"):
                    _tracker.SetOnboardingDone(req.QueryString["done"] != "0");
                    WriteText(res, "ok", "text/plain");
                    break;
                case ("POST", "/api/test-toast"):
                    _tracker.SendTestToast();
                    WriteText(res, "ok", "text/plain");
                    break;
                case ("GET", "/overlay"):
                    ServeFile(res, "overlay.html");
                    break;
                case ("GET", "/api/state"):
                    WriteText(res, _tracker.GetStateJson(), "application/json");
                    break;
                case ("GET", "/events"):
                    OpenEventStream(res);
                    return; // Verbindung bleibt offen
                case ("POST", "/api/toggle"):
                {
                    var key = req.QueryString["key"] ?? "";
                    bool ok = _tracker.ToggleManual(key);
                    WriteText(res, ok ? "ok" : "unknown key", "text/plain", ok ? 200 : 404);
                    break;
                }
                case ("POST", "/api/area"):
                {
                    int.TryParse(req.QueryString["idx"], out int idx);
                    _tracker.SetAreaOverride(idx);
                    WriteText(res, "ok", "text/plain");
                    break;
                }
                case ("POST", "/api/reset"):
                    _tracker.ResetManual();
                    WriteText(res, "ok", "text/plain");
                    break;
                case ("POST", "/api/clearlog"):
                    _tracker.ClearFlagLog();
                    WriteText(res, "ok", "text/plain");
                    break;
                default:
                    WriteText(res, "Nicht gefunden", "text/plain", 404);
                    break;
            }
        }
        catch
        {
            try { res.Abort(); } catch { }
        }
    }

    /// <summary>
    /// /events: Server-Sent Events. Die Antwort bleibt offen; neue Nachrichten werden über Write() angehängt.
    /// Zuerst: "retry: 2000" (Browser verbindet sich nach Abbruch nach 2 s neu) und der aktuelle Zustand.
    /// </summary>
    private void OpenEventStream(HttpListenerResponse res)
    {
        res.ContentType = "text/event-stream; charset=utf-8";
        res.Headers["Cache-Control"] = "no-cache";
        AddSecurityHeaders(res);
        res.SendChunked = true;
        var writer = new StreamWriter(res.OutputStream, new UTF8Encoding(false)) { AutoFlush = true };
        try
        {
            writer.Write("retry: 2000\n\n");
            writer.Write($"data: {_tracker.GetStateJson()}\n\n");
        }
        catch { return; }
        lock (_clientsLock) _clients.Add(writer);
    }

    // Eine Nachricht im SSE-Format: "data: …" gefolgt von einer Leerzeile
    private void Send(string json) => Write($"data: {json}\n\n");

    /// <summary>Schreibt an alle offenen Seiten. Wer nicht mehr erreichbar ist, fliegt aus der Liste.</summary>
    private void Write(string payload)
    {
        lock (_clientsLock)
        {
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                try { _clients[i].Write(payload); }
                catch
                {
                    try { _clients[i].Dispose(); } catch { }
                    _clients.RemoveAt(i);
                }
            }
        }
    }

    /// <summary>Liefert eine HTML-Seite aus. Nur feste Namen aus dem switch oben – nie Pfade aus der Anfrage.</summary>
    private void ServeFile(HttpListenerResponse res, string name)
    {
        string? content = null;
        if (_webOverrideDir != null)
        {
            var p = Path.Combine(_webOverrideDir, name);
            if (File.Exists(p)) content = File.ReadAllText(p);
        }
        content ??= ReadEmbedded("web/" + name);
        if (content == null) { WriteText(res, "Datei fehlt: " + name, "text/plain", 404); return; }
        WriteText(res, content, "text/html");
    }

    /// <summary>Liefert eine Schrift-/CSS-Datei aus dem web/fonts-Ordner bzw. aus der exe. Nur einfache Dateinamen.</summary>
    private void ServeAsset(HttpListenerResponse res, string name)
    {
        if (!AssetName.IsMatch(name)) { WriteText(res, "Nicht gefunden", "text/plain", 404); return; }
        byte[]? data = null;
        if (_webOverrideDir != null)
        {
            var p = Path.Combine(_webOverrideDir, "fonts", name);
            if (File.Exists(p)) data = File.ReadAllBytes(p);
        }
        data ??= ReadEmbeddedBytes("web/" + name);
        if (data == null) { WriteText(res, "Nicht gefunden", "text/plain", 404); return; }
        var type = name.EndsWith(".woff2") ? "font/woff2" : name.EndsWith(".css") ? "text/css; charset=utf-8" : "text/plain; charset=utf-8";
        res.StatusCode = 200;
        res.ContentType = type;
        res.Headers["Cache-Control"] = "max-age=86400";
        AddSecurityHeaders(res);
        res.ContentLength64 = data.Length;
        res.OutputStream.Write(data, 0, data.Length);
        res.OutputStream.Close();
    }

    /// <summary>Liest eine in die exe eingebaute Datei als Bytes (Schriften). Namen siehe DSRTracker.csproj.</summary>
    public static byte[]? ReadEmbeddedBytes(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (s == null) return null;
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Seiten dürfen nur Inhalte vom eigenen Server laden und nur von der eigenen Seite eingebettet werden.</summary>
    private static void AddSecurityHeaders(HttpListenerResponse res)
    {
        res.Headers["X-Content-Type-Options"] = "nosniff";      // Browser soll den Dateityp nicht raten
        res.Headers["Referrer-Policy"] = "no-referrer";         // beim Klick auf externe Links keine Adresse mitschicken
        // CSP: Skripte, Styles, Schriften, Verbindungen nur vom eigenen Server; Einbetten nur durch eigene Seiten
        // ('unsafe-inline', weil Skripte und Styles direkt in den HTML-Dateien stehen)
        res.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self' 'unsafe-inline'; style-src 'self' 'unsafe-inline'; " +
            "font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'self'; form-action 'self'; base-uri 'none'";
    }

    /// <summary>Liest eine in die exe eingebaute Textdatei (HTML-Seiten, Gegnerliste).</summary>
    public static string? ReadEmbedded(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (s == null) return null;
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    /// <summary>Schreibt eine komplette Textantwort (HTML, JSON oder Klartext) inkl. Sicherheits-Headern.</summary>
    private static void WriteText(HttpListenerResponse res, string text, string type, int status = 200)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        res.StatusCode = status;
        res.ContentType = type + "; charset=utf-8";
        res.Headers["Cache-Control"] = "no-cache";
        AddSecurityHeaders(res);
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }
}
