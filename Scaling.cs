using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using System.Drawing;

// Minimal x64 bindings to AMD's stable ADLX C interfaces. See SOURCES.md.
sealed class Amd : IDisposable
{
    [DllImport("amdadlx64.dll", CallingConvention=CallingConvention.Cdecl)] static extern int ADLXQueryFullVersion(out ulong version);
    [DllImport("amdadlx64.dll", CallingConvention=CallingConvention.Cdecl)] static extern int ADLXInitialize(ulong version, out IntPtr system);
    [DllImport("amdadlx64.dll", CallingConvention=CallingConvention.Cdecl)] static extern int ADLXTerminate();
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
    [DllImport("kernel32.dll")] static extern bool FreeLibrary(IntPtr module);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetPtr(IntPtr self, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetForDisplay(IntPtr self, IntPtr display, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int At(IntPtr self, uint index, out IntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate uint Count(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetInt(IntPtr self, out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetSize(IntPtr self, out UIntPtr value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int GetBool(IntPtr self, out byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetInt(IntPtr self, int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int SetBool(IntPtr self, byte value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] delegate int Release(IntPtr self);
    readonly Stack<IntPtr> owned = new Stack<IntPtr>();
    IntPtr library, system, services;
    bool initialized;
    public sealed class Display
    {
        public ulong Id; public string Name; public IntPtr Ptr;
        public override string ToString() { return Name + " (ID " + Id + ")"; }
    }
    public readonly List<Display> Displays = new List<Display>();
    static T Fn<T>(IntPtr p, int slot) where T:class
    {
        if (p == IntPtr.Zero) throw new Exception("AMD returned an empty interface.");
        return (T)(object)Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(p), slot * IntPtr.Size), typeof(T));
    }
    static void Check(int result, string operation)
    {
        if (result > 2 || result < 0) throw new Exception(operation + " failed (AMD code " + result + ").");
    }
    IntPtr Own(IntPtr p) { if(p != IntPtr.Zero) owned.Push(p); return p; }
    public Amd()
    {
        try {
            library = LoadLibraryEx(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "amdadlx64.dll"), IntPtr.Zero, 0x800);
            if(library == IntPtr.Zero) throw new Exception("Cannot load the installed AMD ADLX driver: " + Marshal.GetLastWin32Error());
            ulong version; Check(ADLXQueryFullVersion(out version), "Query AMD version");
            Check(ADLXInitialize(version, out system), "Initialize AMD"); initialized=true;
            IntPtr p; Check(Fn<GetPtr>(system,3)(system,out p), "Get display services"); services=Own(p);
            Check(Fn<GetPtr>(services,4)(services,out p), "Get displays"); IntPtr list=Own(p);
            uint begin=Fn<Count>(list,5)(list), end=Fn<Count>(list,6)(list);
            for(uint i=begin; i<end; i++) {
                Check(Fn<At>(list,11)(list,i,out p), "Read display"); IntPtr display=Own(p);
                IntPtr name; UIntPtr id;
                Check(Fn<GetPtr>(display,6)(display,out name), "Read monitor name");
                Check(Fn<GetSize>(display,13)(display,out id), "Read monitor ID");
                Displays.Add(new Display { Id=id.ToUInt64(), Name=Marshal.PtrToStringAnsi(name), Ptr=display });
            }
        } catch { Dispose(); throw; }
    }
    IntPtr Feature(Display display, int slot)
    {
        IntPtr p; Check(Fn<GetForDisplay>(services,slot)(services,display.Ptr,out p), "Get scaling interface"); return Own(p);
    }
    bool Supported(IntPtr p) { byte v; Check(Fn<GetBool>(p,3)(p,out v),"Read support"); return v!=0; }
    public string Status(Display d)
    {
        IntPtr gpu=Feature(d,11), mode=Feature(d,12);
        if(!Supported(gpu) || !Supported(mode)) return "Scaling not supported";
        byte on; int value; Check(Fn<GetBool>(gpu,4)(gpu,out on),"Read GPU scaling"); Check(Fn<GetInt>(mode,4)(mode,out value),"Read scaling mode");
        return "GPU scaling " + (on!=0 ? "ON" : "OFF") + ", " + ModeName(value);
    }
    public Display Select(ulong? id)
    {
        if(id.HasValue) { foreach(Display d in Displays) if(d.Id==id.Value) return d; throw new Exception("Selected monitor is disconnected. Run list to choose a current display ID."); }
        if(Displays.Count==1) return Displays[0];
        throw new Exception("Choose a monitor using --display ID, or the tray menu. Found " + Displays.Count + " displays.");
    }
    public string Apply(Display d, string command)
    {
        IntPtr gpu=Feature(d,11), mode=Feature(d,12);
        if(!Supported(gpu) || !Supported(mode)) throw new Exception("GPU scaling is not supported on " + d.Name);
        int old; byte enabled; Check(Fn<GetInt>(mode,4)(mode,out old),"Read scaling mode"); Check(Fn<GetBool>(gpu,4)(gpu,out enabled),"Read GPU scaling");
        int target = command=="center" ? 2 : command=="stretch" ? 1 : command=="aspect" ? 0 : command=="cycle" ? (old==2 ? 1 : old==1 ? 0 : 2) : -1;
        if(target<0) throw new Exception("Unknown command: " + command);
        try {
            if(enabled==0) Check(Fn<SetBool>(gpu,5)(gpu,1),"Enable GPU scaling");
            Check(Fn<SetInt>(mode,5)(mode,target),"Set scaling mode");
            int actual; byte on; Check(Fn<GetInt>(mode,4)(mode,out actual),"Verify mode"); Check(Fn<GetBool>(gpu,4)(gpu,out on),"Verify GPU scaling");
            if(actual!=target || on==0) throw new Exception("Driver did not retain the requested setting.");
        } catch(Exception original) {
            int restoreMode=Fn<SetInt>(mode,5)(mode,old), restoreEnabled=Fn<SetBool>(gpu,5)(gpu,enabled);
            if(restoreMode>2 || restoreEnabled>2) throw new Exception(original.Message + " Restoring previous settings also failed; check Adrenalin.");
            throw;
        }
        return d.Name + ": " + ModeName(target);
    }
    public static string ModeName(int mode) { return mode==0 ? "Preserve aspect ratio" : mode==1 ? "Stretch (Full panel)" : mode==2 ? "Center" : "Unknown ("+mode+")"; }
    public void Dispose()
    {
        while(owned.Count>0) { IntPtr p=owned.Pop(); Fn<Release>(p,1)(p); }
        if(initialized) { ADLXTerminate(); initialized=false; }
        if(library!=IntPtr.Zero) { FreeLibrary(library); library=IntPtr.Zero; }
    }
}

sealed class ScalingTray : Form
{
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr handle,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr handle,int id);
    readonly NotifyIcon icon=new NotifyIcon();
    readonly List<int> registered=new List<int>();
    readonly string[] commands={"center","stretch","aspect","cycle"};
    ulong? displayId;
    readonly string config=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"monitor.txt");
    readonly Amd amd;
    readonly Bitmap trayBitmap;
    readonly Icon trayIcon;
    bool closing;
    bool cleanedUp;
    IntPtr hotkeyHandle;
    static Icon CreateTrayIcon(out Bitmap bitmap)
    {
        bitmap=new Bitmap(32,32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using(Graphics g=Graphics.FromImage(bitmap))
        using(Brush orange=new SolidBrush(Color.FromArgb(244,76,32)))
        using(Brush white=new SolidBrush(Color.White))
        using(Font font=new Font("Segoe UI",20,FontStyle.Bold,GraphicsUnit.Pixel)) {
            g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent); g.FillEllipse(orange,1,1,30,30);
            StringFormat centered=new StringFormat { Alignment=StringAlignment.Center, LineAlignment=StringAlignment.Center };
            g.DrawString("A",font,white,new RectangleF(0,-1,32,33),centered);
        }
        return Icon.FromHandle(bitmap.GetHicon());
    }
    public ScalingTray(ulong? target)
    {
        displayId=target; ulong saved;
        if(!displayId.HasValue && File.Exists(config) && ulong.TryParse(File.ReadAllText(config).Trim(),out saved)) displayId=saved;
        ShowInTaskbar=false; FormBorderStyle=FormBorderStyle.FixedToolWindow;
        trayIcon=CreateTrayIcon(out trayBitmap); icon.Icon=trayIcon; icon.Text="AMD Scaling Hotkeys";
        ContextMenuStrip menu=new ContextMenuStrip();
        try {
            amd=new Amd();
            // A settings file may have travelled with the executable from another PC.
            // Re-select automatically when the new machine has one display (or let
            // the tray menu choose when it has several).
            bool known=false;
            if(displayId.HasValue) foreach(Amd.Display existing in amd.Displays) if(existing.Id==displayId.Value) { known=true; break; }
            if(displayId.HasValue && !known) displayId=amd.Displays.Count==1 ? (ulong?)amd.Displays[0].Id : null;
            if(!displayId.HasValue && amd.Displays.Count==1) displayId=amd.Displays[0].Id;
            ToolStripMenuItem monitors=new ToolStripMenuItem("Target monitor");
            foreach(Amd.Display d in amd.Displays) {
                ulong id=d.Id;
                ToolStripMenuItem item=new ToolStripMenuItem(d.ToString()); item.Checked=displayId==id;
                item.Click+=delegate { displayId=id; File.WriteAllText(config,id.ToString()); foreach(ToolStripMenuItem other in monitors.DropDownItems) other.Checked=other==item; };
                monitors.DropDownItems.Add(item);
            }
            menu.Items.Add(monitors);
        } catch {
            throw;
        }
        string[] labels={"Center — Ctrl+Alt+1","Stretch — Ctrl+Alt+2","Preserve aspect ratio — Ctrl+Alt+3","Cycle — Ctrl+Alt+S"};
        for(int i=0;i<commands.Length;i++) { string command=commands[i]; menu.Items.Add(labels[i],null,delegate { Apply(command); }); }
        menu.Items.Add("Exit",null,delegate {
            // Let the context-menu click finish before disposing the tray window.
            if(IsHandleCreated && !IsDisposed) BeginInvoke(new Action(delegate { if(IsHandleCreated && !IsDisposed) Close(); }));
        });
        icon.ContextMenuStrip=menu; icon.Visible=true; hotkeyHandle=Handle;
        uint[] keys={0x31,0x32,0x33,0x53};
        List<string> failed=new List<string>();
        for(int i=0;i<keys.Length;i++) {
            if(RegisterHotKey(hotkeyHandle,i+1,0x4003,keys[i])) registered.Add(i+1); else failed.Add(labels[i]);
        }
        if(failed.Count>0) MessageBox.Show("These keys are already in use. The tray menu still works:\n"+string.Join("\n",failed.ToArray()),"AMD Scaling");
        icon.ShowBalloonTip(3000,"AMD Scaling", "Ready: Ctrl+Alt+1 Center, 2 Stretch, 3 Aspect, S Cycle.",ToolTipIcon.Info);
    }
    protected override void SetVisibleCore(bool value) { base.SetVisibleCore(false); }
    void Apply(string command)
    {
        if(closing || IsDisposed) return;
        try { string result=amd.Apply(amd.Select(displayId),command); icon.Text="AMD Scaling: " + result.Substring(0,Math.Min(48,result.Length)); }
        catch(Exception e) { if(!closing && !IsDisposed) icon.ShowBalloonTip(5000,"AMD Scaling error",e.Message,ToolTipIcon.Error); }
    }
    protected override void WndProc(ref Message m) { if(!closing && m.Msg==0x312) { int id=m.WParam.ToInt32(); if(id>=1 && id<=4) Apply(commands[id-1]); } base.WndProc(ref m); }
    protected override void Dispose(bool disposing) {
        if(disposing && !cleanedUp) {
            cleanedUp=true; closing=true;
            foreach(int id in registered) UnregisterHotKey(hotkeyHandle,id);
            registered.Clear(); icon.Visible=false; icon.Dispose(); if(amd!=null) amd.Dispose(); trayIcon.Dispose(); trayBitmap.Dispose();
        }
        base.Dispose(disposing);
    }
}

static class Program
{
    [STAThread] static int Main(string[] args)
    {
        string command=args.Length==0 ? "tray" : args[0].ToLowerInvariant();
        try {
            if(command=="help" || command=="--help") { Console.WriteLine("AmdScaling.exe list | center | stretch | aspect | cycle | tray [--display ID]\nChanges enable GPU scaling on the selected display. Center -> Stretch -> Aspect -> Center.\nNo display argument is needed when only one display is connected."); return 0; }
            ulong? id=null;
            if(args.Length>1) { ulong parsed; if(args.Length!=3 || args[1]!="--display" || !ulong.TryParse(args[2],out parsed)) throw new Exception("Use --display followed by an ID from the list command."); id=parsed; }
            if(command=="tray") {
                bool created;
                using(Mutex single=new Mutex(true,"Local\\AmdScalingHotkeys",out created)) {
                    if(!created) { MessageBox.Show("AMD Scaling Hotkeys is already running in the notification area."); return 0; }
                    Application.EnableVisualStyles(); using(ScalingTray tray=new ScalingTray(id)) Application.Run(tray);
                }
            } else {
                if(command!="list" && command!="center" && command!="stretch" && command!="aspect" && command!="cycle") throw new Exception("Use list, center, stretch, aspect, cycle, or tray.");
                using(Amd amd=new Amd()) {
                    if(command=="list") foreach(Amd.Display d in amd.Displays) { try { Console.WriteLine(d + ": " + amd.Status(d)); } catch(Exception e) { Console.WriteLine(d+": "+e.Message); } }
                    else Console.WriteLine(amd.Apply(amd.Select(id),command));
                }
            }
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e.Message); if(command=="tray" || Application.ExecutablePath.EndsWith("Hotkeys.exe",StringComparison.OrdinalIgnoreCase)) MessageBox.Show(e.Message,"AMD Scaling"); return 1; }
    }
}
