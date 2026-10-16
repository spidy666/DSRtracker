// =====================================================================================
//  Tracker.cs – Herzstück: Gegnerliste, Kill-Erkennung, Zustand
// =====================================================================================
//  Enthält
//    • die Datenklassen der Gegnerliste (EnemyDef, AreaDef, RouteDef, EnemyFile)
//      samt Prüfung einer eigenen Liste (EnemyFile.Parse),
//    • den gespeicherten Zustand (SavedState → state.json),
//    • die Klasse Tracker: fragt das Spiel alle 0,5 s über den GameReader ab,
//      erkennt neue Kills, verwaltet manuelle Haken und schickt den Zustand
//      als JSON an alle verbundenen Seiten (über das Ereignis Broadcast → WebServer).
//
//  Schlüssel eines Gegners: "Gebietsindex-Gegnerindex", z. B. "0-0" = erster Gegner
//  im ersten Gebiet der Liste. Diese Schlüssel benutzen auch Steuerseite und Overlay.
// =====================================================================================

using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSRTracker;

/// <summary>Ein Gegner aus dsr_enemies.json.</summary>
public sealed class EnemyDef
{
    [JsonPropertyName("de")] public string De { get; set; } = "";          // deutscher Name (aus den Spieltexten)
    [JsonPropertyName("en")] public string En { get; set; } = "";          // englischer Name
    [JsonPropertyName("pts")] public int Pts { get; set; }                 // Punkte nach DLDU-Regeln
    [JsonPropertyName("flags")] public List<uint> Flags { get; set; } = new(); // Event-Flags; eines gesetzt = besiegt. Leer = nur manuell
    [JsonPropertyName("manual")] public bool Manual { get; set; }          // Markierung "respawnt, kein Flag" – nur zur Doku; im Code zählt, ob Flags leer ist
    [JsonPropertyName("tip")] public string Tip { get; set; } = "";        // kurzer Tipp für "Nächstes Ziel"
}

/// <summary>Ein Gebiet mit seinen Gegnern.</summary>
public sealed class AreaDef
{
    [JsonPropertyName("de")] public string De { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("enemies")] public List<EnemyDef> Enemies { get; set; } = new();
}

/// <summary>Ein Kapitel der Route: Titel + Gegner-Schlüssel in Spielreihenfolge.</summary>
public sealed class RouteDef
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("titleEn")] public string TitleEn { get; set; } = "";
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = new();
}

/// <summary>Die komplette Gegnerliste: Gebiete, Route und Zonenkarte (für "Aktuelles Gebiet").</summary>
public sealed class EnemyFile
{
    [JsonPropertyName("areas")] public List<AreaDef> Areas { get; set; } = new();
    [JsonPropertyName("route")] public List<RouteDef> Route { get; set; } = new();
    // Zonenkarte: [x, y, z, Gebietsindex] – Gebietsindex -1 = Feuerband-Schrein (keine Punkte)
    [JsonPropertyName("zones")] public List<float[]> Zones { get; set; } = new();

