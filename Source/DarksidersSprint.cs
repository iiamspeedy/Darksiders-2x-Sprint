// Darksiders Warmastered hold-L3 sprint, build cs:2679. C# 5 / .NET Framework 4.8.
// No game files are edited. All code and data live in the running process.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;

static class Native {
    [DllImport("kernel32", SetLastError=true)] internal static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr read);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool WriteProcessMemory(IntPtr h, IntPtr address, byte[] bytes, UIntPtr size, out UIntPtr written);
    [DllImport("kernel32", SetLastError=true)] internal static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr address, UIntPtr size, uint allocation, uint protect);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool VirtualProtectEx(IntPtr h, IntPtr address, UIntPtr size, uint protect, out uint old);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool FlushInstructionCache(IntPtr h, IntPtr address, UIntPtr size);
    [DllImport("kernel32", SetLastError=true)] internal static extern IntPtr OpenThread(uint access, bool inherit, int id);
    [DllImport("kernel32", SetLastError=true)] internal static extern uint SuspendThread(IntPtr h);
    [DllImport("kernel32", SetLastError=true)] internal static extern uint ResumeThread(IntPtr h);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool Wow64GetThreadContext(IntPtr h, byte[] context);
    [DllImport("kernel32")] internal static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32", SetLastError=true)] internal static extern bool IsWow64Process(IntPtr h, out bool wow64);
    [DllImport("xinput1_4", EntryPoint="XInputGetState")] internal static extern uint XInput(uint index, out Pad state);
    [StructLayout(LayoutKind.Sequential)] internal struct Pad { public uint packet; public ushort buttons; public byte lt,rt; public short lx,ly,rx,ry; }
}

