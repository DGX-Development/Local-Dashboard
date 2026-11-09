using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;

internal sealed class AppInstanceGate : IDisposable {
    readonly Mutex mutex;
    readonly Mutex installerMarker;
    internal readonly EventWaitHandle Activate;
    bool owned;
    internal static string SharedName {get{return @"Global\DGX.LocalDashboard.User."+WindowsIdentity.GetCurrent().User.Value;}}
    internal AppInstanceGate(string name=null){
        bool production=name==null;
        name=name??SharedName;
        mutex=new Mutex(false,name);
        try{Activate=new EventWaitHandle(false,EventResetMode.AutoReset,name+".Activate");if(production)installerMarker=new Mutex(false,@"Local\DGX.LocalDashboard.Setup");}
        catch{if(Activate!=null)Activate.Dispose();mutex.Dispose();throw;}
    }
    internal bool Acquire(){
        if(owned)return true;
        try{owned=mutex.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
        return owned;
    }
    internal void RequestActivation(){Activate.Set();}
    internal static string OtherRunningVersion(bool legacyOnly=false){
        using(var self=Process.GetCurrentProcess()){
            foreach(var process in Process.GetProcesses())using(process){
                try{
                    if(process.Id==self.Id||process.SessionId!=self.SessionId)continue;
                    var module=process.MainModule;if(module==null)continue;string path=module.FileName;
                    var info=FileVersionInfo.GetVersionInfo(path);
                    if(!String.Equals(info.ProductName,"Local Dashboard",StringComparison.Ordinal))continue;
                    Version version;if(legacyOnly&&Version.TryParse(info.FileVersion,out version)&&version>=new Version(2,2,0,0))continue;
                    return info.FileVersion??"unknown";
                }catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}catch(System.IO.IOException){}
            }
        }
        return null;
    }
    public void Dispose(){
        if(owned){mutex.ReleaseMutex();owned=false;}
        if(installerMarker!=null)installerMarker.Dispose();Activate.Dispose();mutex.Dispose();
    }
}
