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
    private const string ProcessName = "DarkSoulsRemastered";

    // Gesamtgröße des Event-Flag-Blocks (Gruppe 7 beginnt bei 0x11300, 18 Gebiete à 0x500)
    public const int FlagBlockSize = 0x11300 + 18 * 0x500;

    private const string EventFlagsPattern = "48 8B 0D ? ? ? ? 99 33 C2 45 33 C0 2B C2 8D 50 F6";
    private const string WorldChrManPattern = "48 8b 0d ? ? ? ? 0f 28 f1 48 85 c9 74 ? 48 89 7c";
    private const string GameDataManPattern = "48 8b 05 ? ? ? ? 48 8b 50 10 48 89 54 24 60";

    private Process? _process;
    private IntPtr _handle = IntPtr.Zero;
    private long _eventFlagsStatic;
    private long _worldChrStatic;
    private long _gameDataManStatic;
    private DateTime _nextAttachAttempt = DateTime.MinValue;
    private int _failedScans;

    /// <summary>Dateiversion der gefundenen DarkSoulsRemastered.exe (nur zur Anzeige/Diagnose).</summary>
    public string? GameVersion { get; private set; }

    public bool IsAttached => _process != null && _handle != IntPtr.Zero && _eventFlagsStatic != 0;
    public string Status { get; private set; } = "Suche DarkSoulsRemastered.exe …";

    /// <summary>Verbindet sich mit dem Spiel, falls noch nicht geschehen. Gibt true zurück, wenn verbunden.</summary>
    public bool EnsureAttached()
    {
        if (_process != null)
        {
            bool exited;
            try { exited = _process.HasExited; } catch { exited = true; }
            if (exited)
            {
                Detach("Spiel wurde beendet – warte auf DarkSoulsRemastered.exe …");
            }
        }

        if (IsAttached) return true;
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
                GameVersion = FileVersionInfo.GetVersionInfo(exe).FileVersion;
            }
            catch (Exception ex)
            {
                Detach($"Spiel gefunden, aber Zugriff auf die Prozessinfo nicht möglich ({ex.Message}). Läuft das Spiel unter einem anderen Benutzer?");
                return false;
            }
            _handle = Native.OpenProcess(Native.PROCESS_VM_READ | Native.PROCESS_QUERY_LIMITED_INFORMATION, false, _process.Id);
            if (_handle == IntPtr.Zero)
            {
                Detach($"Konnte das Spiel nicht zum Lesen öffnen (Fehler {Marshal.GetLastWin32Error()}). Spiel und Tracker sollten unter demselben Benutzer und ohne Administratorrechte laufen.");
                return false;
            }
            _failedScans = 0;
        }

        // Signaturen suchen (kann direkt nach Spielstart noch fehlschlagen → später erneut)
        try
        {
            var module = _process.MainModule!;
            long baseAddr = module.BaseAddress.ToInt64();
            int size = module.ModuleMemorySize;
            var image = ReadModuleImage(baseAddr, size);

            _eventFlagsStatic = ScanRelative(image, baseAddr, EventFlagsPattern, 3, 7);
            _worldChrStatic = ScanRelative(image, baseAddr, WorldChrManPattern, 3, 7);
            _gameDataManStatic = ScanRelative(image, baseAddr, GameDataManPattern, 3, 7); // optional (Spielzeit)

            if (_eventFlagsStatic == 0 || _worldChrStatic == 0)
            {
                _eventFlagsStatic = 0;
                // direkt nach Spielstart normal; bleibt es dauerhaft so, passt die Spielversion vermutlich nicht
                Status = ++_failedScans < 15
                    ? "Spiel gefunden, Speicherstruktur noch nicht bereit – versuche erneut …"
                    : $"Speicherstruktur nicht gefunden – evtl. nicht unterstützte Spielversion ({GameVersion ?? "unbekannt"}). Getestet mit Dark Souls Remastered 1.03.1 (Steam).";
                return false;
            }

            Status = "Verbunden mit Dark Souls Remastered" + (GameVersion != null ? $" ({GameVersion})" : "");
            return true;
        }
        catch (Exception ex)
        {
            Detach($"Fehler beim Verbinden: {ex.Message}");
            return false;
        }
    }

    /// <summary>True, wenn ein Charakter geladen ist (nicht im Hauptmenü / Ladebildschirm).</summary>
    public bool IsPlayerLoaded()
    {
        if (!IsAttached) return false;
        long wcm = ReadInt64(_worldChrStatic);
        if (wcm == 0) return false;
        long playerIns = ReadInt64(wcm + 0x68);
        return playerIns != 0;
    }

    /// <summary>Spielerposition (Weltkoordinaten), null wenn nicht verfügbar.</summary>
    public (float X, float Y, float Z)? ReadPlayerPosition()
    {
        if (!IsAttached) return null;
        long wcm = ReadInt64(_worldChrStatic);
        if (wcm == 0) return null;
        long playerIns = ReadInt64(wcm + 0x68);
        if (playerIns == 0) return null;
        long ctrl = ReadInt64(playerIns + 0x68);
        if (ctrl == 0) return null;
        long posObj = ReadInt64(ctrl + 0x28);
        if (posObj == 0) return null;
        var buf = new byte[12];
        if (!ReadBytes(posObj + 0x10, buf)) return null;
        float x = BitConverter.ToSingle(buf, 0), y = BitConverter.ToSingle(buf, 4), z = BitConverter.ToSingle(buf, 8);
        if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z)) return null;
        return (x, y, z);
    }

    /// <summary>Spielzeit (In-Game-Time) in Millisekunden, null wenn nicht verfügbar.</summary>
    public long? ReadIgtMs()
    {
        if (!IsAttached || _gameDataManStatic == 0) return null;
        long gdm = ReadInt64(_gameDataManStatic);
        if (gdm == 0) return null;
        var buf = new byte[4];
        if (!ReadBytes(gdm + 0xa4, buf)) return null;
        int ms = BitConverter.ToInt32(buf, 0);
        return ms >= 0 ? ms : null;
    }

    /// <summary>Liest den kompletten Event-Flag-Block. null, wenn (noch) nicht verfügbar.</summary>
    public byte[]? ReadFlagBlock()
    {
        if (!IsAttached) return null;
        long p1 = ReadInt64(_eventFlagsStatic);
        if (p1 == 0) return null;
        long p2 = ReadInt64(p1);
        if (p2 == 0) return null;
        var buf = new byte[FlagBlockSize];
        return ReadBytes(p2, buf) ? buf : null;
    }

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

    private long ReadInt64(long address)
    {
        var buf = new byte[8];
        return ReadBytes(address, buf) ? BitConverter.ToInt64(buf, 0) : 0;
    }

    private bool ReadBytes(long address, byte[] buffer)
    {
        if (_handle == IntPtr.Zero || address == 0) return false;
        return Native.ReadProcessMemory(_handle, new IntPtr(address), buffer, new IntPtr(buffer.Length), out var read)
               && read.ToInt64() == buffer.Length;
    }

    private byte[] ReadModuleImage(long baseAddr, int size)
    {
        var image = new byte[size];
        const int chunk = 0x10000;
        var tmp = new byte[chunk];
        for (int off = 0; off < size; off += chunk)
        {
            int len = Math.Min(chunk, size - off);
            if (len != chunk) tmp = new byte[len];
            if (ReadBytes(baseAddr + off, tmp))
                Buffer.BlockCopy(tmp, 0, image, off, len);
            // nicht lesbare Seiten bleiben 0
        }
        return image;
    }

    private static long ScanRelative(byte[] image, long baseAddr, string pattern, int addressOffset, int instructionSize)
    {
        var (bytes, mask) = ParsePattern(pattern);
        int idx = FindPattern(image, bytes, mask);
        if (idx < 0) return 0;
        int rel = BitConverter.ToInt32(image, idx + addressOffset);
        return baseAddr + idx + instructionSize + rel;
    }

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

    private static int FindPattern(byte[] data, byte[] pattern, bool[] mask)
    {
        int last = data.Length - pattern.Length;
        byte first = pattern[0];
        for (int i = 0; i <= last; i++)
        {
            if (data[i] != first) continue;
            int j = 1;
            for (; j < pattern.Length; j++)
            {
                if (mask[j] && data[i + j] != pattern[j]) break;
            }
            if (j == pattern.Length) return i;
        }
        return -1;
    }

    private static class Native
    {
        public const uint PROCESS_VM_READ = 0x0010;
        public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr OpenProcess(uint access, bool inheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadProcessMemory(IntPtr process, IntPtr baseAddress, byte[] buffer, IntPtr size, out IntPtr bytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr handle);
    }
}