sealed class X86 {
    readonly List<byte> bytes = new List<byte>();
    readonly Dictionary<string,int> labels = new Dictionary<string,int>();
    readonly List<KeyValuePair<int,string>> fixups = new List<KeyValuePair<int,string>>();
    public void B(params byte[] b) { bytes.AddRange(b); }
    public void U(uint n) { B(BitConverter.GetBytes(n)); }
    public void Label(string name) { labels.Add(name, bytes.Count); }
    public void J(byte condition, string target) { B(0x0F,condition); fixups.Add(new KeyValuePair<int,string>(bytes.Count,target)); U(0); }
    public void Jmp(string target) { B(0xE9);fixups.Add(new KeyValuePair<int,string>(bytes.Count,target));U(0); }
    public byte[] Finish(uint at, uint returnTo) {
        foreach (var f in fixups) { byte[] b=BitConverter.GetBytes(labels[f.Value]-f.Key-4); for(int i=0;i<4;i++)bytes[f.Key+i]=b[i]; }
        B(0xE9); U(unchecked(returnTo-at-(uint)bytes.Count-4)); return bytes.ToArray();
    }
    public static byte[] Sprint(uint at, uint data, uint module) {
        X86 a=new X86();
        // 32 bytes of saved state, including full XMM0 and MXCSR.
        a.B(0x9C,0x50,0x52,0x83,0xEC,0x14,0x0F,0xAE,0x1C,0x24);
        a.B(0x0F,0x11,0x44,0x24,0x04); // movups [esp+4],xmm0
        a.B(0xFF,0x05);a.U(data+8); // hooks
        a.B(0x89,0x35);a.U(data+12); // PlayerState*
        a.B(0xC7,0x05);a.U(data+32);a.U(0); // active this frame
        a.B(0x8B,0x06,0xA3);a.U(data+36); // actual state vtable
        a.B(0x3D);a.U(module+0x195B314);a.J(0x85,"done"); // exactly PlayerMove
        a.B(0x83,0x3D);a.U(data);a.B(1);a.J(0x85,"done");
        // Fail closed if the helper disappears or stalls for more than five seconds.
        // Low InterruptTime wraps every ~429 seconds; expiry latches enabled=0.
        a.B(0xA1);a.U(0x7FFE0008);a.B(0x2B,0x05);a.U(data+40);
        a.B(0x3D);a.U(50000000);a.J(0x86,"alive");
        a.B(0xC7,0x05);a.U(data);a.U(0);a.Jmp("done");
        a.Label("alive");
        a.B(0x8B,0x56,0x0C,0x85,0xD2);a.J(0x84,"done");
        a.B(0xA1);a.U(module+0x1ED67FC);a.B(0x85,0xC0);a.J(0x84,"done");
        a.B(0x39,0x90,0xB8,0x01,0,0);a.J(0x85,"done"); // actual War instance
        a.B(0x39,0xB2,0x48,0x01,0,0);a.J(0x85,"done"); // current state == this
        a.B(0x83,0x3D);a.U(data+48);a.B(1);a.J(0x85,"done"); // XInput L3+stick, polled every 8ms
        a.B(0x81,0xBA,0x94,0x05,0,0);a.U(0x3C23D70A);a.J(0x86,"done"); // game MoveSpeed > .01
        a.B(0x83,0xBA,0x68,0x06,0,0,0);a.J(0x85,"done"); // not mounted
        a.B(0x80,0xBA,0x70,0x05,0,0,0);a.J(0x85,"done"); // not Chaos form
        // No strafe, block, jump, context, attack, activate, weapon fire, focus or wrath.
        foreach(uint offset in new uint[]{0x5F9,0x5FD,0x600,0x601,0x603,0x608,0x60A,0x60C,0x60E,0x610,0x612,0x618,0x61A}) {
            a.B(0x80,0xBA);a.U(offset);a.B(0);a.J(0x85,"done");
        }
        a.B(0x8B,0x82,0x44,0x01,0,0,0x85,0xC0);a.J(0x84,"done"); // proxy
        a.B(0x80,0x78,0x2D,1);a.J(0x85,"done"); // on ground
        a.B(0x80,0x78,0x2E,0);a.J(0x85,"done"); // not sliding
        a.B(0x8B,0x46,0x08); // descriptor
        a.B(0x80,0xB8,0x00,0x01,0,0,1);a.J(0x85,"done"); // collision enabled
        a.B(0x80,0xB8,0x01,0x01,0,0,0);a.J(0x85,"done"); // not water
        a.B(0x80,0xB8,0x0C,0x01,0,0,1);a.J(0x85,"done"); // move control
        // Hook entry has already pushed the half-float argument (ESP = frameESP-4).
        // Local frameESP+4C,+50 become hookESP+70,+74 with 32 bytes saved.
        a.B(0xF3,0x0F,0x10,0x44,0x24,0x70);a.B(0xF3,0x0F,0x11,0x05);a.U(data+16);
        a.B(0xF3,0x0F,0x59,0x05);a.U(data+44);
        a.B(0xF3,0x0F,0x11,0x44,0x24,0x70);a.B(0xF3,0x0F,0x11,0x05);a.U(data+24);
        a.B(0xF3,0x0F,0x10,0x44,0x24,0x74);a.B(0xF3,0x0F,0x11,0x05);a.U(data+20);
        a.B(0xF3,0x0F,0x59,0x05);a.U(data+44);
        a.B(0xF3,0x0F,0x11,0x44,0x24,0x74);a.B(0xF3,0x0F,0x11,0x05);a.U(data+28);
        a.B(0xFF,0x05);a.U(data+4);
        a.B(0xC7,0x05);a.U(data+32);a.U(1);
        a.Label("done");
        a.B(0x0F,0x10,0x44,0x24,0x04,0x0F,0xAE,0x14,0x24,0x83,0xC4,0x14,0x5A,0x58,0x9D);
        a.B(0x8D,0x4C,0x24,0x6C,0x51); // original lea ecx,[esp+6C]; push ecx
        return a.Finish(at,module+SprintSession.HookRva+5);
    }
}