    /// <summary>Liest und prüft eine Gegnerliste. Gibt null und eine Fehlerbeschreibung zurück, wenn sie ungültig ist.</summary>
    public static EnemyFile? Parse(string json, out string error)
    {
        // Prüft alles, was später zu Abstürzen oder seltsamer Anzeige führen könnte:
        // Namen vorhanden und nicht zu lang, Punkte 0–100, Flags gültig, Routen-Schlüssel existieren,
        // Zonen-Einträge haben 4 Werte und verweisen auf ein vorhandenes Gebiet.
        EnemyFile? f;
        try { f = JsonSerializer.Deserialize<EnemyFile>(json); }
        catch (Exception ex) { error = "JSON-Fehler: " + ex.Message; return null; }
        if (f == null || f.Areas.Count == 0) { error = "keine Gebiete"; return null; }
        for (int a = 0; a < f.Areas.Count; a++)
        {
            var area = f.Areas[a];
            if (string.IsNullOrWhiteSpace(area.De) || area.Enemies.Count == 0) { error = $"Gebiet {a} ohne Name oder Gegner"; return null; }
            foreach (var e in area.Enemies)
            {
                if (string.IsNullOrWhiteSpace(e.De) || e.De.Length > 120 || e.En.Length > 120) { error = $"ungültiger Gegnername in {area.De}"; return null; }
                if (e.Pts is < 0 or > 100) { error = $"ungültige Punkte bei {e.De}"; return null; }
                foreach (var flag in e.Flags)
                    if (!EventFlags.TryGetOffset(flag, out _, out _)) { error = $"ungültiges Flag {flag} bei {e.De}"; return null; }
            }
        }
        foreach (var r in f.Route)
            foreach (var k in r.Keys)
            {
                var parts = k.Split('-');
                if (parts.Length != 2 || !int.TryParse(parts[0], out int ai) || !int.TryParse(parts[1], out int ei)
                    || ai < 0 || ai >= f.Areas.Count || ei < 0 || ei >= f.Areas[ai].Enemies.Count)
                { error = $"ungültiger Routen-Eintrag {k}"; return null; }
            }
        foreach (var z in f.Zones)
            if (z.Length != 4 || z[3] < -1 || z[3] >= f.Areas.Count) { error = "ungültiger Zonen-Eintrag"; return null; }
        error = "";
        return f;
    }
}

// igt = Spielzeit (ms) zum Zeitpunkt des Kills, null wenn unbekannt
public sealed record RecentKill(string key, string de, string en, int pts, string time, long? igt = null);

/// <summary>Inhalt von state.json: nur das, was der Spielstand nicht selbst weiß.</summary>
public sealed class SavedState
{
    [JsonPropertyName("manualDone")] public List<string> ManualDone { get; set; } = new();
    [JsonPropertyName("recentKills")] public List<RecentKill> RecentKills { get; set; } = new();
    [JsonPropertyName("areaOverride")] public int AreaOverride { get; set; } = -2;
    [JsonPropertyName("onboardingDone")] public bool OnboardingDone { get; set; }
}

/// <summary>Hält den Zustand aller Gegner und erkennt neue Kills.</summary>
public sealed class Tracker
{
    private readonly object _lock = new();
    private readonly GameReader _reader = new();
    private readonly List<(string Key, AreaDef Area, EnemyDef Enemy)> _enemies = new();
    private readonly EnemyFile _file;
    private readonly string _statePath;

    // ---- Zustand. Alles, was mehrere Threads anfassen (Tracker-Schleife + Webserver), nur unter _lock ändern.
    private readonly HashSet<string> _autoDone = new();       // vom Spiel erkannt (Flag gesetzt)
    private readonly HashSet<string> _manualDone = new();     // von Hand abgehakt (wird gespeichert)
    private readonly List<RecentKill> _recentKills = new();   // letzte Kills, neueste zuerst (Splits, Laufband)
    private readonly List<object> _flagLog = new();           // Flag-Monitor: zuletzt geänderte Flags
    private byte[]? _lastBlock;                               // Flag-Block vom letzten Durchlauf (zum Vergleichen)
    private bool _playerLoaded;                               // Charakter geladen (nicht im Menü)?
    private bool _hadFirstRead;                               // erster Lesevorgang nach dem Laden: noch keine Einblendungen
    private long? _igtMs;
    private (float X, float Y, float Z)? _pos;
    private int _autoArea = -2;      // -2 = unbekannt, -1 = Feuerband-Schrein, sonst Gebietsindex
    private int _areaOverride = -2;  // -2 = automatisch
    private long _lastSentIgt = -1;

    // Jede Nachricht (JSON) an alle offenen Seiten. Der WebServer hängt sich hier ein.
    public event Action<string>? Broadcast;

