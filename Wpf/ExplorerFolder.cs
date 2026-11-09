using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Controls;
using System.Windows.Threading;

internal sealed partial class WpfDashboard {
    DateTime lastFolderRequest=DateTime.MinValue;
    Func<string,bool> activateFolder=ActivateExplorerFolder;
    Action<string> launchFolder=path=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"\""+path.TrimEnd('\\')+"\""){UseShellExecute=false});
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr handle);
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr handle,int command);
    static object ComValue(object instance,string name,params object[] arguments){return instance.GetType().InvokeMember(name,BindingFlags.GetProperty|BindingFlags.InvokeMethod,null,instance,arguments);}
    static void ReleaseCom(object instance){if(instance!=null&&Marshal.IsComObject(instance))Marshal.ReleaseComObject(instance);}
    static bool ActivateExplorerFolder(string folder){
        object shell=null,windows=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application"));windows=ComValue(shell,"Windows");int count=Convert.ToInt32(ComValue(windows,"Count"));
            for(int i=0;i<count;i++){object item=null;try{
                item=ComValue(windows,"Item",i);if(item==null)continue;string executable=Convert.ToString(ComValue(item,"FullName"));if(!String.Equals(Path.GetFileName(executable),"explorer.exe",StringComparison.OrdinalIgnoreCase))continue;
                Uri location;if(!Uri.TryCreate(Convert.ToString(ComValue(item,"LocationURL")),UriKind.Absolute,out location)||!location.IsFile)continue;
                if(!String.Equals(Path.GetFullPath(location.LocalPath).TrimEnd('\\'),Path.GetFullPath(folder).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))continue;
                var handle=new IntPtr(Convert.ToInt64(ComValue(item,"HWND")));if(IsIconic(handle))ShowWindowAsync(handle,9);SetForegroundWindow(handle);return true;
            }catch(COMException){}catch(TargetInvocationException){}finally{ReleaseCom(item);}}
        }catch(COMException){}catch(TargetInvocationException){}finally{ReleaseCom(windows);ReleaseCom(shell);}return false;
    }
    bool RequestDataFolder(){
        string path=Path.GetFullPath(profile);if(!Directory.Exists(path))throw new DirectoryNotFoundException("Data folder is unavailable.");
        if(activateFolder(path)){lastFolderRequest=DateTime.MinValue;return true;}
        if(DateTime.UtcNow-lastFolderRequest<TimeSpan.FromSeconds(10))return false;
        launchFolder(path);lastFolderRequest=DateTime.UtcNow;return false;
    }
    void OpenDataFolder(){RequestDataFolder();}
    void OpenDataFolderWithFeedback(Button button,TextBlock feedback){
        try{if(RequestDataFolder()){feedback.Text="Data folder is open in File Explorer.";return;}}catch(Exception error){feedback.Text="Could not open the folder. "+error.Message;return;}
        button.IsEnabled=false;button.Content="Opening…";feedback.Text="Opening File Explorer. It may appear on another monitor.";int attempts=0;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};
        timer.Tick+=(s,e)=>{bool found=false;try{found=activateFolder(Path.GetFullPath(profile));}catch(COMException){}if(found||++attempts>=20){timer.Stop();button.IsEnabled=true;button.Content="Open data folder";feedback.Text=found?"Data folder is open in File Explorer.":"File Explorer was requested. Check the taskbar or another monitor.";}};
        button.Unloaded+=(s,e)=>timer.Stop();timer.Start();
    }
}
