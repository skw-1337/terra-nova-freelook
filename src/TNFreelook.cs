// ============================================================================
//  Terra Nova: Strike Force Centauri - Mouse Freelook
//  External helper for the DOS game running in DOSBox (Steam, GOG, standalone).
//
//  - Finds the game inside any DOSBox-family emulator (DOSBox, DOSBox Staging,
//    DOSBox-X...) by CODE SIGNATURES: no hard-coded addresses, so it works with
//    the English and French executables and any DOSBox memory setting.
//  - In a mission, press the toggle key (default: Y) : mouse X turns, mouse Y
//    looks up/down, the aiming reticle stays centred (you fire where you look).
//  - Only writes to fields identified from the game's own code (body heading,
//    head pitch, mouse cursor). Automatically switches off outside the 3D view.
//
//  v1.1: no more periodic heading nudge (the view no longer shakes when standing
//    still), 1 ms timer (smoother), game cursor frozen while active (no reticle
//    trails on the cockpit), O / Esc switch it off, pointer kept inside the game
//    window (dual screens).
//
//  Build (no install needed, uses the C# compiler shipped with Windows):
//    C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /optimize
//        /out:TNFreelook.exe TNFreelook.cs
// ============================================================================
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

static class TNFreelook
{
    const string VERSION = "1.1";

    // ------------------------------------------------------------------ settings
    static int SensX = 12;          // heading units per mouse count (65536 = 360 deg)
    static int SensY = 8;           // pitch units per mouse count
    static bool InvertY = false;
    static int ToggleScan = 0x15;   // physical key position (scancode). 0x15 = Y on QWERTY/AZERTY
    static bool Sound = true;

    // limits used by the game itself for the head pitch
    const int PITCH_MIN = -7187, PITCH_MAX = 6127;
    const double CENTRE_Y = 0.36;   // centre of the 3D view = 36% of the cursor frame height
    const int NUDGE = 32;           // activation test heading offset (camera ignores < 16)
    const double BLIND = 1.5;       // mouse turned but camera frozen this long -> not in the 3D view

    // ------------------------------------------------------------------ signatures
    // "(....)" = captured absolute address, "...." = wildcard. Literal bytes are hex.
    static readonly string[][] SIGS = {
        new[]{ "camera",  "8b15 (....) c1fa10 8d04d500000000 01d0 c1e002 01d0 05 (....) 668b15 (....) 668b401b 01d0 66a3 (....) 66a1 (....) 66a3 (....)" },
        new[]{ "haze",    "5231d2 8a82 (....) 3a82 (....) 740f 68 (....) 6a01 e8" },
        new[]{ "mouse",   "84d2 0f85 .... 803d (....) 00 7423 8b15 (....) a1 (....) c1fa10" },
        new[]{ "draw",    "8b15 (....) ff15 .... 30c9 880d ...." },
        new[]{ "frame",   "8b0424 8b15 (....) a3 (....) a1 ...." },
        // mouse event handler: "cursor frozen" flag (motion events ignored, reticle not redrawn)
        new[]{ "freeze",  "f6400401 0f84 .... 803d (....) 00 0f85 .... c605 .... 01" },
    };
    static readonly byte[] HAZE_TEXT = Encoding.ASCII.GetBytes("Memory trash: hazeRadius");