sealed class SprintSession : IDisposable {
    internal const uint HookRva=0x41052B; // PlayerState::updateMovement + 69B, shared path
    internal const string ExpectedHash="bb46640ddc728b2365cf3515ce173b25648ce5df649a85707dff8b9a8df42a43";
    internal static readonly byte[] Original={0x8D,0x4C,0x24,0x6C,0x51};
    internal Process Game; internal IntPtr Handle; internal uint Module, Cave, Data; internal bool Patched;
    byte[] jump;Thread inputWorker;volatile bool stopInput;internal volatile bool L3,StickMoving;internal volatile int Controller=-1;
    public static IntPtr P(uint a) { return new IntPtr((long)a); }
    static void Check(bool ok,string action) { if(!ok)throw new Win32Exception(Marshal.GetLastWin32Error(),action); }
    public byte[] Read(uint a,int n) { byte[] b=new byte[n];UIntPtr count;Check(Native.ReadProcessMemory(Handle,P(a),b,(UIntPtr)n,out count)&&count.ToUInt64()==(ulong)n,"Read game memory");return b; }
    public uint U(uint a) { return BitConverter.ToUInt32(Read(a,4),0); }
    public void Write(uint a,byte[] b) { UIntPtr count;Check(Native.WriteProcessMemory(Handle,P(a),b,(UIntPtr)b.Length,out count)&&count.ToUInt64()==(ulong)b.Length,"Write mod memory"); }
    public void W(uint a,uint n) { Write(a,BitConverter.GetBytes(n)); }
    public SprintSession(bool write,bool adoptExisting=false) {
        Process[] games=Process.GetProcessesByName("darksiders1");
        if(games.Length!=1)throw new Exception(games.Length==0 ? "Start Darksiders Warmastered and load your game first." : "More than one darksiders1.exe is running. Close the extra copy.");
        Game=games[0]; string path=Game.MainModule.FileName;
        string hash;using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        if(hash!=ExpectedHash)throw new Exception("Unsupported game build. No patch applied. SHA256: "+hash);
        Module=checked((uint)Game.MainModule.BaseAddress.ToInt64());
        Handle=Native.OpenProcess(write ? 0x438u : 0x410u,false,Game.Id);Check(Handle!=IntPtr.Zero,"Open Darksiders process");
        try {
            bool wow;Check(Native.IsWow64Process(Handle,out wow),"Check process architecture");if(!wow)throw new Exception("Expected the 32-bit game on 64-bit Windows.");
            byte[] current=Read(Module+HookRva,Original.Length);
            if(!current.SequenceEqual(Original)) {
                if(!adoptExisting || current[0]!=0xE9)throw new Exception("Movement code is already modified. Disable other movement patches or restart the game.");
                Cave=unchecked(Module+HookRva+5+(uint)BitConverter.ToInt32(current,1));Data=Cave+4096;
                byte[] ownStub=X86.Sprint(Cave,Data,Module);
                if(!Read(Cave,ownStub.Length).SequenceEqual(ownStub))throw new Exception("Unrecognized movement patch; refusing to replace it.");
            }
            uint clock=U(0x7FFE0008);Thread.Sleep(20);if(U(0x7FFE0008)==clock)throw new Exception("Could not validate the mod watchdog clock.");
        } catch { Native.CloseHandle(Handle);Handle=IntPtr.Zero;throw; }
    }
    public void Enable() {
        if(Patched) { Heartbeat();W(Data,1);return; }
        IntPtr memory=Native.VirtualAllocEx(Handle,IntPtr.Zero,(UIntPtr)8192,0x3000,0x04);
        Check(memory!=IntPtr.Zero,"Allocate mod memory");
        Cave=checked((uint)memory.ToInt64());Data=Cave+4096;
        byte[] code=X86.Sprint(Cave,Data,Module);Write(Cave,code);W(Data+44,0x40000000);Heartbeat();W(Data,1);
        uint old;Check(Native.VirtualProtectEx(Handle,P(Cave),(UIntPtr)4096,0x20,out old),"Protect mod code");
        Check(Native.FlushInstructionCache(Handle,P(Cave),(UIntPtr)code.Length),"Flush mod code cache");
        jump=new byte[]{0xE9,0,0,0,0};Array.Copy(BitConverter.GetBytes(unchecked(Cave-(Module+HookRva)-5)),0,jump,1,4);
        ReplaceCode(Module+HookRva,Original,jump);Patched=true;
        stopInput=false;inputWorker=new Thread(()=> { while(!stopInput) { try { PollInput();W(Data+48,L3&&StickMoving?1u:0u);Heartbeat(); }catch { try { W(Data+48,0);W(Data,0); }catch{}break; }Thread.Sleep(8); } });inputWorker.IsBackground=true;inputWorker.Start();
    }
    internal void PollInput() {
        L3=false;StickMoving=false;Controller=-1;
        for(uint i=0;i<4;i++) { Native.Pad p;if(Native.XInput(i,out p)==0) { Controller=(int)i;L3=(p.buttons&0x40)!=0;StickMoving=(long)p.lx*p.lx+(long)p.ly*p.ly>8000L*8000L;return; } }
    }
    public void Heartbeat() { if(Data!=0)W(Data+40,U(0x7FFE0008)); }
    void ReplaceCode(uint site,byte[] expected,byte[] replacement) {
        // Suspend and inspect every existing thread. Repeat on a thread-list change;
        // reject a thread parked inside the overwritten six-byte instruction window.
        for(int attempt=0;attempt<50;attempt++) {
            var held=new List<IntPtr>();bool retry=false;
            try {
                Game.Refresh();int[] ids=Game.Threads.Cast<ProcessThread>().Select(t=>t.Id).OrderBy(i=>i).ToArray();
                foreach(int id in ids) {
                    IntPtr t=Native.OpenThread(0x4A,false,id); // suspend,get context,query
                    if(t==IntPtr.Zero) { retry=true;break; }
                    if(Native.SuspendThread(t)==uint.MaxValue) { Native.CloseHandle(t);retry=true;break; }
                    held.Add(t);byte[] context=new byte[716];Array.Copy(BitConverter.GetBytes(0x10003),context,4);
                    Check(Native.Wow64GetThreadContext(t,context),"Inspect suspended game thread");
                    uint ip=BitConverter.ToUInt32(context,184);
                    if(ip>=site && ip<site+expected.Length)retry=true;
                }
                Game.Refresh();int[] now=Game.Threads.Cast<ProcessThread>().Select(t=>t.Id).OrderBy(i=>i).ToArray();
                if(!ids.SequenceEqual(now))retry=true;
                if(!retry) {
                    if(!Read(site,expected.Length).SequenceEqual(expected))throw new Exception("Patch site changed unexpectedly; refusing to overwrite another mod.");
                    uint old;Check(Native.VirtualProtectEx(Handle,P(site),(UIntPtr)expected.Length,0x40,out old),"Temporarily unlock movement code");
                    try {
                        try { Write(site,replacement);Check(Native.FlushInstructionCache(Handle,P(site),(UIntPtr)expected.Length),"Flush game code cache");if(!Read(site,expected.Length).SequenceEqual(replacement))throw new Exception("Live patch verification failed."); }
                        catch { Write(site,expected);Native.FlushInstructionCache(Handle,P(site),(UIntPtr)expected.Length);throw; }
                    }
                    finally { uint ignored;Check(Native.VirtualProtectEx(Handle,P(site),(UIntPtr)expected.Length,old,out ignored),"Restore game code protection"); }
                    return;
                }
            } finally { foreach(IntPtr t in held) { Native.ResumeThread(t);Native.CloseHandle(t); } }
            Thread.Sleep(5);
        }
        throw new Exception("Game threads were busy. No code changed; try again while paused.");
    }
    public void Disable() {
        stopInput=true;if(inputWorker!=null) { inputWorker.Join();inputWorker=null; }
        if(!Patched)return;
        if(Game.HasExited) { Patched=false;return; }
        W(Data,0);ReplaceCode(Module+HookRva,jump,Original);Patched=false;
        // Keep the small allocation until the game exits: a suspended instruction or
        // pending return can still reference the trampoline. Never free executable
        // memory while a game thread might be inside it.
    }
    public string Status() {
        if(!Patched)PollInput();
        uint root=U(Module+0x1ED67FC);uint player=root==0?0:U(root+0x1B8);
        if(player==0)return "Waiting for War / gameplay...";
        byte[] b=Read(player,0x720);uint state=BitConverter.ToUInt32(b,0x148);uint proxy=BitConverter.ToUInt32(b,0x144);
        string grounded=proxy==0?"?":Read(proxy+0x2D,1)[0].ToString();
        string stateName=state!=0&&U(state)==Module+0x195B314 ? "ordinary movement" : "other state (sprint blocked)";
        string s=String.Format("XInput pad {0}: L3 {1}   Stick {2}\r\nGrounded {3}   State: {4}\r\nWar position: {5:F1}, {6:F1}, {7:F1}",Controller,L3?"HELD":"released",StickMoving?"MOVING":"idle",grounded,stateName,BitConverter.ToSingle(b,0x60),BitConverter.ToSingle(b,0x64),BitConverter.ToSingle(b,0x68));
        if(Patched) { byte[] d=Read(Data,48);s+=String.Format("\r\nSprint: {0}   Changed: {1}   Hook hits: {6}\r\nMovement: {2:F2}, {3:F2} -> {4:F2}, {5:F2}",BitConverter.ToUInt32(d,0)==0?"WATCHDOG DISABLED":BitConverter.ToUInt32(d,32)==1?"ACTIVE 2x":"ready",BitConverter.ToUInt32(d,4),BitConverter.ToSingle(d,16),BitConverter.ToSingle(d,20),BitConverter.ToSingle(d,24),BitConverter.ToSingle(d,28),BitConverter.ToUInt32(d,8)); }
        return s;
    }
    public void RetireV02() {
        uint oldSite=Module+0x410664;byte[] code=Read(oldSite,6);byte[] original={0xF3,0x0F,0x10,0x44,0x24,0x78};
        if(code.SequenceEqual(original))return;
        if(code[0]!=0xE9 || code[5]!=0x90)throw new Exception("Unknown old movement patch.");
        uint oldCave=unchecked(oldSite+5+(uint)BitConverter.ToInt32(code,1));
        if(!Read(oldCave,10).SequenceEqual(new byte[]{0x9C,0x50,0x52,0x83,0xEC,4,0x0F,0xAE,0x1C,0x24}))throw new Exception("Unrecognized old stub.");
        byte[] tail=Read(oldCave+543,11);if(!tail.Take(6).SequenceEqual(original)||tail[6]!=0xE9||unchecked(oldCave+554+(uint)BitConverter.ToInt32(tail,7))!=oldSite+6)throw new Exception("Old return target mismatch.");
        W(oldCave+4096,0);ReplaceCode(oldSite,code,original);
    }
    public void RetireCurrent() {
        if(Data==0)return;
        W(Data,0);byte[] expected=Read(Module+HookRva,Original.Length);
        ReplaceCode(Module+HookRva,expected,Original);Data=0;
    }
    public void Dispose() { Disable();if(Handle!=IntPtr.Zero)Native.CloseHandle(Handle);Handle=IntPtr.Zero; }
}

