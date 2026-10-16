// =====================================================================================
//  GameReader.cs – Zugriff auf Dark Souls Remastered (nur lesend)
// =====================================================================================
//  Diese Datei ist die einzige Stelle, die mit dem Spiel spricht. Sie
//    1. findet den Prozess DarkSoulsRemastered.exe,
//    2. öffnet ihn ausschließlich mit Leserechten,
//    3. sucht im Programmcode des Spiels nach bekannten Byte-Mustern ("Signaturen"),
//       um herauszufinden, wo im Speicher die interessanten Daten liegen,
//    4. liest von dort: Event-Flags (wer besiegt ist), Spielerposition und Spielzeit.
//
//  Ganz unten steht die Klasse EventFlags, die eine Flag-Nummer (z. B. 11810900)
//  in eine Speicherposition (Byte + Bit) im Flag-Block umrechnet und zurück.
//
//  Wichtig für die Sicherheit: Es gibt hier kein WriteProcessMemory, kein
//  VirtualAllocEx, kein CreateRemoteThread – also weder Schreiben noch Code-Injection.
// =====================================================================================

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DSRTracker;

/// <summary>
/// Liest Dark Souls Remastered ausschließlich lesend (ReadProcessMemory).
/// Der Prozess wird nur mit PROCESS_VM_READ | PROCESS_QUERY_LIMITED_INFORMATION geöffnet.
/// Es wird nichts in den Spielspeicher geschrieben, kein Code und keine DLL eingeschleust.
///
/// Die Signaturen und die Event-Flag-Adressierung stammen aus öffentlicher
/// Community-Forschung (JKAnderson / DSR-Gadget, FrankvdStam / SoulMemory).
/// </summary>
public sealed class GameReader : IDisposable
{
    // Prozessname ohne ".exe", so wie Windows ihn in der Prozessliste führt
    private const string ProcessName = "DarkSoulsRemastered";

    // Gesamtgröße des Event-Flag-Blocks (Gruppe 7 beginnt bei 0x11300, 18 Gebiete à 0x500)
    public const int FlagBlockSize = 0x11300 + 18 * 0x500;

    // ---- Signaturen ("AOB" = Array of Bytes)
    // Das sind kurze Ausschnitte aus dem Maschinencode des Spiels. Jede Signatur enthält
    // einen Befehl der Form "mov rcx, [rip + Abstand]". Findet man die Stelle, kann man
    // aus dem Abstand die Adresse eines globalen Zeigers ausrechnen (siehe ScanRelative).
    // "?" steht für Bytes, die sich je nach Build unterscheiden dürfen (eben dieser Abstand).
    //   EventFlags   → Zeiger auf den Manager der Event-Flags (wer ist besiegt, welche Tür offen …)
    //   WorldChrMan  → Zeiger auf die Liste der Figuren in der Welt (darüber: Spielerposition)
    //   GameDataMan  → Zeiger auf allgemeine Spieldaten (darüber: Spielzeit)
    private const string EventFlagsPattern = "48 8B 0D ? ? ? ? 99 33 C2 45 33 C0 2B C2 8D 50 F6";
    private const string WorldChrManPattern = "48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c";
    private const string GameDataManPattern = "48 8b 05 ? ? ? ? 48 8b 50 10 48 89 54 24 60";

    private Process? _process;                          // der gefundene Spielprozess (null = nicht verbunden)
    private IntPtr _handle = IntPtr.Zero;               // Windows-Handle mit Leserechten auf den Prozess
    private long _eventFlagsStatic;                     // Adresse des globalen EventFlags-Zeigers (0 = unbekannt)
    private long _worldChrStatic;                       // Adresse des globalen WorldChrMan-Zeigers
    private long _gameDataManStatic;                    // Adresse des globalen GameDataMan-Zeigers (optional)
    private DateTime _nextAttachAttempt = DateTime.MinValue; // Verbindungsversuche nur alle 2 Sekunden
    private int _failedScans;                           // wie oft die Signatursuche erfolglos war

    /// <summary>Erkannte Spielversion (über die SHA256-Prüfsumme der exe), sonst "unbekannt".</summary>
    public string? GameVersion { get; private set; }

