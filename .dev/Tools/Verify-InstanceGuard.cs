using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
internal static class VerifyInstanceGuard {
    static int Child(string executable,string mode,string name){
        using(var process=Process.Start(new ProcessStartInfo(executable,mode+" "+name){UseShellExecute=false,CreateNoWindow=true})){
            if(!process.WaitForExit(5000))throw new Exception("Guard helper did not exit.");return process.ExitCode;
        }
    }
    static int Main(string[] args){
        if(args.Length==2){
            var child=new AppInstanceGate(args[1]);bool acquired=child.Acquire();
            if(args[0]=="abandon"&&acquired)Environment.Exit(0);
            if(!acquired)child.RequestActivation();child.Dispose();return acquired?0:2;
        }
        string name=AppInstanceGate.SharedName+".Test."+Guid.NewGuid().ToString("N");
        string self=System.Reflection.Assembly.GetExecutingAssembly().Location;
        string other=Path.Combine(Path.GetDirectoryName(self),"SecondBuild.GuardCheck.exe");
        File.Copy(self,other,true);
        using(var first=new AppInstanceGate(name)){
            if(!first.Acquire()||Child(other,"probe",name)!=2||!first.Activate.WaitOne(2000))throw new Exception("Second-build blocking or activation failed.");
        }
        if(Child(other,"probe",name)!=0)throw new Exception("Normal exit did not release guard.");
        using(var observer=new AppInstanceGate(name)){
            if(Child(other,"abandon",name)!=0||!observer.Acquire())throw new Exception("Abandoned guard recovery failed.");
        }
        if(AppInstanceGate.SharedName.IndexOf("2.2",StringComparison.Ordinal)>=0)throw new Exception("Version leaked into shared identity.");
        Console.WriteLine("Shared instance guard passed: differently named processes, duplicate blocked, activation signaled, exit released, abandoned owner recovered. Isolated random test identity; no app window opened.");return 0;
    }
}