sealed class SprintForm : Form {
    readonly Button enable=new Button(),disable=new Button(),diagnostic=new Button();
    readonly Label headline=new Label(),status=new Label();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    SprintSession session;string lastLog="";readonly string logPath;uint lastCount;bool wasActive;
    public SprintForm(bool autoEnable) {
        Text="Darksiders • Hold L3 Sprint 2x • v0.4";ClientSize=new Size(650,410);MinimumSize=Size;StartPosition=FormStartPosition.CenterScreen;
        BackColor=Color.FromArgb(24,26,32);ForeColor=Color.White;Font=new Font("Segoe UI",10);
        headline.SetBounds(22,18,605,42);headline.Text="Hold L3 while moving to sprint at 2×";headline.Font=new Font(Font.FontFamily,16,FontStyle.Bold);Controls.Add(headline);
        Label help=new Label();help.SetBounds(24,66,600,45);help.Text="Start the game, load into gameplay, then enable sprint.\r\nRelease L3 for normal speed. Keep this helper open.";Controls.Add(help);
        enable.SetBounds(24,124,180,38);enable.Text="Enable sprint";enable.ForeColor=Color.Black;enable.Click+=(s,e)=>StartSprint();Controls.Add(enable);
        disable.SetBounds(218,124,180,38);disable.Text="Disable / restore";disable.ForeColor=Color.Black;disable.Enabled=false;disable.Click+=(s,e)=>StopSprint();Controls.Add(disable);
        diagnostic.SetBounds(412,124,210,38);diagnostic.Text="Check input (read only)";diagnostic.ForeColor=Color.Black;diagnostic.Click+=(s,e)=>CheckInput();Controls.Add(diagnostic);
        status.SetBounds(24,183,600,200);status.Font=new Font("Consolas",10);status.Text="Sprint is OFF. Game files and saves are never edited.";Controls.Add(status);
        logPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"DarksidersSprint.log");Log("Helper started. Sprint OFF.");
        timer.Interval=100;timer.Tick+=(s,e)=>RefreshStatus();timer.Start();
        if(autoEnable)Shown+=(s,e)=>StartSprint();
        FormClosing+=(s,e)=> { if(session!=null)try { session.Dispose();session=null;Log("Original movement code restored."); }catch(Exception ex) { e.Cancel=true;ShowError(ex); } };
    }
    void Log(string s) { if(s==lastLog)return;lastLog=s;try { File.AppendAllText(logPath,DateTimeOffset.Now.ToString("o")+" "+s+Environment.NewLine); }catch{} }
    void ShowError(Exception e) { Log("ERROR: "+e.Message);MessageBox.Show(this,e.Message,"Darksiders sprint",MessageBoxButtons.OK,MessageBoxIcon.Information); }
    void StartSprint() {
        try { if(session!=null)session.Dispose();session=new SprintSession(true);session.Enable();enable.Enabled=false;disable.Enabled=true;diagnostic.Enabled=false;Log("Sprint hook installed; waiting for XInput L3 and ordinary movement.");RefreshStatus(); }
        catch(Exception e) { if(session!=null)try { session.Dispose(); }catch{}session=null;ShowError(e); }
    }
    void StopSprint() {
        try { if(session!=null)session.Dispose();session=null;enable.Enabled=true;disable.Enabled=false;diagnostic.Enabled=true;status.Text="Sprint OFF. Original movement code restored.";Log(status.Text); }
        catch(Exception e) { ShowError(e); }
    }
    void CheckInput() {
        try { if(session!=null)session.Dispose();session=new SprintSession(false);Log("Read-only game input diagnostic opened.");RefreshStatus(); }
        catch(Exception e) { session=null;ShowError(e); }
    }
    void RefreshStatus() {
        if(session==null)return;
        try {
            if(session.Game.HasExited) { session.Dispose();session=null;enable.Enabled=true;disable.Enabled=false;diagnostic.Enabled=true;status.Text="Game closed. Sprint OFF.";Log(status.Text);return; }
            session.Heartbeat();status.Text=session.Status();
            if(session.Patched) { uint count=session.U(session.Data+4);bool active=session.U(session.Data+32)==1;if(active!=wasActive || unchecked(count-lastCount)>=300) { Log("Scaled frames="+count+" | "+status.Text.Replace("\r\n"," | "));lastCount=count;wasActive=active; } }
        } catch(Exception e) { timer.Stop();ShowError(e); }
    }
}

