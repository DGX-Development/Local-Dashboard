using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;

internal sealed class ChecklistLocationChoice { public int schemaVersion{get;set;} public string path{get;set;} }
internal static class ChecklistLocation {
    internal static string Config(string profile){return Path.Combine(profile,".local","checklist-location.json");}
    internal static string Token(string path){return File.Exists(path)?DashboardData.Hash(DashboardData.ReadBounded(path)):null;}
    internal static string DefaultPath(string profile){return Path.Combine(profile,".local","current-work.json");}
    internal static void UseDefault(string profile,string expectedConfig,List<Entry> entries)
    {
        string config=Config(profile);
        using(var mutex=new Mutex(false,ChecklistStore.LockName(config)))
        {
            bool held=false;
            try
            {
                try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}
                if(!held||Token(config)!=expectedConfig)throw new IOException("The checklist location changed. Reopen Settings.");
                if(File.Exists(Path.Combine(profile,".local","restore-pending.json"))||File.Exists(Path.Combine(profile,".local","safe-restore-pending.json")))
                    throw new IOException("Finish recovering the interrupted configuration restore before changing the checklist location.");
                new ChecklistStore(DefaultPath(profile),entries).Read();
                File.Delete(config);
            }
            finally{if(held)mutex.ReleaseMutex();}
        }
    }
    internal static string PathFor(string profile){
        if(!File.Exists(Config(profile)))return DefaultPath(profile);
        var choice=AppData.Json.Deserialize<ChecklistLocationChoice>(Encoding.UTF8.GetString(DashboardData.ReadBounded(Config(profile),16384)));
        if(choice==null||choice.schemaVersion!=1)throw new IOException("The checklist location is invalid. Choose its file in Settings > Tasks.");
        return Validate(profile,choice.path,false);
    }
    internal static void RequireAvailable(string profile){if(File.Exists(Config(profile))&&!File.Exists(PathFor(profile)))throw new IOException("Your selected checklist file is unavailable. Reconnect its folder or choose an existing checklist in Settings > Tasks. No empty replacement was created.");}
    internal static string Validate(string profile,string path,bool requireFolder=true){
        if(String.IsNullOrWhiteSpace(path)||path.Any(Char.IsControl)||path.Contains("\"")||!Path.IsPathRooted(path)||path.StartsWith(@"\\?\")||path.StartsWith(@"\\.\")||!String.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a Dashboard checklist (.json) file using a full file path.");
        path=Path.GetFullPath(path);
        string root=Path.GetFullPath(profile).TrimEnd('\\')+"\\";
        string restored=Path.GetFullPath(Path.Combine(profile,".local","restored-lists"))+"\\";
        if(path.StartsWith(root,StringComparison.OrdinalIgnoreCase)&&!path.StartsWith(restored,StringComparison.OrdinalIgnoreCase)&&!String.Equals(path,Path.Combine(profile,".local","current-work.json"),StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a checklist outside Dashboard's internal data folder, or keep the current default.");
        if(requireFolder&&!Directory.Exists(Path.GetDirectoryName(path)))throw new IOException("The checklist folder is unavailable.");
        return path;
    }
    internal static void Choose(string profile,string destination,string expectedConfig,string expectedCurrent,string expectedDestination,List<Entry> entries){
        destination=Validate(profile,destination);string original=PathFor(profile);
        if(String.Equals(original,destination,StringComparison.OrdinalIgnoreCase))return;
        var locks=new List<Mutex>();
        try{
            foreach(string file in new[]{Config(profile),original,destination}.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)){
                var mutex=new Mutex(false,ChecklistStore.LockName(file));bool held=false;try{held=mutex.WaitOne(0);}catch(AbandonedMutexException){held=true;}if(!held){mutex.Dispose();throw new IOException("A checklist is being edited. Try again.");}locks.Add(mutex);
            }
            if(File.Exists(Path.Combine(profile,".local","restore-pending.json"))||File.Exists(Path.Combine(profile,".local","safe-restore-pending.json")))throw new IOException("Finish recovering the interrupted configuration restore before changing the checklist location.");
            if(Token(Config(profile))!=expectedConfig||Token(original)!=expectedCurrent||Token(destination)!=expectedDestination)throw new IOException("A checklist or its location changed. Reopen Settings; no existing tasks were overwritten.");
            new ChecklistStore(original,entries).Read();new ChecklistStore(destination,entries).Read();
            if(!File.Exists(destination)){
                RequireAvailable(profile);
                byte[] bytes=File.Exists(original)?DashboardData.ReadBounded(original):Encoding.UTF8.GetBytes(AppData.Json.Serialize(new WorkDocument{schemaVersion=1,revision=0,items=new List<WorkItem>()}));
                string temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
                try{File.WriteAllBytes(temporary,bytes);File.Move(temporary,destination);}finally{if(File.Exists(temporary))File.Delete(temporary);}
            }
            DashboardData.Atomic(Config(profile),Encoding.UTF8.GetBytes(AppData.Json.Serialize(new ChecklistLocationChoice{schemaVersion=1,path=destination})));
        }finally{for(int i=locks.Count-1;i>=0;i--){locks[i].ReleaseMutex();locks[i].Dispose();}}
    }
}
