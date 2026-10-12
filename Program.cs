using System.Diagnostics;
using System.Text;
using System.Text.Json;
using DSRTracker;

Console.OutputEncoding = Encoding.UTF8;
Console.Title = "DSR Tracker";

int port = 8765;
bool openBrowser = true;
string? webDir = null;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--port" && i + 1 < args.Length && int.TryParse(args[i + 1], out var p)) port = p;
    if (args[i] == "--no-browser") openBrowser = false;
    if (args[i] == "--web" && i + 1 < args.Length) webDir = args[i + 1]; // zum Anpassen der HTML-Dateien ohne Neu-Bauen
}

var baseDir = AppContext.BaseDirectory;
var enemiesPath = Path.Combine(baseDir, "dsr_enemies.json");
var statePath = Path.Combine(baseDir, "state.json");

// Gegnerliste laden (externe Datei hat Vorrang, sonst eingebettete Standardliste)
string enemiesJson;
if (File.Exists(enemiesPath))
{
    enemiesJson = File.ReadAllText(enemiesPath);
}
else
{
    enemiesJson = WebServer.ReadEmbedded("dsr_enemies.json") ?? throw new Exception("Gegnerliste fehlt");
    try { File.WriteAllText(enemiesPath, enemiesJson); } catch { }
}

EnemyFile file;
try
{
    file = JsonSerializer.Deserialize<EnemyFile>(enemiesJson) ?? throw new Exception("leer");
}
catch (Exception ex)
{
    Console.WriteLine("Fehler in dsr_enemies.json: " + ex.Message);
    Console.WriteLine("Taste drücken zum Beenden …");
    Console.ReadKey();
    return;
}

if (args.Contains("--selftest"))
{
    // Prüft, ob alle Flag-IDs korrekt in Speicherpositionen umgerechnet werden (und zurück)
    int ok = 0, bad = 0;
    foreach (var area in file.Areas)
    foreach (var enemy in area.Enemies)
    foreach (var f in enemy.Flags)
    {
        bool valid = EventFlags.TryGetOffset(f, out int off, out uint mask);
        int bit = valid ? System.Numerics.BitOperations.LeadingZeroCount(mask) : -1;
        uint? back = valid ? EventFlags.FromPosition(off - off % 4, bit) : null;
        if (valid && back == f) ok++;
        else { bad++; Console.WriteLine($"FEHLER: {f} ({enemy.De}) -> {off}/{bit} -> {back}"); }
    }
    Console.WriteLine($"Selbsttest: {ok} ok, {bad} Fehler");
    return;
}

var tracker = new Tracker(file, statePath);
var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

var server = new WebServer(tracker, port, webDir);
try
{
    server.Start(cts.Token);
}
catch (Exception ex)
{
    Console.WriteLine($"Webserver konnte nicht auf Port {port} starten: {ex.Message}");
    Console.WriteLine("Anderen Port wählen: DSRTracker.exe --port 8766");
    Console.WriteLine("Taste drücken zum Beenden …");
    Console.ReadKey();
    return;
}

var url = $"http://localhost:{port}";
Console.WriteLine("================================================");
Console.WriteLine(" DSR Tracker – Dark Souls Remastered");
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
    try { Process.Start(new ProcessStartInfo(url + start) { UseShellExecute = true }); } catch { }
}

tracker.Run(cts.Token);
