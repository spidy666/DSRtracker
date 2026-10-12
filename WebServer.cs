using System.Net;
using System.Reflection;
using System.Text;

namespace DSRTracker;

/// <summary>Kleiner lokaler Webserver: Overlay, Steuerseite, Live-Updates per Server-Sent Events.</summary>
public sealed class WebServer
{
    private readonly HttpListener _listener = new();
    private readonly Tracker _tracker;
    private readonly List<StreamWriter> _clients = new();
    private readonly object _clientsLock = new();
    private readonly string? _webOverrideDir;

    public WebServer(Tracker tracker, int port, string? webOverrideDir)
    {
        _tracker = tracker;
        _webOverrideDir = webOverrideDir;
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _tracker.Broadcast += Send;
    }

    public void Start(CancellationToken ct)
    {
        _listener.Start();
        _ = Task.Run(() => AcceptLoop(ct), ct);
        _ = Task.Run(() => HeartbeatLoop(ct), ct);
    }

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

    private async Task HeartbeatLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(15000, ct).ContinueWith(_ => { });
            Write(": ping\n\n");
        }
    }

    private void Handle(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var res = ctx.Response;
        var path = req.Url?.AbsolutePath.TrimEnd('/') ?? "";
        if (path == "") path = "/";

        try
        {
            switch (req.HttpMethod, path)
            {
                case ("GET", "/"):
                case ("GET", "/control"):
                    ServeFile(res, "control.html");
                    break;
                case ("GET", "/setup"):
                    ServeFile(res, "setup.html");
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

    private void OpenEventStream(HttpListenerResponse res)
    {
        res.ContentType = "text/event-stream; charset=utf-8";
        res.Headers["Cache-Control"] = "no-cache";
        res.Headers["Access-Control-Allow-Origin"] = "*";
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

    private void Send(string json) => Write($"data: {json}\n\n");

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

    public static string? ReadEmbedded(string name)
    {
        using var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        if (s == null) return null;
        using var r = new StreamReader(s, Encoding.UTF8);
        return r.ReadToEnd();
    }

    private static void WriteText(HttpListenerResponse res, string text, string type, int status = 200)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        res.StatusCode = status;
        res.ContentType = type + "; charset=utf-8";
        res.Headers["Cache-Control"] = "no-cache";
        res.ContentLength64 = bytes.Length;
        res.OutputStream.Write(bytes, 0, bytes.Length);
        res.OutputStream.Close();
    }
}
