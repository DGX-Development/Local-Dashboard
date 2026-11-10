using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

internal sealed class BackupTaskCopy {
    public string format {get;set;}
    public string status {get;set;}
    public string data {get;set;}
    public string sha256 {get;set;}
}
internal sealed class SafeRestoreJournal {
    public int schemaVersion {get;set;}
    public Dictionary<string,string> before {get;set;}
    public Dictionary<string,string> after {get;set;}
}
internal sealed class RestoredTaskCopies {
    internal string Agent,Personal,Checklist;
    internal string Folder {get{return Path.GetDirectoryName(Agent??Personal??Checklist);}}
}
internal enum RestoreListStatus { Unset, Unavailable, Available }

internal static partial class DashboardData {
    static readonly string[] SafeNames={"catalog.json",".local/window-preferences.json",".local/sidebar-layout.json",".local/pins.json",".local/work-list-source.json",".local/checklist-location.json",".local/personal-tasks-source.json"};
    static string SafeFile(string profile,string name){
        if(!SafeNames.Contains(name)&&name!=".local/safe-restore-pending.json")throw new IOException("Unsupported restore file.");
        string path=Path.GetFullPath(Path.Combine(profile,name.Replace('/',Path.DirectorySeparatorChar)));
        GuardPath(path);return path;
    }
    static void GuardPath(string path){
        for(var dir=new DirectoryInfo(Path.GetDirectoryName(path));dir!=null;dir=dir.Parent)if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Backup and restore folders must not be redirected.");
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Backup and restore files must not be redirected.");
    }
    static BackupTaskCopy MissingCopy(string format,string status){return new BackupTaskCopy{format=format,status=status};}
    static BackupTaskCopy ReadTaskCopy(string path,string format,bool emptyChecklist){
        try{
            byte[] bytes;
            if(!File.Exists(path)){
                if(!emptyChecklist)return MissingCopy(format,"unavailable");
                bytes=Utf8.GetBytes(AppData.Json.Serialize(new WorkDocument{schemaVersion=1,revision=0,items=new List<WorkItem>()}));
            }else bytes=ReadBounded(path,format=="json"?2097152:1048576);
            Text(bytes);return new BackupTaskCopy{format=format,status="included",data=Convert.ToBase64String(bytes),sha256=Hash(bytes)};
        }catch(IOException){return MissingCopy(format,"unavailable");}catch(UnauthorizedAccessException){return MissingCopy(format,"unavailable");}
    }
    static void ValidateTaskCopies(DashboardBackup bundle,List<Entry> entries,string check){
        if(bundle.schemaVersion<3)return;
        string[] roles=bundle.schemaVersion>=4?new[]{"agent","personal","checklist"}:new[]{"agent","checklist"};
        if(bundle.lists==null||bundle.lists.Count!=roles.Length||roles.Any(role=>!bundle.lists.ContainsKey(role)))throw new IOException("Backup task copies are invalid.");
        foreach(string role in roles){
            var copy=bundle.lists[role];
            if(copy==null||(role=="checklist"?copy.format!="json":copy.format!="txt"&&copy.format!="md")||!new[]{"included","unavailable","online"}.Contains(copy.status))throw new IOException("Backup task copy is invalid.");
            if(copy.status!="included"){if(copy.data!=null||copy.sha256!=null)throw new IOException("Unavailable task copy contains unexpected data.");continue;}
            if(copy.data==null)throw new IOException("Backup task copy is missing.");
            byte[] bytes=Convert.FromBase64String(copy.data);
            if(bytes.Length>(role=="checklist"?2097152:1048576)||Hash(bytes)!=copy.sha256)throw new IOException("Backup task copy checksum or size is invalid.");
            Text(bytes);
            if(role=="checklist"){string file=Path.Combine(check,"task-copy.json");File.WriteAllBytes(file,bytes);new ChecklistStore(file,entries).Read();}
        }
    }
    internal static DashboardBackup ExportWithLists(string profile,string destination,bool recovery=false){
        destination=Path.GetFullPath(destination);GuardPath(destination);
        using(Lock(ChecklistLocation.Config(profile)))using(new Lease(SafeNames.Select(n=>SafeFile(profile,n)))){
            string personal=ChecklistLocation.PathFor(profile);WorkSourceChoice source=null;
            try{source=WorkSources.Read(profile);}catch(IOException){}
            WorkSourceChoice personalSource=null;try{personalSource=WorkSources.Read(profile,true);}catch(IOException){}
            if(!recovery&&destination.StartsWith(Path.GetFullPath(profile).TrimEnd('\\')+"\\.local\\",StringComparison.OrdinalIgnoreCase)||SafeNames.Any(n=>String.Equals(destination,SafeFile(profile,n),StringComparison.OrdinalIgnoreCase))||String.Equals(destination,personal,StringComparison.OrdinalIgnoreCase)||source!=null&&String.Equals(destination,source.path,StringComparison.OrdinalIgnoreCase)||personalSource!=null&&String.Equals(destination,personalSource.path,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a backup destination separate from Dashboard and your task lists.");
            var bundle=new DashboardBackup{product="Local Dashboard",schemaVersion=5,createdAt=DateTimeOffset.UtcNow.ToString("o"),files=new Dictionary<string,string>(),hashes=new Dictionary<string,string>(),lists=new Dictionary<string,BackupTaskCopy>()};
            foreach(string name in Names.Concat(new[]{".local/personal-tasks-source.json"})){
                byte[] bytes=null;
                if(name!=".local/current-work.json"&&name!=".local/agent-work.txt"){
                    string file=name==".local/personal-tasks-source.json"?SafeFile(profile,name):FileFor(profile,name);if(File.Exists(file))bytes=ReadBounded(file);
                }
                bundle.files[name]=bytes==null?null:Convert.ToBase64String(bytes);bundle.hashes[name]=Hash(bytes);
            }
            using(Lock(personal))bundle.lists["checklist"]=ReadTaskCopy(personal,"json",!File.Exists(ChecklistLocation.Config(profile)));
            bundle.lists["agent"]=source==null?MissingCopy("txt","unavailable"):CloudSources.IsCloud(source.kind)?MissingCopy("txt","online"):ReadTaskCopy(source.path,Path.GetExtension(source.path).Equals(".md",StringComparison.OrdinalIgnoreCase)?"md":"txt",false);
            bundle.lists["personal"]=personalSource==null?MissingCopy("txt","unavailable"):CloudSources.IsCloud(personalSource.kind)?MissingCopy("txt","online"):ReadTaskCopy(personalSource.path,Path.GetExtension(personalSource.path).Equals(".md",StringComparison.OrdinalIgnoreCase)?"md":"txt",false);
            bundle.icons=CaptureBackupIcons(profile,Decode(bundle));
            Validate(profile,bundle);
            var bytesToSave=Utf8.GetBytes(AppData.Json.Serialize(bundle));
            if(bytesToSave.Length>16777216)throw new IOException("Backup exceeds the 16 MB limit. Choose smaller custom images before backing up.");
            Atomic(destination,bytesToSave);Validate(profile,Load(destination));return bundle;
        }
    }
    internal static string CreateRestoreRecoveryBackup(string profile)
    {
        string recovery=Path.Combine(profile,".local","recovery","Before restore "+
            DateTime.Now.ToString("yyyy-MM-dd HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8)+".json");
        ExportWithLists(profile,recovery,true);
        return recovery;
    }

    internal static string TaskCopySummary(DashboardBackup backup){
        var copies=TaskCopies(backup);
        return String.Join("\r\n",new[]{"agent","personal","checklist"}.Select(role=>(role=="agent"?"Agent tasks: ":role=="personal"?"Personal tasks: ":"Checklist: ")+(copies[role].status=="included"?"copy included.":copies[role].status=="online"?"not included; direct online access is unavailable.":"not included; the file was unavailable or this older backup did not contain it.")));
    }
    internal static Dictionary<string,BackupTaskCopy> TaskCopies(DashboardBackup bundle){
        if(bundle.schemaVersion>=4)return bundle.lists;
        if(bundle.schemaVersion==3){var old=new Dictionary<string,BackupTaskCopy>(bundle.lists);old["personal"]=MissingCopy("txt","unavailable");return old;}
        var files=Decode(bundle);var result=new Dictionary<string,BackupTaskCopy>();
        foreach(string role in new[]{"agent","checklist"}){
            byte[] bytes=files[role=="agent"?".local/agent-work.txt":".local/current-work.json"];
            result[role]=bytes==null?MissingCopy(role=="agent"?"txt":"json","unavailable"):new BackupTaskCopy{format=role=="agent"?"txt":"json",status="included",data=Convert.ToBase64String(bytes),sha256=Hash(bytes)};
        }
        result["personal"]=MissingCopy("txt","unavailable");return result;
    }
    internal static void CheckBackup(string profile,DashboardBackup bundle){Validate(profile,bundle);}
    internal static RestoredTaskCopies RestoreCopies(string profile,DashboardBackup bundle,bool useCopies,Action<int> fault=null){
        return RestoreCopies(profile,bundle,useCopies?new[]{"agent","personal","checklist"}:new string[0],fault);
    }
    internal static RestoredTaskCopies RestoreCopies(string profile,DashboardBackup bundle,IEnumerable<string> useCopies,Action<int> fault=null){
        Validate(profile,bundle);
        using(new Lease(SafeNames.Select(n=>SafeFile(profile,n)))){
            RecoverSafeCore(profile);RecoverCore(profile);
            var incoming=Decode(bundle);var before=new Dictionary<string,string>();var after=new Dictionary<string,string>();
            foreach(string name in SafeNames){string file=SafeFile(profile,name);before[name]=File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;after[name]=before[name];}
            RestoreBackupIcons(profile,bundle,incoming);
            foreach(string name in SafeNames.Take(4))after[name]=incoming[name]==null?null:Convert.ToBase64String(incoming[name]);
            if(after[".local/window-preferences.json"]!=null){
                var raw=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(Convert.FromBase64String(after[".local/window-preferences.json"])));
                var old=before[".local/window-preferences.json"]==null?new WindowPreferences():AppData.Json.Deserialize<WindowPreferences>(Text(Convert.FromBase64String(before[".local/window-preferences.json"])));
                raw["startWithWindows"]=old.startWithWindows;after[".local/window-preferences.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(raw)));
            }else if(before[".local/window-preferences.json"]!=null&&AppData.Json.Deserialize<WindowPreferences>(Text(Convert.FromBase64String(before[".local/window-preferences.json"]))).startWithWindows){
                after[".local/window-preferences.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(new WindowPreferences{closeToTray=true,minimizeToTray=true,startWithWindows=true})));
            }
            if(after[".local/sidebar-layout.json"]!=null){
                var raw=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(Convert.FromBase64String(after[".local/sidebar-layout.json"])));
                long revision=Convert.ToInt64(raw["revision"]);if(before[".local/sidebar-layout.json"]!=null){var old=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(Convert.FromBase64String(before[".local/sidebar-layout.json"])));revision=Math.Max(revision,Convert.ToInt64(old["revision"]));}
                if(revision==Int64.MaxValue)throw new IOException("Data revision limit reached.");raw["revision"]=revision+1;after[".local/sidebar-layout.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(raw)));
            }
            var result=new RestoredTaskCopies();var copies=TaskCopies(bundle);
            string folder=Path.Combine(profile,".local","restored-lists",DateTime.UtcNow.ToString("yyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,8));GuardPath(Path.Combine(folder,"check"));
            if(copies.Values.Any(copy=>copy.status=="included")&&(Directory.Exists(folder)||Path.Combine(folder,"My Checklist.json").Length>218))throw new IOException("The restored-list destination cannot safely hold editable copies. No original list was changed.");
            foreach(string role in new[]{"agent","personal","checklist"})if(copies[role].status=="included"){
                Directory.CreateDirectory(folder);string file=Path.Combine(folder,role=="agent"?"Agent Tasks."+copies[role].format:role=="personal"?"Personal Tasks."+copies[role].format:"My Checklist.json");
                using(var stream=new FileStream(file,FileMode.CreateNew,FileAccess.Write,FileShare.None)){var bytes=Convert.FromBase64String(copies[role].data);stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                if(Hash(ReadBounded(file))!=copies[role].sha256)throw new IOException("Restored task copy could not be verified. Original lists are unchanged.");
                if(role=="agent")result.Agent=file;else if(role=="personal")result.Personal=file;else result.Checklist=file;
            }
            {
                if(result.Agent!=null&&useCopies.Contains("agent"))after[".local/work-list-source.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(new WorkSourceChoice{schemaVersion=1,kind="local",path=result.Agent})));
                if(result.Personal!=null&&useCopies.Contains("personal"))after[".local/personal-tasks-source.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(new WorkSourceChoice{schemaVersion=1,kind="local",path=result.Personal})));
                if(result.Checklist!=null&&useCopies.Contains("checklist"))after[".local/checklist-location.json"]=Convert.ToBase64String(Utf8.GetBytes(AppData.Json.Serialize(new ChecklistLocationChoice{schemaVersion=1,path=result.Checklist})));
            }
            string pending=SafeFile(profile,".local/safe-restore-pending.json");
            Atomic(pending,Utf8.GetBytes(AppData.Json.Serialize(new SafeRestoreJournal{schemaVersion=1,before=before,after=after})));
            try{
                int i=0;foreach(string name in SafeNames){string file=SafeFile(profile,name);string current=File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;if(current!=before[name])throw new IOException("Dashboard configuration changed during restore. Existing edits were preserved.");WriteOptional(file,after[name]==null?null:Convert.FromBase64String(after[name]));if(fault!=null)fault(++i);}
                File.Delete(pending);
            }catch{RecoverSafeCore(profile);throw;}
            return result;
        }
    }
    static IEnumerable<Dictionary<string,object>> IconRows(object document)
    {
        var sidebar=document as Dictionary<string,object>;
        if(sidebar!=null&&!sidebar.TryGetValue("items",out document))yield break;
        var rows=document as System.Collections.IEnumerable;
        if(rows==null)yield break;
        foreach(var row in rows)
        {
            var item=row as Dictionary<string,object>;
            if(item!=null&&item.ContainsKey("iconPath")&&item["iconPath"] is string)yield return item;
        }
    }

    static Dictionary<string,BackupTaskCopy> CaptureBackupIcons(string profile,Dictionary<string,byte[]> files)
    {
        var result=new Dictionary<string,BackupTaskCopy>(StringComparer.OrdinalIgnoreCase);
        string root=Path.GetFullPath(profile);
        var owned=new[]{"project-icons","website-icons","restored-icons"}.Select(folder=>Path.Combine(root,".local",folder)+"\\").ToArray();
        foreach(string name in new[]{"catalog.json",".local/sidebar-layout.json"})
        {
            if(files[name]==null)continue;
            foreach(var row in IconRows(AppData.Json.DeserializeObject(Text(files[name]))))
            {
                string reference=(string)row["iconPath"];
                if(!Path.IsPathRooted(reference)||result.ContainsKey(reference))continue;
                string path=Path.GetFullPath(reference);
                string format=Path.GetExtension(path).ToLowerInvariant();
                if(!owned.Any(folder=>path.StartsWith(folder,StringComparison.OrdinalIgnoreCase))||!IconFormats.Contains(format))continue;
                GuardPath(path);
                if(!File.Exists(path)){result[reference]=MissingCopy(format,"unavailable");continue;}
                var bytes=ReadBounded(path);
                result[reference]=new BackupTaskCopy{format=format,status="included",data=Convert.ToBase64String(bytes),sha256=Hash(bytes)};
            }
        }
        return result;
    }

    static readonly string[] IconFormats={".png",".ico",".jpg",".jpeg",".bmp",".gif",".tif",".tiff",".webp"};

    static void ValidateBackupIcons(DashboardBackup bundle,Dictionary<string,byte[]> files)
    {
        if(bundle.schemaVersion<5)return;
        if(bundle.icons==null||bundle.icons.Count>600)throw new IOException("Invalid backup icon inventory.");
        var references=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string name in new[]{"catalog.json",".local/sidebar-layout.json"})
            if(files[name]!=null)
                foreach(var row in IconRows(AppData.Json.DeserializeObject(Text(files[name]))))references.Add((string)row["iconPath"]);
        var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in bundle.icons)
        {
            var icon=item.Value;
            if(!unique.Add(item.Key)||!references.Contains(item.Key)||icon==null||!IconFormats.Contains(icon.format)||
                (icon.status!="included"&&icon.status!="unavailable"))throw new IOException("Invalid backup icon record.");
            if(icon.status=="unavailable")
            {
                if(icon.data!=null||icon.sha256!=null)throw new IOException("Invalid unavailable icon record.");
                continue;
            }
            if(icon.data==null)throw new IOException("Backup icon data is missing.");
            var bytes=Convert.FromBase64String(icon.data);
            if(bytes.Length==0||bytes.Length>2097152||Hash(bytes)!=icon.sha256)throw new IOException("Backup icon checksum or size is invalid.");
        }
    }

    static void RestoreBackupIcons(string profile,DashboardBackup bundle,Dictionary<string,byte[]> files)
    {
        if(bundle.schemaVersion<5)return;
        var restored=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        string folder=Path.Combine(profile,".local","restored-icons",Guid.NewGuid().ToString("N"));
        foreach(var item in bundle.icons.Where(icon=>icon.Value.status=="included"))
        {
            string path=Path.Combine(folder,restored.Count.ToString()+item.Value.format);
            GuardPath(path);
            Directory.CreateDirectory(folder);
            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {
                var bytes=Convert.FromBase64String(item.Value.data);
                output.Write(bytes,0,bytes.Length);
                output.Flush(true);
            }
            if(Hash(ReadBounded(path))!=item.Value.sha256)throw new IOException("Restored icon could not be verified.");
            restored.Add(item.Key,path);
        }
        if(restored.Count==0)return;
        foreach(string name in new[]{"catalog.json",".local/sidebar-layout.json"})
        {
            if(files[name]==null)continue;
            var document=AppData.Json.DeserializeObject(Text(files[name]));
            bool changed=false;
            foreach(var row in IconRows(document))
            {
                string path;
                if(!restored.TryGetValue((string)row["iconPath"],out path))continue;
                row["iconPath"]=path;
                changed=true;
            }
            if(changed)
            {
                var bytes=Utf8.GetBytes(AppData.Json.Serialize(document));
                if(bytes.Length>2097152)throw new IOException("Restored configuration exceeds its size limit.");
                files[name]=bytes;
            }
        }
    }

    internal static string BackupIconSummary(DashboardBackup backup)
    {
        if(backup.schemaVersion<5)return "This older backup contains icon paths only.";
        int included=backup.icons.Values.Count(icon=>icon.status=="included");
        int missing=backup.icons.Count-included;
        return included+" Dashboard-owned icon files included"+(missing==0?".":". "+missing+" were unavailable.");
    }

    internal static RestoreListStatus CurrentRestoreList(string profile,string role,List<Entry> entries)
    {
        try
        {
            if(role=="checklist")
            {
                var path=ChecklistLocation.PathFor(profile);
                return File.Exists(path)&&new ChecklistStore(path,entries).Read()!=null?RestoreListStatus.Available:File.Exists(ChecklistLocation.Config(profile))?RestoreListStatus.Unavailable:RestoreListStatus.Unset;
            }
            var source=WorkSources.Read(profile,role=="personal");
            if(CloudSources.IsCloud(source.kind))return RestoreListStatus.Unavailable;
            return Text(ReadBounded(source.path,1048576))!=null?RestoreListStatus.Available:RestoreListStatus.Unavailable;
        }
        catch(TaskSourceNotSelectedException){return RestoreListStatus.Unset;}
        catch(IOException){return RestoreListStatus.Unavailable;}
        catch(UnauthorizedAccessException){return RestoreListStatus.Unavailable;}
        catch(ArgumentException){return RestoreListStatus.Unavailable;}
    }
    static void RecoverSafeCore(string profile){
        string pending=SafeFile(profile,".local/safe-restore-pending.json");if(!File.Exists(pending))return;
        var journal=AppData.Json.Deserialize<SafeRestoreJournal>(Text(ReadBounded(pending,33554432)));
        if(journal!=null&&journal.schemaVersion==1&&journal.before!=null&&journal.after!=null&&journal.before.Count==SafeNames.Length-1&&journal.after.Count==SafeNames.Length-1&&!journal.before.ContainsKey(".local/personal-tasks-source.json")&&!journal.after.ContainsKey(".local/personal-tasks-source.json")){string path=SafeFile(profile,".local/personal-tasks-source.json");string retained=File.Exists(path)?Convert.ToBase64String(ReadBounded(path)):null;journal.before[".local/personal-tasks-source.json"]=retained;journal.after[".local/personal-tasks-source.json"]=retained;}
        if(journal==null||journal.schemaVersion!=1||journal.before==null||journal.after==null||journal.before.Count!=SafeNames.Length||journal.after.Count!=SafeNames.Length||SafeNames.Any(n=>!journal.before.ContainsKey(n)||!journal.after.ContainsKey(n)))throw new IOException("Interrupted restore record is invalid; recovery files were kept.");
        foreach(string name in SafeNames){
            foreach(string value in new[]{journal.before[name],journal.after[name]})if(value!=null&&Convert.FromBase64String(value).Length>2097152)throw new IOException("Interrupted restore record exceeds its limit.");
            string file=SafeFile(profile,name);string valueNow=File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;
            if(valueNow!=journal.before[name]&&valueNow!=journal.after[name])throw new IOException("Configuration changed after an interrupted restore. Files were kept for recovery; no list was overwritten.");
        }
        foreach(string name in SafeNames)
        {
            string file=SafeFile(profile,name);
            string current=File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;
            if(current!=journal.before[name])WriteOptional(file,journal.before[name]==null?null:Convert.FromBase64String(journal.before[name]));
        }
        File.Delete(pending);
    }
    internal static void SaveDashboardEdit(string profile,CatalogDraft catalog,SidebarStore sidebar,SidebarSnapshot snapshot,SidebarLayout layout,Action<int> fault=null)
    {
        using(new Lease(SafeNames.Select(name=>SafeFile(profile,name))))
        {
            RecoverSafeCore(profile);
            var before=SafeNames.ToDictionary(name=>name,name=>{
                string file=SafeFile(profile,name);
                return File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;
            });
            var after=new Dictionary<string,string>(before);
            string links=catalog.Prepare();
            if(links!=null)
            {
                byte[] bytes=Utf8.GetBytes(links);
                if(bytes.Length>2097152)throw new IOException("Catalog exceeds its size limit.");
                after["catalog.json"]=Convert.ToBase64String(bytes);
            }
            if(layout!=null)after[".local/sidebar-layout.json"]=Convert.ToBase64String(Utf8.GetBytes(sidebar.Prepare(snapshot,layout)));
            else if(sidebar.Read().Token!=snapshot.Token)throw new IOException("Categories changed elsewhere. Reopen Edit Dashboard.");
            var changed=SafeNames.Where(name=>after[name]!=before[name]).ToList();
            if(changed.Count==0)return;
            string pending=SafeFile(profile,".local/safe-restore-pending.json");
            Atomic(pending,Utf8.GetBytes(AppData.Json.Serialize(new SafeRestoreJournal{schemaVersion=1,before=before,after=after})));
            try
            {
                int written=0;
                foreach(string name in changed)
                {
                    string file=SafeFile(profile,name);
                    string current=File.Exists(file)?Convert.ToBase64String(ReadBounded(file)):null;
                    if(current!=before[name])throw new IOException("Dashboard data changed during Save. Reopen Edit Dashboard.");
                    Atomic(file,Convert.FromBase64String(after[name]));
                    if(fault!=null)fault(++written);
                }
                File.Delete(pending);
            }
            catch{RecoverSafeCore(profile);throw;}
        }
    }
    internal static void RecoverSafe(string profile){using(new Lease(SafeNames.Select(n=>SafeFile(profile,n))))RecoverSafeCore(profile);}
}