/// <summary>Umrechnung zwischen DS1-Event-Flag-IDs und Positionen im Flag-Block.</summary>
public static class EventFlags
{
    private static readonly (char Group, int Offset)[] Groups =
    {
        ('0', 0x00000), ('1', 0x00500), ('5', 0x05F00), ('6', 0x0B900), ('7', 0x11300),
    };

    private static readonly string[] Areas =
    {
        "000", "100", "101", "102", "110", "120", "121", "130", "131",
        "132", "140", "141", "150", "151", "160", "170", "180", "181",
    };

    public static bool TryGetOffset(uint flagId, out int offset, out uint mask)
    {
        offset = 0; mask = 0;
        var s = flagId.ToString("D8");
        if (s.Length != 8) return false;
        int g = Array.FindIndex(Groups, x => x.Group == s[0]);
        int a = Array.IndexOf(Areas, s.Substring(1, 3));
        if (g < 0 || a < 0) return false;
        int section = s[4] - '0';
        int number = int.Parse(s.Substring(5, 3));
        offset = Groups[g].Offset + a * 0x500 + section * 128 + (number / 32) * 4;
        mask = 0x80000000u >> (number % 32);
        return offset + 4 <= GameReader.FlagBlockSize;
    }

    public static bool IsSet(byte[] block, uint flagId)
    {
        if (!TryGetOffset(flagId, out int off, out uint mask)) return false;
        return (BitConverter.ToUInt32(block, off) & mask) != 0;
    }

    /// <summary>Umkehrung: Byte-Offset (4er-Wort) + Bit → Flag-ID. null, wenn keine gültige ID.</summary>
    public static uint? FromPosition(int wordOffset, int bit)
    {
        int g = -1;
        for (int i = Groups.Length - 1; i >= 0; i--)
        {
            if (wordOffset >= Groups[i].Offset) { g = i; break; }
        }
        if (g < 0) return null;
        int rem = wordOffset - Groups[g].Offset;
        int a = rem / 0x500;
        if (a >= Areas.Length) return null;
        if (Groups[g].Group == '0' && a > 0) return null;
        int rem2 = rem % 0x500;
        int section = rem2 / 128;
        int number = (rem2 % 128) / 4 * 32 + bit;
        if (section > 9 || number > 999) return null;
        var s = $"{Groups[g].Group}{Areas[a]}{section}{number:D3}";
        return uint.Parse(s);
    }
}