static class Program {
    [STAThread] static int Main(string[] args) {
        bool autoEnable=args.Length==1&&args[0]=="--enabled";
        if(args.Length>0&&!autoEnable) {
            try {
                if(args[0]=="--dump-stub") { File.WriteAllBytes(args[1],X86.Sprint(0x30000000,0x30001000,0x770000));return 0; }
                if(args[0]=="--verify") {
                    using(var s=new SprintSession(false))File.WriteAllText(args[1],"Supported executable verified; original hook bytes intact.\r\n"+s.Status());return 0;
                }
                if(args[0]=="--retire-v02") { using(var s=new SprintSession(true)) { s.RetireV02();File.WriteAllText(args[1],"Old v0.2 hook restored and verified."); }return 0; }
                if(args[0]=="--retire-current") { using(var s=new SprintSession(true,true)) { s.RetireCurrent();File.WriteAllText(args[1],"Recognized sprint hook restored and verified."); }return 0; }
                if(args[0]=="--test-restore") {
                    using(var s=new SprintSession(true)) { s.Enable();if(s.Read(s.Module+SprintSession.HookRva,1)[0]!=0xE9)throw new Exception("Live jump missing");s.Heartbeat();Thread.Sleep(150);s.Disable();if(!s.Read(s.Module+SprintSession.HookRva,5).SequenceEqual(SprintSession.Original))throw new Exception("Restore mismatch");File.WriteAllText(args[1],"PASS: installed hook, verified live jump, disabled, and verified all five original bytes restored.\r\n"+s.Status()); }return 0;
                }
                if(args[0]=="--input") { int seconds=args.Length>2?int.Parse(args[2]):15;using(var s=new SprintSession(false))using(var f=new StreamWriter(args[1])) { for(int i=0;i<seconds*10;i++) { Native.Pad p;uint err=Native.XInput(0,out p);f.WriteLine(DateTimeOffset.Now.ToString("o")+" XInput="+err+" L3="+((p.buttons&0x40)!=0)+" X="+p.lx+" Y="+p.ly+" | "+s.Status().Replace("\r\n"," | "));f.Flush();Thread.Sleep(100); } }return 0; }
                throw new Exception("Unknown command line option.");
            } catch(Exception e) { if(args.Length>1)File.WriteAllText(args[1],"ERROR: "+e);return 1; }
        }
        bool created;using(var mutex=new Mutex(true,"Local\\DarksidersSprint-2679",out created)) {
            if(!created) { MessageBox.Show("Darksiders Sprint is already open.");return 1; }
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SprintForm(autoEnable));
        }return 0;
    }
}
