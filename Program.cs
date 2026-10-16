// =====================================================================================
//  Program.cs – Startpunkt der exe
// =====================================================================================
//  Ablauf beim Start:
//    1. Startoptionen auswerten (--port, --no-browser, --portable, --web, --selftest)
//    2. Datenordner festlegen (%APPDATA%\DSRTracker oder neben der exe)
//    3. Gegnerliste laden (eingebaut oder eigene, geprüfte Datei)
//    4. Tracker und lokalen Webserver starten
//    5. Infos in der Konsole ausgeben, Browser öffnen
//    6. Tracker-Schleife laufen lassen, bis das Fenster geschlossen wird
//
//  Diese Datei nutzt "Top-Level-Statements": Es gibt keine Main-Methode, der Code
//  wird einfach von oben nach unten ausgeführt.
// =====================================================================================

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DSRTracker;

Console.OutputEncoding = Encoding.UTF8; // Umlaute und Sonderzeichen in der Konsole richtig anzeigen
Console.Title = "DSR Tracker";

// ---- 1. Startoptionen
int port = 8765;
bool openBrowser = true;
bool portable = false;
string? webDir = null;
for (int i = 0; i < args.Length; i++)
{
    // Ports unter 1024 sind unter Windows/Linux oft reserviert, daher nur 1024–65535
    if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var p) && p is >= 1024 and <= 65535) port = p;
    if (args[i] == "--no-browser") openBrowser = false;
    if (args[i] == "--portable") portable = true; // Daten neben der exe statt unter %APPDATA%
    if (args[i] == "--web" && i + 1 < args.Length) webDir = args[i + 1]; // zum Anpassen der HTML-Dateien ohne Neu-Bauen
}

// ---- 2. Datenordner: %APPDATA%\DSRTracker (bzw. neben der exe mit --portable)
var baseDir = AppContext.BaseDirectory;  // Ordner, in dem die exe liegt
var dataDir = portable ? baseDir : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DSRTracker");
try { Directory.CreateDirectory(dataDir); } catch { dataDir = baseDir; } // Notfall: neben der exe
var statePath = Path.Combine(dataDir, "state.json");          // Haken, letzte Kills, Einstellungen
var enemiesPath = Path.Combine(dataDir, "dsr_enemies.json");  // optionale eigene Gegnerliste

// Einmalige Übernahme des alten Spielstands neben der exe (frühere Versionen speicherten dort)
var oldState = Path.Combine(baseDir, "state.json");
if (!portable && !File.Exists(statePath) && File.Exists(oldState))
{
    try { File.Copy(oldState, statePath); Console.WriteLine($" Spielstand übernommen: {oldState} -> {statePath}"); } catch { }
}

// ---- 3. Gegnerliste: eingebaute Liste, außer man legt bewusst eine eigene dsr_enemies.json in den Datenordner
var embeddedJson = WebServer.ReadEmbedded("dsr_enemies.json") ?? throw new Exception("Gegnerliste fehlt");
EnemyFile file = EnemyFile.Parse(embeddedJson, out _) ?? throw new Exception("Eingebaute Gegnerliste ist ungültig");
if (File.Exists(enemiesPath))
{
    // Eigene Liste nur verwenden, wenn sie die Prüfung in EnemyFile.Parse besteht
    var custom = EnemyFile.Parse(File.ReadAllText(enemiesPath), out var error);
    if (custom != null) { file = custom; Console.WriteLine(" Eigene Gegnerliste: " + enemiesPath); }
    else Console.WriteLine($" Warnung: {enemiesPath} ist ungültig ({error}) – verwende die eingebaute Liste.");
}
if (!portable && File.Exists(Path.Combine(baseDir, "dsr_enemies.json")))
    Console.WriteLine(" Hinweis: dsr_enemies.json neben der exe wird nicht mehr verwendet (eingebaute Liste ist aktuell).");

// ---- Selbsttest (läuft auch im Build-Workflow auf GitHub)
if (args.Contains("--selftest"))
{
    // Prüft, ob alle Flag-IDs korrekt in Speicherpositionen umgerechnet werden (und zurück)
    int ok = 0, bad = 0;
    foreach (var area in file.Areas)
    foreach (var enemy in area.Enemies)
    foreach (var f in enemy.Flags)
    {
        bool valid = EventFlags.TryGetOffset(f, out int off, out uint mask);
        int bit = valid ? System.Numerics.BitOperations.LeadingZeroCount(mask) : -1; // Maske → Bitnummer
        uint? back = valid ? EventFlags.FromPosition(off - off % 4, bit) : null;     // und wieder zurück
        if (valid && back == f) ok++;
        else { bad++; Console.WriteLine($"FEHLER: {f} ({enemy.De}) -> {off}/{bit} -> {back}"); }
    }
    Console.WriteLine($"Selbsttest: {ok} ok, {bad} Fehler");
    return;
}

// ---- 4. Tracker und Webserver starten
var tracker = new Tracker(file, statePath);
var cts = new CancellationTokenSource();
// Strg+C beendet sauber: Schleife stoppen statt den Prozess hart abzubrechen
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var server = new WebServer(tracker, port, webDir);
try
{
    server.Start(cts.Token);
}
catch (Exception ex)
{
    // Häufigste Ursache: Port schon belegt (z. B. Tracker läuft bereits)
    Console.WriteLine($"Webserver konnte nicht auf Port {port} starten: {ex.Message}");
    Console.WriteLine("Anderen Port wählen: DSRTracker.exe --port 8766");
    Console.WriteLine("Taste drücken zum Beenden …");
    Console.ReadKey();
    return;
}

// ---- 5. Infos in der Konsole
var url = $"http://localhost:{port}";
Console.WriteLine("================================================");
var ver = typeof(Tracker).Assembly.GetName().Version; // kommt aus <Version> in DSRTracker.csproj
Console.WriteLine($" DSR Tracker v{ver?.Major}.{ver?.Minor}.{ver?.Build} – Dark Souls Remastered");
Console.WriteLine("================================================");
Console.WriteLine($" {tracker.EnemyCount} Gegner, {tracker.MaxPoints} Punkte möglich, {tracker.FlagCount} automatisch erkennbar");
Console.WriteLine();
Console.WriteLine($" Steuerung:        {url}/");
Console.WriteLine($" Einrichtung:      {url}/setup");
Console.WriteLine($" Guide:            {url}/guide");
Console.WriteLine($" Overlay (OBS):    {url}/overlay");
Console.WriteLine($" Overlay kompakt:  {url}/overlay?view=compact");
Console.WriteLine();
Console.WriteLine(" Liest das Spiel nur (kein Schreiben in den Speicher).");
Console.WriteLine(" Beenden: Fenster schließen oder Strg+C");
Console.WriteLine("================================================");

if (openBrowser)
{
    // Beim ersten Start den Einrichtungsassistenten öffnen, danach die Steuerseite
    var start = tracker.OnboardingDone ? "/" : "/setup";
    // UseShellExecute = true: Windows öffnet die URL im Standardbrowser
    try { Process.Start(new ProcessStartInfo(url + start) { UseShellExecute = true }); } catch { }
}

// ---- 6. Hauptschleife (blockiert, bis Strg+C oder Fenster geschlossen)
tracker.Run(cts.Token);
