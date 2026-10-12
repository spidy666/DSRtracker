using System.Text.Json;
using System.Text.Json.Serialization;

namespace DSRTracker;

public sealed class EnemyDef
{
    [JsonPropertyName("de")] public string De { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("pts")] public int Pts { get; set; }
    [JsonPropertyName("flags")] public List<uint> Flags { get; set; } = new();
    [JsonPropertyName("manual")] public bool Manual { get; set; }
    [JsonPropertyName("tip")] public string Tip { get; set; } = "";
}

public sealed class AreaDef
{
    [JsonPropertyName("de")] public string De { get; set; } = "";
    [JsonPropertyName("en")] public string En { get; set; } = "";
    [JsonPropertyName("enemies")] public List<EnemyDef> Enemies { get; set; } = new();
}

public sealed class RouteDef
{
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("titleEn")] public string TitleEn { get; set; } = "";
    [JsonPropertyName("keys")] public List<string> Keys { get; set; } = new();
}

public sealed class EnemyFile
{
    [JsonPropertyName("areas")] public List<AreaDef> Areas { get; set; } = new();
    [JsonPropertyName("route")] public List<RouteDef> Route { get; set; } = new();
    // Zonenkarte: [x, y, z, Gebietsindex] – Gebietsindex -1 = Feuerband-Schrein (keine Punkte)
    [JsonPropertyName("zones")] public List<float[]> Zones { get; set; } = new();
}

// igt = Spielzeit (ms) zum Zeitpunkt des Kills, null wenn unbekannt
public sealed record RecentKill(string key, string de, string en, int pts, string time, long? igt = null);

public sealed class SavedState
{
    [JsonPropertyName("manualDone")] public List<string> ManualDone { get; set; } = new();
    [JsonPropertyName("recentKills")] public List<RecentKill> RecentKills { get; set; } = new();
    [JsonPropertyName("areaOverride")] public int AreaOverride { get; set; } = -2;
}

/// <summary>Hält den Zustand aller Gegner und erkennt neue Kills.</summary>
public sealed class Tracker
{
    private readonly object _lock = new();
    private readonly GameReader _reader = new();
    private readonly List<(string Key, AreaDef Area, EnemyDef Enemy)> _enemies = new();
    private readonly EnemyFile _file;
    private readonly string _statePath;

    private readonly HashSet<string> _autoDone = new();
    private readonly HashSet<string> _manualDone = new();
    private readonly List<RecentKill> _recentKills = new();
    private readonly List<object> _flagLog = new();
    private byte[]? _lastBlock;
    private bool _playerLoaded;
    private bool _hadFirstRead;
    private long? _igtMs;
    private (float X, float Y, float Z)? _pos;
    private int _autoArea = -2;      // -2 = unbekannt, -1 = Feuerband-Schrein, sonst Gebietsindex
    private int _areaOverride = -2;  // -2 = automatisch
    private long _lastSentIgt = -1;

    public event Action<string>? Broadcast;

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

    private void Poll()
    {
        if (!_reader.EnsureAttached())
        {
            if (_playerLoaded) { _playerLoaded = false; PushState(); }
            _lastBlock = null;
            _hadFirstRead = false;
            return;
        }

        bool loaded = _reader.IsPlayerLoaded();
        if (loaded != _playerLoaded)
        {
            _playerLoaded = loaded;
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {(loaded ? "Charakter geladen" : "Kein Charakter geladen (Menü/Ladebildschirm)")}");
            if (!loaded) { _lastBlock = null; _hadFirstRead = false; }
            PushState();
        }
        if (!loaded) return;

        // Spielzeit lesen und ca. sekündlich an Overlay/Steuerseite schicken
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

        // Aktuelles Gebiet anhand der Position schätzen (nächster Punkt der Zonenkarte)
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

        var block = _reader.ReadFlagBlock();
        if (block == null) return;

        bool changed = false;
        var newKills = new List<(string Key, EnemyDef Enemy)>();

        lock (_lock)
        {
            // Flag-Monitor: alle geänderten Flags protokollieren
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
                    if (_hadFirstRead && !_manualDone.Contains(key)) newKills.Add((key, enemy));
                }
                else
                {
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

        foreach (var k in newKills)
        {
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Erledigt: {k.Enemy.De} (+{k.Enemy.Pts})");
            Broadcast?.Invoke(JsonSerializer.Serialize(new { type = "kill", key = k.Key, de = k.Enemy.De, en = k.Enemy.En, pts = k.Enemy.Pts }));
        }
        if (changed) PushState();
    }

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

    public void SetAreaOverride(int idx)
    {
        lock (_lock)
        {
            _areaOverride = idx >= -2 && idx < _file.Areas.Count ? idx : -2;
            SaveState();
        }
        PushState();
    }

    private string? KnownName(uint flag)
    {
        foreach (var (_, area, enemy) in _enemies)
            if (enemy.Flags.Contains(flag)) return $"{enemy.De} ({area.De})";
        return null;
    }

    private void AddRecentKill(string key, EnemyDef enemy)
    {
        // jeder Gegner höchstens einmal in der Liste
        _recentKills.RemoveAll(k => k.key == key);
        _recentKills.Insert(0, new RecentKill(key, enemy.De, enemy.En, enemy.Pts, DateTime.Now.ToString("HH:mm:ss"), _playerLoaded ? _igtMs : null));
        SaveState();
    }

    // ---------------------------------------------------------------- manuelle Eingriffe

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
                total,
                maxTotal = MaxPoints,
                done = areas.Sum(a => a.done),
                count = EnemyCount,
                areas,
                igt = _igtMs,
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

    private void PushState() => Broadcast?.Invoke(GetStateJson());

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
            foreach (var k in s.RecentKills)
                if (_enemies.Any(x => x.Key == k.key) && _recentKills.All(r => r.key != k.key)) _recentKills.Add(k);
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Warnung: state.json konnte nicht gelesen werden: " + ex.Message);
        }
    }

    private void SaveState()
    {
        try
        {
            var s = new SavedState { ManualDone = _manualDone.ToList(), RecentKills = _recentKills.ToList(), AreaOverride = _areaOverride };
            File.WriteAllText(_statePath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            Console.WriteLine("  Warnung: state.json konnte nicht gespeichert werden: " + ex.Message);
        }
    }
}