    /// <summary>Baut aus der Gegnerliste die flache Liste aller Gegner mit Schlüssel und lädt state.json.</summary>
    public Tracker(EnemyFile file, string statePath)
    {
        _file = file;
        _statePath = statePath;
        for (int a = 0; a < file.Areas.Count; a++)
        for (int e = 0; e < file.Areas[a].Enemies.Count; e++)
            _enemies.Add(($"{a}-{e}", file.Areas[a], file.Areas[a].Enemies[e]));

        foreach (var (_, area, enemy) in _enemies)
        foreach (var f in enemy.Flags)
            if (!EventFlags.TryGetOffset(f, out _, out _))
                Console.WriteLine($"  Warnung: Flag {f} bei '{enemy.De}' ({area.De}) ist ungültig.");

        LoadState();
    }

    public int EnemyCount => _enemies.Count;
    public int MaxPoints => _enemies.Sum(x => x.Enemy.Pts);
    public int FlagCount => _enemies.Count(x => x.Enemy.Flags.Count > 0);

    // ---------------------------------------------------------------- Abfrage-Schleife

    /// <summary>Hauptschleife: alle 0,5 s Poll(), Statusänderungen in Konsole und an die Seiten.</summary>
    public void Run(CancellationToken ct)
    {
        string lastStatus = "";
        while (!ct.IsCancellationRequested)
        {
            try { Poll(); }
            catch (Exception ex) { _reader.Detach("Fehler: " + ex.Message); }

            if (_reader.Status != lastStatus)
            {
                lastStatus = _reader.Status;
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {lastStatus}");
                PushState();
            }
            Thread.Sleep(500);
        }
        _reader.Dispose();
    }