    // Die Dateiversion der DSR-exe ist immer "1,0,0,0" und taugt nicht zur Unterscheidung.
    // Deshalb werden bekannte exe-Dateien über ihre SHA256-Prüfsumme erkannt.
    private static readonly Dictionary<string, string> KnownBuilds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["a45aaa36dd2f6cc151670a639ea5547043cf38ea79ff4178b963c6ed71f98d7b"] = "1.03.1 (Steam)",
    };
    // Die exe ist ~50 MB groß; damit sie nicht bei jedem Verbinden neu gehasht wird,
    // merken wir uns das Ergebnis pro Pfad + Größe + Änderungszeit.
    private static readonly Dictionary<string, string> HashCache = new();

    /// <summary>Bestimmt die Spielversion anhand der SHA256-Prüfsumme der exe-Datei auf der Festplatte.</summary>
    private static string IdentifyBuild(string exePath)
    {
        try
        {
            var info = new FileInfo(exePath);
            var cacheKey = $"{exePath}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
            if (!HashCache.TryGetValue(cacheKey, out var hash))
            {
                // FileShare.ReadWrite | Delete: die Datei ist vom laufenden Spiel geöffnet;
                // wir lesen sie nur und blockieren nichts.
                using var fs = new FileStream(exePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(fs)).ToLowerInvariant();
                HashCache[cacheKey] = hash;
            }
            // Unbekannte Builds werden trotzdem gelesen; angezeigt wird dann der Anfang der Prüfsumme
            return KnownBuilds.TryGetValue(hash, out var name) ? name : $"unbekannt (SHA256 {hash[..12]}…)";
        }
        catch (Exception ex)
        {
            return "unbekannt (" + ex.Message + ")";
        }
    }

    // "Verbunden" heißt: Prozess gefunden, Handle offen und die Flag-Signatur gefunden
    public bool IsAttached => _process != null && _handle != IntPtr.Zero && _eventFlagsStatic != 0;
    // Klartext-Status für Konsole und Steuerseite
    public string Status { get; private set; } = "Suche DarkSoulsRemastered.exe …";

    /// <summary>Verbindet sich mit dem Spiel, falls noch nicht geschehen. Gibt true zurück, wenn verbunden.</summary>
    /// <remarks>
    /// Wird vom Tracker bei jedem Durchlauf (alle ~0,5 s) aufgerufen. Ablauf:
    /// 1. Ist der bekannte Prozess beendet? → trennen.
    /// 2. Schon verbunden? → fertig.
    /// 3. Höchstens alle 2 s: Prozess suchen, Dateiname prüfen, Version bestimmen, Handle öffnen.
    /// 4. Signaturen im Programmcode suchen. Klappt das nicht (z. B. direkt nach Spielstart),
    ///    wird es beim nächsten Durchlauf erneut versucht.
    /// </remarks>
    public bool EnsureAttached()
    {
        // 1. Wurde das Spiel inzwischen beendet?
        if (_process != null)
        {
            bool exited;
            try { exited = _process.HasExited; } catch { exited = true; }
            if (exited)
            {
                Detach("Spiel wurde beendet – warte auf DarkSoulsRemastered.exe …");
            }
        }

        // 2. Alles schon bereit
        if (IsAttached) return true;
        // 3. Nicht im Dauerfeuer suchen – nur alle 2 Sekunden
        if (DateTime.UtcNow < _nextAttachAttempt) return false;
        _nextAttachAttempt = DateTime.UtcNow.AddSeconds(2);

        if (_process == null)
        {
            var candidates = Process.GetProcessesByName(ProcessName);
            if (candidates.Length == 0)
            {
                Status = "Suche DarkSoulsRemastered.exe …";
                return false;
            }
            _process = candidates[0];
            // Prüfen, dass es wirklich die Spiel-exe ist (Dateiname), und Version merken
            try
            {
                var exe = _process.MainModule?.FileName ?? "";
                if (!Path.GetFileName(exe).Equals(ProcessName + ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    Detach("Gefundener Prozess ist nicht DarkSoulsRemastered.exe – warte …");
                    return false;
                }
                GameVersion = IdentifyBuild(exe);
            }
            catch (Exception ex)
            {
                // Typisch, wenn das Spiel unter einem anderen Benutzer oder als Administrator läuft
                Detach($"Spiel gefunden, aber Zugriff auf die Prozessinfo nicht möglich ({ex.Message}). Läuft das Spiel unter einem anderen Benutzer?");
                return false;
            }
            // Minimal nötige Rechte: Speicher lesen + eingeschränkte Prozessinfo. Kein Schreibrecht.
            _handle = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_QUERY_LIMITED_INFORMATION, false, _process.Id);
            if (_handle == IntPtr.Zero)
            {
                Detach($"Konnte das Spiel nicht zum Lesen öffnen (Fehler {Marshal.GetLastWin32Error()}). Spiel und Tracker sollten unter demselben Benutzer und ohne Administratorrechte laufen.");
                return false;
            }
            _failedScans = 0;
        }

        // 4. Signaturen suchen (kann direkt nach Spielstart noch fehlschlagen → später erneut)
        try
        {
            // Das komplette Hauptmodul (den geladenen Programmcode der exe) einmal einlesen …
            var module = _process.MainModule!;
            long baseAddr = module.BaseAddress.ToInt64();
            int size = module.ModuleMemorySize;
            var image = ReadModuleImage(baseAddr, size);

            // … und darin die drei Signaturen suchen. 3 = Position des 4-Byte-Abstands im Befehl,
            // 7 = Länge des Befehls (der Abstand zählt ab dem Ende des Befehls).
            _eventFlagsStatic = ScanRelative(image, baseAddr, EventFlagsPattern, 3, 7);
            _worldChrStatic = ScanRelative(image, baseAddr, WorldChrManPattern, 3, 7);
            _gameDataManStatic = ScanRelative(image, baseAddr, GameDataManPattern, 3, 7); // optional (Spielzeit)

            if (_eventFlagsStatic == 0 || _worldChrStatic == 0)
            {
                _eventFlagsStatic = 0;
                // direkt nach Spielstart normal; bleibt es dauerhaft so, passt die Spielversion vermutlich nicht
                Status = ++_failedScans < 15
                    ? "Spiel gefunden, Speicherstruktur noch nicht bereit – versuche erneut …"
                    : $"Speicherstruktur nicht gefunden – evtl. nicht unterstützte Spielversion: {GameVersion ?? "unbekannt"}. Getestet mit Dark Souls Remastered 1.03.1 (Steam).";
                return false;
            }

            Status = "Verbunden mit Dark Souls Remastered" + (GameVersion != null ? $" – Version {GameVersion}" : "");
            return true;
        }
        catch (Exception ex)
        {
            Detach($"Fehler beim Verbinden: {ex.Message}");
            return false;
        }
    }

    // ---- Zeigerketten
    // Die Spieldaten liegen nicht an festen Adressen, sondern hinter mehreren Zeigern:
    //   globaler Zeiger → Objekt → Unterobjekt → … → Wert
    // Die Abstände (0x68, 0x28, 0x10, 0xa4 …) stammen aus der Community-Forschung.
    // Ist ein Zeiger 0, ist das Objekt (noch) nicht da, z. B. im Hauptmenü.

    /// <summary>True, wenn ein Charakter geladen ist (nicht im Hauptmenü / Ladebildschirm).</summary>
    public bool IsPlayerLoaded()
    {
        if (!IsAttached) return false;
        long wcm = ReadInt64(_worldChrStatic);        // WorldChrMan
        if (wcm == 0) return false;
        long playerIns = ReadInt64(wcm + 0x68);       // → PlayerIns (die Spielfigur)
        return playerIns != 0;
    }

    /// <summary>Spielerposition (Weltkoordinaten), null wenn nicht verfügbar.</summary>
    public (float X, float Y, float Z)? ReadPlayerPosition()
    {
        if (!IsAttached) return null;
        long wcm = ReadInt64(_worldChrStatic);        // WorldChrMan
        if (wcm == 0) return null;
        long playerIns = ReadInt64(wcm + 0x68);       // → PlayerIns
        if (playerIns == 0) return null;
        long ctrl = ReadInt64(playerIns + 0x68);      // → Steuerung der Figur
        if (ctrl == 0) return null;
        long posObj = ReadInt64(ctrl + 0x28);         // → Positionsobjekt
        if (posObj == 0) return null;
        var buf = new byte[12];                       // drei float-Werte: X, Y, Z
        if (!ReadBytes(posObj + 0x10, buf)) return null;
        float x = BitConverter.ToSingle(buf, 0), y = BitConverter.ToSingle(buf, 4), z = BitConverter.ToSingle(buf, 8);
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) return null; // Müll beim Laden abfangen
        return (x, y, z);
    }

    /// <summary>Spielzeit (In-Game-Time) in Millisekunden, null wenn nicht verfügbar.</summary>
    public long? ReadIgtMs()
    {
        if (!IsAttached || _gameDataManStatic == 0) return null;
        long gdm = ReadInt64(_gameDataManStatic);     // GameDataMan
        if (gdm == 0) return null;
        var buf = new byte[4];
        if (!ReadBytes(gdm + 0xa4, buf)) return null; // 32-Bit-Zahl: Spielzeit in ms
        int ms = BitConverter.ToInt32(buf, 0);
        return ms >= 0 ? ms : null;
    }

    /// <summary>Liest den kompletten Event-Flag-Block. null, wenn (noch) nicht verfügbar.</summary>
    /// <remarks>
    /// Der Block ist ~90 KB groß; jedes Bit ist ein Flag (1 = gesetzt, z. B. "Boss besiegt").
    /// Er wird bei jedem Durchlauf komplett gelesen und vom Tracker mit dem vorherigen verglichen.
    /// </remarks>
    public byte[]? ReadFlagBlock()
    {
        if (!IsAttached) return null;
        long p1 = ReadInt64(_eventFlagsStatic);       // globaler Zeiger → EventFlag-Manager
        if (p1 == 0) return null;
        long p2 = ReadInt64(p1);                      // Manager → Beginn des Flag-Blocks
        if (p2 == 0) return null;
        var buf = new byte[FlagBlockSize];
        return ReadBytes(p2, buf) ? buf : null;
    }

    /// <summary>Trennt die Verbindung (Handle schließen, alles zurücksetzen) und setzt den Status-Text.</summary>
    public void Detach(string status)
    {
        if (_handle != IntPtr.Zero) Native.CloseHandle(_handle);
        _handle = IntPtr.Zero;
        _process?.Dispose();
        _process = null;
        _eventFlagsStatic = 0;
        _worldChrStatic = 0;
        _gameDataManStatic = 0;
        Status = status;
    }

    public void Dispose() => Detach("Beendet");

    // ---------------------------------------------------------------- Speicher lesen

    /// <summary>Liest 8 Bytes (einen Zeiger) an einer Adresse im Spiel. 0, wenn nicht lesbar.</summary>
    private long ReadInt64(long address)
    {
        var buf = new byte[8];
        return ReadBytes(address, buf) ? BitConverter.ToInt64(buf, 0) : 0;
    }

    /// <summary>Liest genau buffer.Length Bytes. Nur true, wenn wirklich alles gelesen wurde.</summary>
    private bool ReadBytes(long address, byte[] buffer)
    {
        if (_handle == IntPtr.Zero || address == 0) return false;
        return Native.ReadProcessMemory(_handle, new IntPtr(address), buffer, new IntPtr(buffer.Length), out var read)
               && read.ToInt64() == buffer.Length;
    }

    /// <summary>Kopiert das geladene Hauptmodul des Spiels in 64-KB-Stücken, um darin Signaturen zu suchen.</summary>
    private byte[] ReadModuleImage(long baseAddr, int size)
    {
        var image = new byte[size];
        const int chunk = 0x10000;
        var tmp = new byte[chunk];
        for (int off = 0; off < size; off += chunk)
        {
            int len = Math.Min(chunk, size - off);
            if (len != chunk) tmp = new byte[len];      // letztes, kürzeres Stück
            if (ReadBytes(baseAddr + off, tmp))
                Buffer.BlockCopy(tmp, 0, image, off, len);
            // nicht lesbare Seiten bleiben 0
        }
        return image;
    }

    /// <summary>
    /// Sucht eine Signatur und rechnet die darin enthaltene RIP-relative Adresse aus.
    /// </summary>
    /// <remarks>
    /// Beispiel "48 8B 0D xx xx xx xx" = mov rcx, [rip + xx]. Die Zieladresse ist
    /// Adresse des Befehls + Länge des Befehls (7) + Abstand (die 4 Bytes ab Position 3).
    /// Ergebnis ist die Adresse des globalen Zeigers im Spiel, oder 0 wenn nicht gefunden.
    /// </remarks>
    private static long ScanRelative(byte[] image, long baseAddr, string pattern, int addressOffset, int instructionSize)
    {
        var (bytes, mask) = ParsePattern(pattern);
        int idx = FindPattern(image, bytes, mask);
        if (idx < 0) return 0;
        int rel = BitConverter.ToInt32(image, idx + addressOffset);
        return baseAddr + idx + instructionSize + rel;
    }

    /// <summary>Wandelt "48 8B ? 0D" in Bytes + Maske um (Maske false = "?", beliebiges Byte).</summary>
    private static (byte[] bytes, bool[] mask) ParsePattern(string pattern)
    {
        var parts = pattern.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var bytes = new byte[parts.Length];
        var mask = new bool[parts.Length];
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i] == "?" || parts[i] == "??") continue;
            bytes[i] = Convert.ToByte(parts[i], 16);
            mask[i] = true;
        }
        return (bytes, mask);
    }

    /// <summary>Einfache Mustersuche: erste Position, an der alle festen Bytes passen. -1 = nicht gefunden.</summary>
    private static int FindPattern(byte[] data, byte[] pattern, bool[] mask)
    {
        int last = data.Length - pattern.Length;
        byte first = pattern[0];                       // das erste Byte ist bei allen Signaturen fest
        for (int i = 0; i <= last; i++)
        {
            if (data[i] != first) continue;            // schneller Vorfilter
            int j = 1;
            for (; j < pattern.Length; j++)
            {
                if (mask[j] && data[i + j] != pattern[j]) break;
            }
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    /// <summary>Die drei benutzten Windows-Funktionen. Mehr wird aus kernel32 nicht importiert.</summary>
    private static class Native
    {
        public const uint PROCESS_VM_READ = 0x0010;                    // Speicher lesen
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;  // eingeschränkte Prozessinfo

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, IntPtr size, out IntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}

/// <summary>Umrechnung zwischen DS1-Event-Flag-IDs und Positionen im Flag-Block.</summary>
/// <remarks>
/// Eine Flag-ID hat 8 Ziffern: G AAA S NNN
///   G   = Gruppe (0, 1, 5, 6 oder 7) → Startposition der Gruppe im Block
///   AAA = Gebietscode (z. B. 100, 181) → Index in der Gebietsliste, je Gebiet 0x500 Bytes
///   S   = Abschnitt 0–9, je Abschnitt 128 Bytes
///   NNN = Nummer 0–999 → (NNN / 32) wählt das 4-Byte-Wort, (NNN % 32) das Bit darin
/// Beispiel: 11810900 → Gruppe 1, Gebiet 181, Abschnitt 0, Nummer 900.
/// Innerhalb eines Worts zählt das Spiel die Bits vom höchsten Bit aus (daher 0x80000000 >> n).
/// </remarks>
public static class EventFlags
{
    // Gruppe → Byte-Position, an der ihr Bereich im Flag-Block beginnt
    private static readonly (char Group, int Offset)[] Groups =
    {
        ('0', 0x00000), ('1', 0x00500), ('5', 0x05F00), ('6', 0x0B900), ('7', 0x11300),
    };

    // Gebietscodes in der Reihenfolge, in der das Spiel sie ablegt (Index = Position × 0x500)
    private static readonly string[] Areas =
    {
        "000", "100", "101", "102", "110", "120", "121", "130", "131",
        "132", "140", "141", "150", "151", "160", "170", "180", "181",
    };

    /// <summary>Rechnet eine Flag-ID in Byte-Position und Bitmaske um. false bei ungültiger ID.</summary>
    public static bool TryGetOffset(uint flagId, out int offset, out uint mask)
    {
        offset = 0; mask = 0;
        var s = flagId.ToString("D8");                 // immer 8 Stellen, mit führenden Nullen
        if (s.Length != 8) return false;
        int g = Array.FindIndex(Groups, x => x.Group == s[0]);
        int a = Array.IndexOf(Areas, s.Substring(1, 3));
        if (g < 0 || a < 0) return false;
        int section = s[4] - '0';
        int number = int.Parse(s.Substring(5, 3));
        offset = Groups[g].Offset + a * 0x500 + section * 128 + (number / 32) * 4;
        mask = 0x80000000u >> (number % 32);
        return offset + 4 <= GameReader.FlagBlockSize; // darf nicht hinter das Blockende zeigen
    }

    /// <summary>Ist das Flag im übergebenen Flag-Block gesetzt?</summary>
    public static bool IsSet(byte[] block, uint flagId)
    {
        if (!TryGetOffset(flagId, out int off, out uint mask)) return false;
        return (BitConverter.ToUInt32(block, off) & mask) != 0;
    }

    /// <summary>Umkehrung: Byte-Offset (4er-Wort) + Bit → Flag-ID. null, wenn keine gültige ID.</summary>
    /// <remarks>Wird für den Flag-Monitor gebraucht: geänderte Bits → lesbare Flag-Nummer.</remarks>
    public static uint? FromPosition(int wordOffset, int bit)
    {
        // Gruppe suchen: die letzte, deren Startposition nicht hinter dem Offset liegt
        int g = -1;
        for (int i = Groups.Length - 1; i >= 0; i--)
        {
            if (wordOffset >= Groups[i].Offset) { g = i; break; }
        }
        if (g < 0) return null;
        int rem = wordOffset - Groups[g].Offset;
        int a = rem / 0x500;
        if (a >= Areas.Length) return null;
        if (Groups[g].Group == '0' && a > 0) return null;  // Gruppe 0 hat nur ein Gebiet
        int rem2 = rem % 0x500;
        int section = rem2 / 128;
        int number = (rem2 % 128) / 4 * 32 + bit;
        if (section > 9 || number > 999) return null;
        var s = $"{Groups[g].Group}{Areas[a]}{section}{number:D3}";
        return uint.Parse(s);
    }
}