    // ------------------------------------------------------------------ Win32
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr read);
    [DllImport("kernel32.dll", SetLastError = true)] static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, IntPtr size, out IntPtr written);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr VirtualQueryEx(IntPtr h, IntPtr addr, out MBI info, IntPtr len);
    [StructLayout(LayoutKind.Sequential)]
    struct MBI { public IntPtr BaseAddress, AllocationBase; public uint AllocationProtect; public UIntPtr RegionSize; public uint State, Protect, Type; }

    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint type);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr hwnd, out RECT r);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr hwnd, ref POINT p);
    [DllImport("user32.dll")] static extern bool ClipCursor(ref RECT r);
    [DllImport("user32.dll", EntryPoint = "ClipCursor")] static extern bool ClipCursorOff(IntPtr none);
    [DllImport("winmm.dll")] static extern uint timeBeginPeriod(uint ms);
    [DllImport("kernel32.dll")] static extern bool SetConsoleCtrlHandler(CtrlHandler h, bool add);
    delegate bool CtrlHandler(int ev);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out int pid);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern IntPtr CreateWindowEx(uint exStyle, string cls, string name, uint style, int x, int y, int w, int h, IntPtr parent, IntPtr menu, IntPtr inst, IntPtr param);
    [DllImport("user32.dll")] static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devs, uint n, uint size);
    [DllImport("user32.dll")] static extern bool PeekMessage(out MSG msg, IntPtr hwnd, uint min, uint max, uint remove);
    [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern IntPtr DispatchMessage(ref MSG msg);
    [DllImport("user32.dll")] static extern uint GetRawInputData(IntPtr hRaw, uint cmd, byte[] data, ref uint size, uint headerSize);
    [StructLayout(LayoutKind.Sequential)] struct RAWINPUTDEVICE { public ushort UsagePage, Usage; public uint Flags; public IntPtr Target; }
    [StructLayout(LayoutKind.Sequential)] struct MSG { public IntPtr hwnd; public uint message; public IntPtr wParam, lParam; public uint time; public int x, y; }

    // ------------------------------------------------------------------ game state
    static IntPtr hProc = IntPtr.Zero;
    static int gamePid = 0;
    static long guestBase;          // host address of guest linear address 0
    static uint aHazeText, aPlayerId, aMaster, aCamHeading, aPitch, aWarp, aCursor, aDraw, aFrame, aFreeze;
    static bool frozen = false, clipped = false;
    static CtrlHandler onClose;     // kept alive: releases the cursor if the window is closed

    static void Main(string[] args)
    {
        Console.Title = "Terra Nova Freelook " + VERSION;
        LoadSettings();
        Console.WriteLine("Terra Nova: Strike Force Centauri - Mouse Freelook " + VERSION);
        Console.WriteLine("Toggle key: " + KeyName() + "   (settings: TNFreelook.ini)");
        Console.WriteLine("Leave this window open. Start the game, enter a mission, press the toggle key.");
        Console.WriteLine("Close it when you are done playing (it edits DOSBox's memory like a trainer:");
        Console.WriteLine("quit it before playing online games protected by an anti-cheat).");
        Console.WriteLine();
        int vkToggle = (int)MapVirtualKey((uint)ToggleScan, 1);
        int vkOptions = (int)MapVirtualKey(0x18, 1);   // O (options screen), same place on QWERTY/AZERTY
        const int VK_ESCAPE = 0x1B;
        RawMouse mouse = new RawMouse();
        timeBeginPeriod(1);                          // 1-2 ms sleeps instead of ~15.6 ms: smoother
        onClose = ev => { Release(); return false; };
        SetConsoleCtrlHandler(onClose, true);

        bool on = false, prevKey = false;
        uint entry = 0;
        double lastCheck = 0, lastMission = 0, pendingT = 0;
        int pending = 0;                             // heading written since the camera last moved
        byte[] camPrev = null;
        Stopwatch clock = Stopwatch.StartNew();

        while (true)
        {
            mouse.Pump();
            double now = clock.Elapsed.TotalSeconds;
            try
            {
                if (hProc == IntPtr.Zero || !GameStillLoaded())
                {
                    if (on) { on = false; Say("Freelook OFF (game closed)", 500); }
                    frozen = false; Unclip();
                    if (now - lastCheck > 2) { lastCheck = now; TryAttach(); }
                    mouse.Take(); Thread.Sleep(50); continue;
                }
                bool fg = ForegroundIsGame();
                bool key = fg && (GetAsyncKeyState(vkToggle) & 0x8000) != 0;
                if (key && !prevKey)
                {
                    if (on) { on = false; Say("Freelook OFF", 500); }
                    else
                    {
                        entry = PlayerEntry();
                        // one-time check that the 3D view is live (tiny heading nudge, undone)
                        if (InMission(entry) && CameraAlive(entry))
                        {
                            on = true; mouse.Take();
                            camPrev = Read(aCamHeading, 2); pending = 0; lastMission = now;
                            Say("Freelook ON", 1200);
                        }
                        else Say("Not in a mission (3D view) - freelook refused", 300, 300);
                    }
                }
                prevKey = key;

                if (on)
                {
                    // passive safety (no more periodic nudge: it made the view shake)
                    if (fg && ((GetAsyncKeyState(vkOptions) & 0x8000) != 0 || (GetAsyncKeyState(VK_ESCAPE) & 0x8000) != 0))
                    { on = false; Say("Freelook OFF (menu)", 500); }
                    else if (now - lastMission > 0.25)
                    {
                        lastMission = now;
                        if (!InMission(entry)) { on = false; Say("Freelook OFF (mission ended)", 500); }
                    }
                    if (on)
                    {
                        byte[] cam = Read(aCamHeading, 2);
                        if (cam[0] != camPrev[0] || cam[1] != camPrev[1]) { camPrev = cam; pending = 0; }
                        else if (pending != 0 && now - pendingT > BLIND && Math.Abs(pending) > 64)
                        {   // we kept turning but the 3D camera did not follow: not in the 3D view
                            AddHeading(entry, -pending); pending = 0;
                            on = false; Say("Freelook OFF (left the 3D view)", 500);
                        }
                    }
                }

                int dx, dy; mouse.Take(out dx, out dy);
                if (on && fg)
                {
                    if (dx != 0)
                    {
                        AddHeading(entry, dx * SensX);
                        if (pending == 0) pendingT = now;
                        pending += dx * SensX;
                    }
                    if (dy != 0)
                    {
                        int p = BitConverter.ToInt16(Read(aPitch, 2), 0) + (InvertY ? -dy : dy) * SensY;
                        p = Math.Max(PITCH_MIN, Math.Min(PITCH_MAX, p));
                        Write(aPitch, BitConverter.GetBytes((short)p));
                    }
                    byte[] fr = Read(aFrame, 4);
                    int w = BitConverter.ToInt16(fr, 0), h = BitConverter.ToInt16(fr, 2);
                    if (w >= 200 && w <= 640 && h >= 200 && h <= 480)
                    {
                        byte[] c = new byte[4];
                        BitConverter.GetBytes((short)(w / 2)).CopyTo(c, 0);
                        BitConverter.GetBytes((short)Math.Round(h * CENTRE_Y)).CopyTo(c, 2);
                        Write(aCursor, c);           // cursor used for aiming
                        Write(aDraw, c);             // where the reticle is drawn
                        Write(aWarp, new byte[] { 1 }); // mouse lib: push cursor to driver, skip reading the mouse
                    }
                    // freeze the game cursor: its interrupt handler no longer moves/redraws the
                    // reticle between two re-centrings (that left trails on the cockpit).
                    // Clicks still work (queued before this test). Re-written: the game may clear it.
                    Write(aFreeze, new byte[] { 1 }); frozen = true;
                    Clip();                          // keep the Windows pointer inside the game window
                }
                else
                {
                    if (frozen) { Write(aFreeze, new byte[] { 0 }); frozen = false; }
                    Unclip();
                }
            }
            catch (Exception e)
            {
                if (on) Say("Freelook OFF (" + e.Message + ")", 500);
                on = false; frozen = false; Unclip(); Detach(); Thread.Sleep(500);
            }
            Thread.Sleep(2);
        }
    }

    // ------------------------------------------------------------------ cursor helpers
    static void Clip()
    {
        IntPtr hwnd = GetForegroundWindow();       // the game window (checked by the caller)
        RECT r; POINT p = new POINT();
        if (!GetClientRect(hwnd, out r) || !ClientToScreen(hwnd, ref p)) return;
        RECT zone = new RECT { Left = p.X, Top = p.Y, Right = p.X + r.Right, Bottom = p.Y + r.Bottom };
        ClipCursor(ref zone); clipped = true;      // re-applied every loop (Windows may drop it)
    }

    static void Unclip()
    {
        if (clipped) { ClipCursorOff(IntPtr.Zero); clipped = false; }
    }

    static void Release()
    {
        try { if (frozen && hProc != IntPtr.Zero) Write(aFreeze, new byte[] { 0 }); } catch { }
        frozen = false; ClipCursorOff(IntPtr.Zero); clipped = false;
    }

    // ------------------------------------------------------------------ game helpers
    static uint PlayerEntry()
    {
        ushort id = BitConverter.ToUInt16(Read(aPlayerId + 2, 2), 0);
        return aMaster + (uint)id * 0x25;
    }

    static bool InMission(uint entry)
    {
        double x = BitConverter.ToInt32(Read(entry + 0x0f, 4), 0) / 65536.0;
        double y = BitConverter.ToInt32(Read(entry + 0x13, 4), 0) / 65536.0;
        return x >= 2 && x <= 510 && y >= 2 && y <= 510;    // 0 outside a mission
    }

    static void AddHeading(uint entry, int delta)
    {
        ushort cap = BitConverter.ToUInt16(Read(entry + 0x1b, 2), 0);
        Write(entry + 0x1b, BitConverter.GetBytes((ushort)((cap + delta) & 0xFFFF)));
    }

    static bool CameraAlive(uint entry)
    {
        byte[] cam0 = Read(aCamHeading, 2);
        AddHeading(entry, NUDGE);
        bool ok = false;
        Stopwatch t = Stopwatch.StartNew();
        while (t.Elapsed.TotalSeconds < 1.0)
        {
            Thread.Sleep(10);
            byte[] cam = Read(aCamHeading, 2);
            if (cam[0] != cam0[0] || cam[1] != cam0[1]) { ok = true; break; }
        }
        AddHeading(entry, -NUDGE);
        return ok;
    }

    static bool GameStillLoaded()
    {
        try
        {
            Process p = Process.GetProcessById(gamePid);
            if (p.HasExited) { Detach(); return false; }
            byte[] b = Read(aHazeText, HAZE_TEXT.Length);
            for (int i = 0; i < b.Length; i++) if (b[i] != HAZE_TEXT[i]) { Detach(); return false; }
            return true;
        }
        catch { Detach(); return false; }
    }

    static bool ForegroundIsGame()
    {
        int pid; GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        return pid == gamePid;
    }

    // ------------------------------------------------------------------ attach / signature scan
    static void TryAttach()
    {
        foreach (Process p in Process.GetProcesses())
        {
            string n = p.ProcessName.ToLowerInvariant();
            if (!n.Contains("dosbox")) continue;
            IntPtr h = OpenProcess(0x0010 | 0x0020 | 0x0008 | 0x0400, false, p.Id);
            if (h == IntPtr.Zero) continue;
            hProc = h; gamePid = p.Id;
            if (Scan()) { Say("Game found in " + p.ProcessName + " (pid " + p.Id + ")", 0); return; }
            CloseHandle(h); hProc = IntPtr.Zero; gamePid = 0;
        }
    }

    static void Detach()
    {
        if (hProc != IntPtr.Zero) CloseHandle(hProc);
        hProc = IntPtr.Zero; gamePid = 0;
    }

    static Regex Sig(string s)
    {
        StringBuilder sb = new StringBuilder();
        foreach (string tok in s.Split(' '))
        {
            if (tok == "(....)") sb.Append("(....)");
            else if (tok == "....") sb.Append("....");
            else for (int i = 0; i < tok.Length; i += 2)
                    sb.Append(Regex.Escape(((char)Convert.ToByte(tok.Substring(i, 2), 16)).ToString()));
        }
        return new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant);
    }

    static string Latin1(byte[] b) { return Encoding.GetEncoding(28591).GetString(b); }

    static uint Cap(Match m, int i)
    {
        string g = m.Groups[i].Value;
        return (uint)(g[0] | (g[1] << 8) | (g[2] << 16) | (g[3] << 24));
    }

    // best = highest captured addresses (relocated code, not a raw copy of the exe left in memory)
    static Match Best(Regex r, string mem)
    {
        Match best = null; uint bestMin = 0;
        for (Match m = r.Match(mem); m.Success; m = m.NextMatch())
        {
            uint mn = uint.MaxValue;
            for (int i = 1; i < m.Groups.Count; i++) mn = Math.Min(mn, Cap(m, i));
            if (best == null || mn > bestMin) { best = m; bestMin = mn; }
        }
        return best;
    }

    static bool Scan()
    {
        // candidate guest RAM: large committed read/write private or mapped regions
        long addr = 0;
        MBI mbi;
        while (VirtualQueryEx(hProc, new IntPtr(addr), out mbi, new IntPtr(Marshal.SizeOf(typeof(MBI)))) != IntPtr.Zero)
        {
            long size = (long)mbi.RegionSize.ToUInt64();
            long start = mbi.BaseAddress.ToInt64();
            bool rw = (mbi.Protect & 0x04) != 0 || (mbi.Protect & 0x40) != 0;
            if (mbi.State == 0x1000 && rw && size >= 4L << 20 && size <= 1L << 30)
            {
                if (ScanRegion(start, size)) return true;
            }
            addr = start + size;
            if (addr <= start) break;
        }
        return false;
    }

    static bool ScanRegion(long start, long size)
    {
        byte[] buf = RawRead(start, (int)size);
        if (buf == null) return false;
        string mem = Latin1(buf);
        Regex haze = Sig(SIGS[1][1]);
        for (Match m = haze.Match(mem); m.Success; m = m.NextMatch())
        {
            uint strGuest = Cap(m, 3);
            if (strGuest < 0x100000) continue;                 // raw exe copy: not relocated
            // locate the text in this region -> host address of guest 0
            int idx = IndexOf(buf, HAZE_TEXT, 0);
            while (idx >= 0)
            {
                long baseCand = start + idx - strGuest;
                if (baseCand >= start - 0x100000 && PlausibleIvt(baseCand) &&
                    ResolveAll(baseCand, start, buf, mem, strGuest))
                    return true;
                idx = IndexOf(buf, HAZE_TEXT, idx + 1);
            }
        }
        return false;
    }

    // DOSBox fills the real-mode interrupt table with F000:xxxx (BIOS) vectors
    static bool PlausibleIvt(long baseHost)
    {
        byte[] ivt = RawRead(baseHost, 1024);
        if (ivt == null) return false;
        int bios = 0;
        for (int i = 0; i < 256; i++) if (BitConverter.ToUInt16(ivt, i * 4 + 2) == 0xF000) bios++;
        return bios >= 32;
    }

    static bool ResolveAll(long baseHost, long regStart, byte[] buf, string mem, uint strGuest)
    {
        guestBase = baseHost;
        Match cam = Best(Sig(SIGS[0][1]), mem);
        Match mou = Best(Sig(SIGS[2][1]), mem);
        Match drw = Best(Sig(SIGS[3][1]), mem);
        Match frm = Best(Sig(SIGS[4][1]), mem);
        Match frz = Best(Sig(SIGS[5][1]), mem);
        if (cam == null || mou == null || drw == null || frm == null || frz == null) return false;
        aHazeText = strGuest;
        aPlayerId = Cap(cam, 1); aMaster = Cap(cam, 2); aCamHeading = Cap(cam, 4); aPitch = Cap(cam, 5);
        aWarp = Cap(mou, 1); aCursor = Cap(mou, 2);
        aDraw = Cap(drw, 1);
        aFrame = Cap(frm, 2);
        aFreeze = Cap(frz, 1);
        if (aFreeze != aWarp - 1) return false;      // same layout in every known version
        // sanity: every address must lie inside this memory region
        foreach (uint a in new[] { aPlayerId, aMaster, aCamHeading, aPitch, aWarp, aCursor, aDraw, aFrame, aFreeze })
        {
            long host = guestBase + a;
            if (host < regStart || host + 64 > regStart + buf.Length) return false;
        }
        return true;
    }

    static int IndexOf(byte[] hay, byte[] needle, int from)
    {
        for (int i = from; i <= hay.Length - needle.Length; i++)
        {
            if (hay[i] != needle[0]) continue;
            int j = 1;
            while (j < needle.Length && hay[i + j] == needle[j]) j++;
            if (j == needle.Length) return i;
        }
        return -1;
    }

    // ------------------------------------------------------------------ memory
    static byte[] RawRead(long host, int n)
    {
        byte[] b = new byte[n];
        IntPtr got;
        if (!ReadProcessMemory(hProc, new IntPtr(host), b, new IntPtr(n), out got) || got.ToInt64() != n) return null;
        return b;
    }

    static byte[] Read(uint guest, int n)
    {
        byte[] b = RawRead(guestBase + guest, n);
        if (b == null) throw new Exception("read failed");
        return b;
    }

    static void Write(uint guest, byte[] data)
    {
        IntPtr w;
        if (!WriteProcessMemory(hProc, new IntPtr(guestBase + guest), data, new IntPtr(data.Length), out w))
            throw new Exception("write failed");
    }

    // ------------------------------------------------------------------ raw mouse
    class RawMouse
    {
        IntPtr hwnd;
        int dx, dy;
        byte[] buf = new byte[64];

        public RawMouse()
        {
            hwnd = CreateWindowEx(0, "STATIC", "TNFreelookRaw", 0, 0, 0, 0, 0, new IntPtr(-3), IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
            RAWINPUTDEVICE[] d = { new RAWINPUTDEVICE { UsagePage = 1, Usage = 2, Flags = 0x100, Target = hwnd } };  // RIDEV_INPUTSINK
            if (!RegisterRawInputDevices(d, 1, (uint)Marshal.SizeOf(typeof(RAWINPUTDEVICE))))
                throw new Exception("RegisterRawInputDevices failed");
        }

        public void Pump()
        {
            MSG m;
            int header = IntPtr.Size == 8 ? 24 : 16;
            while (PeekMessage(out m, hwnd, 0, 0, 1))
            {
                if (m.message == 0x00FF)          // WM_INPUT
                {
                    uint size = (uint)buf.Length;
                    if (GetRawInputData(m.lParam, 0x10000003, buf, ref size, (uint)header) != 0xFFFFFFFF
                        && BitConverter.ToUInt32(buf, 0) == 0)                  // RIM_TYPEMOUSE
                    {
                        ushort flags = BitConverter.ToUInt16(buf, header);
                        if ((flags & 1) == 0)                                   // relative movement
                        {
                            dx += BitConverter.ToInt32(buf, header + 12);
                            dy += BitConverter.ToInt32(buf, header + 16);
                        }
                    }
                }
                TranslateMessage(ref m);
                DispatchMessage(ref m);
            }
        }

        public void Take() { dx = dy = 0; }
        public void Take(out int x, out int y) { x = dx; y = dy; dx = dy = 0; }
    }

    // ------------------------------------------------------------------ misc
    static void Say(string msg, int freq, int dur = 100)
    {
        Console.WriteLine(DateTime.Now.ToString("HH:mm:ss") + "  " + msg);
        if (Sound && freq > 0) { try { Console.Beep(freq, dur); } catch { } }
    }

    static string KeyName()
    {
        uint vk = MapVirtualKey((uint)ToggleScan, 1);
        return vk >= 0x30 && vk <= 0x5A ? ((char)vk).ToString() : "scancode 0x" + ToggleScan.ToString("X2");
    }

    static void LoadSettings()
    {
        string ini = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TNFreelook.ini");
        if (!File.Exists(ini))
        {
            File.WriteAllText(ini,
                "; Terra Nova Freelook settings\r\n" +
                "; mouse sensitivity (heading / pitch units per mouse count)\r\n" +
                "sensitivity_x = 12\r\nsensitivity_y = 8\r\n" +
                "; 1 = invert vertical look\r\ninvert_y = 0\r\n" +
                "; toggle key as a PHYSICAL key scancode (hex). 15 = Y (QWERTY/AZERTY), 2C = Z on QWERTZ,\r\n" +
                "; 29 = key left of 1, 3B..44 = F1..F10, 57/58 = F11/F12\r\ntoggle_scancode = 15\r\n" +
                "; 0 = no beeps\r\nsound = 1\r\n");
            return;
        }
        foreach (string line in File.ReadAllLines(ini))
        {
            string l = line.Trim();
            if (l.StartsWith(";") || !l.Contains("=")) continue;
            string k = l.Substring(0, l.IndexOf('=')).Trim().ToLowerInvariant();
            string v = l.Substring(l.IndexOf('=') + 1).Trim();
            try
            {
                if (k == "sensitivity_x") SensX = int.Parse(v);
                else if (k == "sensitivity_y") SensY = int.Parse(v);
                else if (k == "invert_y") InvertY = v == "1";
                else if (k == "toggle_scancode") ToggleScan = Convert.ToInt32(v, 16);
                else if (k == "sound") Sound = v != "0";
            }
            catch { Console.WriteLine("Ignored bad setting: " + l); }
        }
    }
}