    /// <summary>Ein Durchlauf: verbinden, Charakter prüfen, Zeit/Position/Flags lesen, Kills erkennen.</summary>
    private void Poll()
    {
        // 1. Nicht verbunden → Zustand zurücksetzen, beim nächsten Mal erneut versuchen
        if (!_reader.EnsureAttached())
        {
            if (_playerLoaded) { _playerLoaded = false; PushState(); }
            _lastBlock = null;
            _hadFirstRead = false;
            return;
        }

        // 2. Charakter geladen? Im Menü/Ladebildschirm sind die Daten nicht verlässlich.
        bool loaded = _reader.IsPlayerLoaded();
        if (loaded != _playerLoaded)
        {
            _playerLoaded = loaded;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {(loaded ? "Charakter geladen" : "Kein Charakter geladen (Menü/Ladebildschirm)")}");
            if (!loaded) { _lastBlock = null; _hadFirstRead = false; }
            PushState();
        }
        if (!loaded) return;

        // 3. Spielzeit lesen und ca. sekündlich an Overlay/Steuerseite schicken
        var igt = _reader.ReadIgtMs();
        if (igt != null)
        {
            _igtMs = igt;
            if (Math.Abs(igt.Value - _lastSentIgt) >= 1000)
            {
                _lastSentIgt = igt.Value;
                var pp = _reader.ReadPlayerPosition();
                Broadcast?.Invoke(JsonSerializer.Serialize(new { type = "time", igt = igt.Value,
                    pos = pp == null ? null : new[] { MathF.Round(pp.Value.X, 1), MathF.Round(pp.Value.Y, 1), MathF.Round(pp.Value.Z, 1) } }));
            }
        }

        // 4. Aktuelles Gebiet anhand der Position schätzen (nächster Punkt der Zonenkarte)
        var pos = _reader.ReadPlayerPosition();
        if (pos != null)
        {
            _pos = pos;
            int area = NearestZone(pos.Value);
            if (area != _autoArea)
            {
                _autoArea = area;
                PushState();
            }
        }

        // 5. Event-Flags lesen
        var block = _reader.ReadFlagBlock();
        if (block == null) return;

        bool changed = false;
        var newKills = new List<(string Key, EnemyDef Enemy)>();

        lock (_lock)
        {
            // 6. Flag-Monitor: alle geänderten Flags protokollieren (Vergleich Wort für Wort, dann Bit für Bit)
            if (_lastBlock != null)
            {
                for (int off = 0; off < block.Length; off += 4)
                {
                    uint oldW = BitConverter.ToUInt32(_lastBlock, off);
                    uint newW = BitConverter.ToUInt32(block, off);
                    if (oldW == newW) continue;
                    uint diff = oldW ^ newW;
                    for (int bit = 0; bit < 32; bit++)
                    {
                        uint m = 0x80000000u >> bit;
                        if ((diff & m) == 0) continue;
                        var id = EventFlags.FromPosition(off, bit);
                        if (id == null) continue;
                        _flagLog.Insert(0, new { id = id.Value, on = (newW & m) != 0, time = DateTime.Now.ToString("HH:mm:ss"), known = KnownName(id.Value) });
                        changed = true;
                    }
                }
                if (_flagLog.Count > 200) _flagLog.RemoveRange(200, _flagLog.Count - 200);
            }
            _lastBlock = block;

            // 7. Für jeden Gegner mit Flags: besiegt, wenn mindestens eines seiner Flags gesetzt ist
            foreach (var (key, _, enemy) in _enemies)
            {
                if (enemy.Flags.Count == 0) continue;
                bool done = enemy.Flags.Any(f => EventFlags.IsSet(block, f));
                bool was = _autoDone.Contains(key);
                if (done == was) continue;
                changed = true;
                if (done)
                {
                    _autoDone.Add(key);
                    // Neuer Kill nur, wenn schon einmal gelesen wurde (sonst wäre beim Laden alles "neu")
                    // und er nicht schon von Hand abgehakt war
                    if (_hadFirstRead && !_manualDone.Contains(key)) newKills.Add((key, enemy));
                }
                else
                {
                    // Flag wieder weg (anderer Spielstand geladen) → nicht mehr automatisch erledigt
                    _autoDone.Remove(key);
                    if (!_manualDone.Contains(key)) _recentKills.RemoveAll(k => k.key == key);
                }
            }

            // Wurde ein anderer Spielstand geladen, springen viele Flags gleichzeitig → keine Einblendungen
            if (newKills.Count > 3) newKills.Clear();
            foreach (var k in newKills) AddRecentKill(k.Key, k.Enemy);
            _hadFirstRead = true;

            // Liste der letzten Kills an den geladenen Spielstand anpassen
            // (z. B. nach Neustart des Tools oder wenn ein anderer Charakter geladen wurde)
            if (_recentKills.RemoveAll(k => !_autoDone.Contains(k.key) && !_manualDone.Contains(k.key)) > 0)
            {
                changed = true;
                SaveState();
            }
        }

        // 8. Außerhalb des Locks: Kills melden (Konsole + Einblendung in den Overlays), Zustand senden
        foreach (var k in newKills)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Erledigt: {k.Enemy.De} (+{k.Enemy.Pts})");
            Broadcast?.Invoke(JsonSerializer.Serialize(new { type = "kill", key = k.Key, de = k.Enemy.De, en = k.Enemy.En, pts = k.Enemy.Pts }));
        }
        if (changed) PushState();
    }

    /// <summary>
    /// Schätzt das Gebiet: nächstgelegener Punkt der Zonenkarte (Abstand im Quadrat reicht zum Vergleichen).
    /// Rückgabe: Gebietsindex, -1 = Feuerband-Schrein, -2 = keine Zonen bekannt.
    /// </summary>
    private int NearestZone((float X, float Y, float Z) p)
    {
        int best = -2; float bestD = float.MaxValue;
        foreach (var z in _file.Zones)
        {
            if (z.Length < 4) continue;
            float dx = z[0] - p.X, dy = z[1] - p.Y, dz = z[2] - p.Z;
            float d = dx * dx + dy * dy + dz * dz;
            if (d < bestD) { bestD = d; best = (int)z[3]; }
        }
        return best;
    }

    public bool OnboardingDone { get; private set; }

    /// <summary>Merkt sich, ob der Einrichtungsassistent abgeschlossen ist (dann öffnet der Start die Steuerseite).</summary>
    public void SetOnboardingDone(bool done)
    {
        lock (_lock) { OnboardingDone = done; SaveState(); }
    }

    /// <summary>Schickt eine Test-Einblendung an alle Overlays, ohne den Zustand zu ändern.</summary>
    public void SendTestToast()
    {
        Broadcast?.Invoke(JsonSerializer.Serialize(new { type = "kill", key = "test", de = "Test-Einblendung", en = "Test notification", pts = 5 }));
    }

    /// <summary>Gebiet fest einstellen (-2 = wieder automatisch). Ungültige Werte werden zu -2.</summary>
    public void SetAreaOverride(int idx)
    {
        lock (_lock)
        {
            _areaOverride = idx >= -2 && idx < _file.Areas.Count ? idx : -2;
            SaveState();
        }
        PushState();
    }

    /// <summary>Für den Flag-Monitor: gehört das Flag zu einem Gegner der Liste? Dann dessen Name.</summary>
    private string? KnownName(uint flag)
    {
        foreach (var (_, area, enemy) in _enemies)
            if (enemy.Flags.Contains(flag)) return $"{enemy.De} ({area.De})";
        return null;
    }

    /// <summary>Kill vorne in die Liste der letzten Kills, mit Uhrzeit und Spielzeit.</summary>
    private void AddRecentKill(string key, EnemyDef enemy)
    {
        // jeder Gegner höchstens einmal in der Liste
        _recentKills.RemoveAll(k => k.key == key);
        _recentKills.Insert(0, new RecentKill(key, enemy.De, enemy.En, enemy.Pts, DateTime.Now.ToString("HH:mm:ss"), _playerLoaded ? _igtMs : null));
        SaveState();
    }

    // ---------------------------------------------------------------- manuelle Eingriffe

    /// <summary>
    /// Klick auf der Steuerseite: manuellen Haken setzen oder entfernen. false bei unbekanntem Schlüssel.
    /// Ein neuer Haken erzeugt auch eine Einblendung, außer der Gegner war schon automatisch erkannt.
    /// </summary>
    public bool ToggleManual(string key)
    {
        EnemyDef? enemy = null;
        bool nowDone;
        lock (_lock)
        {
            var entry = _enemies.FirstOrDefault(x => x.Key == key);
            if (entry.Enemy == null) return false;
            enemy = entry.Enemy;
            if (_manualDone.Remove(key))
            {
                nowDone = false;
                // nicht mehr erledigt → aus der Liste der letzten Kills entfernen
                if (!_autoDone.Contains(key)) _recentKills.RemoveAll(k => k.key == key);
            }
            else
            {
                _manualDone.Add(key);
                nowDone = true;
                if (!_autoDone.Contains(key)) AddRecentKill(key, enemy);
            }
            SaveState();
        }
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Manuell {(nowDone ? "abgehakt" : "zurückgesetzt")}: {enemy.De}");
        if (nowDone && !_autoDone.Contains(key))
            Broadcast?.Invoke(JsonSerializer.Serialize(new { type = "kill", key, de = enemy.De, en = enemy.En, pts = enemy.Pts }));
        PushState();
        return true;
    }

    /// <summary>Alle manuellen Haken entfernen. Automatisch erkannte bleiben (die kommen aus dem Spielstand).</summary>
    public void ResetManual()
    {
        lock (_lock)
        {
            _manualDone.Clear();
            _recentKills.RemoveAll(k => !_autoDone.Contains(k.key));
            SaveState();
        }
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Manuelle Haken zurückgesetzt");
        PushState();
    }

    public void ClearFlagLog()
    {
        lock (_lock) _flagLog.Clear();
        PushState();
    }

    // ---------------------------------------------------------------- Zustand

    /// <summary>
    /// Der komplette Zustand als JSON (type = "state"). Das ist alles, was Steuerseite und Overlay brauchen:
    /// Punkte, alle Gebiete mit Gegnern (erledigt? auto/manuell?), Verbindung, Spielzeit, Gebiet, Route,
    /// letzte Kills und Flag-Monitor. Wird bei jeder Änderung an alle Seiten geschickt.
    /// </summary>
    public string GetStateJson()
    {
        lock (_lock)
        {
            int total = 0;
            var areas = _file.Areas.Select((area, ai) =>
            {
                var enemies = area.Enemies.Select((e, ei) =>
                {
                    var key = $"{ai}-{ei}";
                    bool auto = _autoDone.Contains(key);
                    bool man = _manualDone.Contains(key);
                    return new
                    {
                        key, de = e.De, en = e.En, pts = e.Pts,
                        done = auto || man, auto, manual = man, tip = e.Tip,
                        tracked = e.Flags.Count > 0,
                    };
                }).ToList();
                int pts = enemies.Where(x => x.done).Sum(x => x.pts);
                total += pts;
                return new
                {
                    de = area.De, en = area.En,
                    done = enemies.Count(x => x.done), count = enemies.Count,
                    pts, maxPts = enemies.Sum(x => x.pts),
                    enemies,
                };
            }).ToList();

            return JsonSerializer.Serialize(new
            {
                type = "state",
                connected = _reader.IsAttached,
                playerLoaded = _playerLoaded,
                status = _reader.Status,
                gameVersion = _reader.GameVersion,
                total,
                maxTotal = MaxPoints,
                done = areas.Sum(a => a.done),
                count = EnemyCount,
                areas,
                igt = _igtMs,
                onboardingDone = OnboardingDone,
                currentArea = _areaOverride != -2 ? _areaOverride : _autoArea,
                areaAuto = _areaOverride == -2,
                autoArea = _autoArea,
                pos = _pos == null ? null : new[] { MathF.Round(_pos.Value.X, 1), MathF.Round(_pos.Value.Y, 1), MathF.Round(_pos.Value.Z, 1) },
                route = _file.Route.Select(r => new { title = r.Title, titleEn = r.TitleEn, keys = r.Keys }),
                recentKills = _recentKills,
                flagLog = _flagLog.Take(60),
            });
        }
    }

    // Zustand an alle offenen Seiten schicken
    private void PushState() => Broadcast?.Invoke(GetStateJson());

    /// <summary>state.json lesen. Unbekannte Schlüssel (z. B. aus einer alten Gegnerliste) werden ignoriert.</summary>
    private void LoadState()
    {
        try
        {
            if (!File.Exists(_statePath)) return;
            var s = JsonSerializer.Deserialize<SavedState>(File.ReadAllText(_statePath));
            if (s == null) return;
            foreach (var k in s.ManualDone)
                if (_enemies.Any(x => x.Key == k)) _manualDone.Add(k);
            _areaOverride = s.AreaOverride;
            OnboardingDone = s.OnboardingDone;
            foreach (var k in s.RecentKills)
                if (_enemies.Any(x => x.Key == k.key) && _recentKills.All(r => r.key != k.key)) _recentKills.Add(k);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Warnung: state.json konnte nicht gelesen werden: " + ex.Message);
        }
    }

    /// <summary>state.json schreiben. Fehler werden nur gemeldet, der Tracker läuft weiter.</summary>
    private void SaveState()
    {
        try
        {
            var s = new SavedState { ManualDone = _manualDone.ToList(), RecentKills = _recentKills.ToList(), AreaOverride = _areaOverride, OnboardingDone = OnboardingDone };
            File.WriteAllText(_statePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Warnung: state.json konnte nicht gespeichert werden: " + ex.Message);
        }
    }
}
