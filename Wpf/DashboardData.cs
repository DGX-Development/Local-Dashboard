using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Threading;

internal sealed class DashboardBackup {
    public string product {get;set;}
    public int schemaVersion {get;set;}
    public string createdAt {get;set;}
    public Dictionary<string,string> files {get;set;}
    public Dictionary<string,string> hashes {get;set;}
    public Dictionary<string,BackupTaskCopy> lists {get;set;}
    public Dictionary<string,BackupTaskCopy> icons {get;set;}
}
internal static partial class DashboardData {
    internal static readonly string[] Names={"catalog.json",".local/window-preferences.json",".local/sidebar-layout.json",".local/pins.json",".local/current-work.json",".local/agent-work.txt",".local/work-list-source.json"};
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    internal static string WorkPath(string profile){return Path.Combine(profile,".local","agent-work.txt");}
    internal static string Hash(byte[] bytes){if(bytes==null)return null;using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");}
    internal static string FileFor(string profile,string name){
        if(!Names.Contains(name)&&name!=".local/current-work-source.json"&&name!=".local/restore-pending.json")throw new IOException("Unsupported data file.");
        string path=name==".local/current-work.json"?ChecklistLocation.PathFor(profile):Path.GetFullPath(Path.Combine(profile,name.Replace('/',Path.DirectorySeparatorChar)));
        for(var dir=new DirectoryInfo(Path.GetDirectoryName(path));dir!=null;dir=dir.Parent)if(dir.Exists&&(dir.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Data folders must not be redirected.");
        if(File.Exists(path)&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("Data files must not be redirected.");
        return path;
    }
    internal static byte[] ReadBounded(string file,int limit=2097152){
        using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read)){
            if(stream.Length>limit)throw new IOException("Data file exceeds its size limit.");
            using(var output=new MemoryStream()){stream.CopyTo(output);if(output.Length>limit)throw new IOException("Data file exceeds its size limit.");return output.ToArray();}
        }
    }
    internal static void Atomic(string path,byte[] bytes){
        string directory=Path.GetDirectoryName(path);
        Directory.CreateDirectory(directory);
        string temp=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
        try{File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static string Text(byte[] bytes){using(var r=new StreamReader(new MemoryStream(bytes),Utf8,true))return r.ReadToEnd();}
    internal static byte[] CatalogImportData(string source){
        byte[] bytes=ReadBounded(source,16777216);string text=Text(bytes);object parsed;
        try{parsed=AppData.Json.DeserializeObject(text);}catch(ArgumentException){return null;}
        if(!(parsed is System.Collections.IList))return null;
        if(bytes.Length>2097152)throw new IOException("Catalog exceeds its size limit.");
        var rows=AppData.Json.Deserialize<List<Dictionary<string,object>>>(text);
        ValidateCatalog(text,Path.GetDirectoryName(Path.GetFullPath(source)));
        bool changed=false;
        foreach(var row in rows){
            foreach(Dictionary<string,object> target in (System.Collections.IList)row["targets"]){
                object raw;if(target.TryGetValue("path",out raw)&&raw is string&&((string)raw).StartsWith("@app/",StringComparison.Ordinal)){
                    target["path"]=Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Path.GetFullPath(source)),((string)raw).Substring(5).Replace('/',Path.DirectorySeparatorChar)));changed=true;
                }
            }
        }
        return changed?Utf8.GetBytes(AppData.Json.Serialize(rows)):bytes;
    }
    internal static int CatalogCount(byte[] bytes,string root){return ValidateCatalog(Text(bytes),root).Count;}
    internal static List<Entry> ValidateCatalog(string json,string root){
        List<Entry> entries;
        try{entries=AppData.ReadEntries(json,root);}catch(Exception error){throw new IOException("This file does not contain a valid dashboard catalog.",error);}
        if(entries.Count>1000||entries.Any(e=>e==null||String.IsNullOrWhiteSpace(e.id)||String.IsNullOrWhiteSpace(e.name)||e.targets==null||e.targets.Count>50))throw new IOException("Invalid catalog.");
        foreach(var entry in entries)foreach(var target in entry.targets){
            if(target==null||String.IsNullOrWhiteSpace(target.id))throw new IOException("Invalid destination.");
            if(target.kind=="web"){Uri uri;if(!Uri.TryCreate(target.url,UriKind.Absolute,out uri)||!new[]{"http","https"}.Contains(uri.Scheme)||target.url.Any(Char.IsControl)||target.url.Contains("\""))throw new IOException("Invalid website destination.");}
            else if(!new[]{"folder","document"}.Contains(target.kind)||String.IsNullOrWhiteSpace(target.path)||!Path.IsPathRooted(target.path)||target.path.Any(Char.IsControl)||target.path.Contains("\""))throw new IOException("Invalid local destination.");
        }
        return entries;
    }
    internal static string ImportCatalog(string profile,byte[] bytes,string expectedHash){
        ValidateCatalog(Text(bytes),profile);string file=FileFor(profile,"catalog.json");
        using(Lock(file)){
            byte[] before=ReadBounded(file);if(Hash(before)!=expectedHash)throw new IOException("Dashboard links changed while the import was being reviewed. Nothing was imported; choose the file again.");
            string backup=Path.Combine(profile,".local","catalog-before-import-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")+".json");
            GuardPath(backup);Atomic(backup,before);
            if(Hash(ReadBounded(file))!=expectedHash)throw new IOException("Dashboard links changed before import. Nothing was imported; the previous copy was retained.");
            Atomic(file,bytes);return backup;
        }
    }
    static IDisposable Lock(string path){return new Lease(new[]{path});}
    sealed class Lease:IDisposable{
        readonly List<Mutex> held=new List<Mutex>();
        internal Lease(IEnumerable<string> paths){try{foreach(string path in paths.OrderBy(p=>p,StringComparer.OrdinalIgnoreCase)){var mutex=new Mutex(false,ChecklistStore.LockName(path));bool acquired=false;try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}if(!acquired){mutex.Dispose();throw new IOException("Dashboard data is being edited. Try again.");}held.Add(mutex);}}catch{Dispose();throw;}}
        public void Dispose(){for(int i=held.Count-1;i>=0;i--){held[i].ReleaseMutex();held[i].Dispose();}held.Clear();}
    }
    internal static string MigrateWork(string profile,string legacyOverride=null){
        string target=FileFor(profile,".local/agent-work.txt"),config=FileFor(profile,".local/current-work-source.json");
        using(new Lease(new[]{target,config})){
            if(File.Exists(target)){Text(ReadBounded(target,1048576));return target;}
            string source=legacyOverride??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Current Work.txt");
            if(legacyOverride==null&&File.Exists(config)){
                var setting=AppData.Json.Deserialize<AssistantWorkSource>(Text(ReadBounded(config,16384)));
                if(setting==null||setting.schemaVersion!=1||String.IsNullOrWhiteSpace(setting.path)||!Path.IsPathRooted(setting.path))throw new IOException("Invalid previous work source; nothing was migrated.");
                source=setting.path;
            }
            if(!File.Exists(source))return target;
            if(!new[]{".txt",".md"}.Contains(Path.GetExtension(source).ToLowerInvariant()))throw new IOException("Work source must be text.");
            byte[] original=ReadBounded(source,1048576);Text(original);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string temp=target+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{
                File.WriteAllBytes(temp,original);
                if(Hash(ReadBounded(temp))!=Hash(original)||Hash(ReadBounded(source,1048576))!=Hash(original))throw new IOException("Work source changed during migration. Original retained.");
                File.Move(temp,target);
                if(Hash(ReadBounded(target))!=Hash(original))throw new IOException("Work migration verification failed. Original retained.");
            }finally{if(File.Exists(temp))File.Delete(temp);}
            return target;
        }
    }
    internal static DashboardBackup Snapshot(string profile){
        var bundle=new DashboardBackup{product="Local Dashboard",schemaVersion=2,createdAt=DateTimeOffset.UtcNow.ToString("o"),files=new Dictionary<string,string>(),hashes=new Dictionary<string,string>()};
        foreach(string name in Names){string file=FileFor(profile,name);byte[] bytes=File.Exists(file)?ReadBounded(file):null;bundle.files[name]=bytes==null?null:Convert.ToBase64String(bytes);bundle.hashes[name]=Hash(bytes);}
        return bundle;
    }
    static Dictionary<string,byte[]> Decode(DashboardBackup bundle){
        if(bundle==null||bundle.product!="Local Dashboard"||(bundle.schemaVersion!=1&&bundle.schemaVersion!=2&&bundle.schemaVersion!=3&&bundle.schemaVersion!=4&&bundle.schemaVersion!=5)||bundle.files==null||bundle.hashes==null)throw new IOException("Unsupported Dashboard backup.");
        string[] expected=bundle.schemaVersion==1?Names.Where(n=>n!=".local/work-list-source.json").ToArray():bundle.schemaVersion>=4?Names.Concat(new[]{".local/personal-tasks-source.json"}).ToArray():Names;
        if(bundle.files.Count!=expected.Length||bundle.hashes.Count!=expected.Length||expected.Any(n=>!bundle.files.ContainsKey(n)||!bundle.hashes.ContainsKey(n)))throw new IOException("Unsupported Dashboard backup.");
        var result=new Dictionary<string,byte[]>();
        foreach(string name in expected){byte[] bytes=bundle.files[name]==null?null:Convert.FromBase64String(bundle.files[name]);if(bytes!=null&&bytes.Length>2097152||Hash(bytes)!=bundle.hashes[name])throw new IOException("Backup checksum or size is invalid.");result[name]=bytes;}
        if(!result.ContainsKey(".local/work-list-source.json"))result[".local/work-list-source.json"]=null;
        if(result["catalog.json"]==null)throw new IOException("Backup has no catalog.");return result;
    }
    static void Validate(string profile,DashboardBackup bundle){
        var files=Decode(bundle);string check=Path.Combine(profile,".local","backup-check-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(check);
        try{
            foreach(var item in files)if(item.Value!=null){string path=Path.Combine(check,Path.GetFileName(item.Key));File.WriteAllBytes(path,item.Value);}
            var entries=ValidateCatalog(Text(files["catalog.json"]),profile);
            if(files[".local/work-list-source.json"]!=null)WorkSources.Validate(AppData.Json.Deserialize<WorkSourceChoice>(Text(files[".local/work-list-source.json"])));
            if(files.ContainsKey(".local/personal-tasks-source.json")&&files[".local/personal-tasks-source.json"]!=null)WorkSources.Validate(AppData.Json.Deserialize<WorkSourceChoice>(Text(files[".local/personal-tasks-source.json"])));
            if(files[".local/current-work.json"]!=null)new ChecklistStore(Path.Combine(check,"current-work.json"),entries).Read();
            if(files[".local/sidebar-layout.json"]!=null)new SidebarStore(Path.Combine(check,"sidebar-layout.json")).Read();
            if(files[".local/pins.json"]!=null){var pins=AppData.Json.Deserialize<string[]>(Text(files[".local/pins.json"]));if(pins==null||pins.Length>1000||pins.Any(p=>String.IsNullOrWhiteSpace(p)||p.Length>100))throw new IOException("Invalid favorites.");}
            if(files[".local/window-preferences.json"]!=null){
                var raw=AppData.Json.DeserializeObject(Text(files[".local/window-preferences.json"])) as Dictionary<string,object>;
                if(raw==null)throw new IOException("Invalid preferences.");if(raw.ContainsKey("theme")&&(!(raw["theme"] is string)||!DashboardTheme.Valid((string)raw["theme"])))throw new IOException("Invalid theme preference.");
                foreach(string key in new[]{"closeToTray","minimizeToTray","startWithWindows","startMinimized"})if(raw.ContainsKey(key)&&!(raw[key] is bool))throw new IOException("Invalid window preference.");
                System.Drawing.Color color;if(raw.ContainsKey("accentColor")&&raw["accentColor"]!=null&&!DashboardAccent.TryParse(raw["accentColor"] as string,out color))throw new IOException("Invalid accent color.");
            }
            if(files[".local/agent-work.txt"]!=null){if(files[".local/agent-work.txt"].Length>1048576)throw new IOException("Agent Work is too large.");Text(files[".local/agent-work.txt"]);}
            ValidateTaskCopies(bundle,entries,check);
            ValidateBackupIcons(bundle,files);
        }finally{Directory.Delete(check,true);}
    }
    internal static void Export(string profile,string destination){
        destination=Path.GetFullPath(destination);
        if(Names.Any(n=>String.Equals(destination,FileFor(profile,n),StringComparison.OrdinalIgnoreCase))||destination.StartsWith(Path.Combine(profile,".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Choose a backup destination outside the data folder.");
        using(Lock(ChecklistLocation.Config(profile)))using(new Lease(Names.Select(n=>FileFor(profile,n)))){
            ChecklistLocation.RequireAvailable(profile);var bundle=Snapshot(profile);Validate(profile,bundle);byte[] bytes=Utf8.GetBytes(AppData.Json.Serialize(bundle));
            Atomic(destination,bytes);Validate(profile,Load(destination));
        }
    }
    internal static DashboardBackup Load(string file){
        string json=Text(ReadBounded(file,16777216));
        object document;
        try{document=AppData.Json.DeserializeObject(json);}
        catch(ArgumentException error){throw new IOException("This file is not a readable Dashboard backup. Choose a JSON file created with File > Back up configuration. Your current data has not been changed.",error);}
        if(document is object[]||document is System.Collections.ArrayList)
            throw new IOException("This file contains dashboard links only (catalog.json), not a configuration backup. In your previous Dashboard copy, use File > Back up configuration, then select that backup here. Your current data has not been changed.");
        if(!(document is Dictionary<string,object>))
            throw new IOException("This file does not contain a Dashboard configuration backup. Choose a file created with File > Back up configuration. Your current data has not been changed.");
        return AppData.Json.ConvertToType<DashboardBackup>(document);
    }
    internal static void Restore(string profile,DashboardBackup bundle,Action<int> fault=null){
        if(bundle!=null&&bundle.schemaVersion>=3)throw new IOException("Use the restore-copies workflow for this backup.");
        Validate(profile,bundle);
        using(Lock(ChecklistLocation.Config(profile)))using(new Lease(Names.Select(n=>FileFor(profile,n)))){
            RecoverCore(profile);
            var before=Snapshot(profile);var incoming=Decode(bundle);
            if(bundle.schemaVersion==1)incoming[".local/work-list-source.json"]=before.files[".local/work-list-source.json"]==null?null:Convert.FromBase64String(before.files[".local/work-list-source.json"]);
            // Windows startup remains a local machine choice, never enabled by importing a file.
            if(incoming[".local/window-preferences.json"]!=null){
                var raw=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(incoming[".local/window-preferences.json"]));
                var old=File.Exists(FileFor(profile,".local/window-preferences.json"))?AppData.Json.Deserialize<WindowPreferences>(File.ReadAllText(FileFor(profile,".local/window-preferences.json"))):new WindowPreferences();
                raw["startWithWindows"]=old.startWithWindows;incoming[".local/window-preferences.json"]=Utf8.GetBytes(AppData.Json.Serialize(raw));
            }
            foreach(string name in new[]{".local/current-work.json",".local/sidebar-layout.json"})if(incoming[name]!=null){
                var raw=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(incoming[name]));long revision=Convert.ToInt64(raw["revision"]);
                if(before.files[name]!=null){var old=(Dictionary<string,object>)AppData.Json.DeserializeObject(Text(Convert.FromBase64String(before.files[name])));revision=Math.Max(revision,Convert.ToInt64(old["revision"]));}
                if(revision==Int64.MaxValue)throw new IOException("Data revision limit reached.");raw["revision"]=revision+1;incoming[name]=Utf8.GetBytes(AppData.Json.Serialize(raw));
            }
            var after=new DashboardBackup{product="Local Dashboard",schemaVersion=2,files=incoming.ToDictionary(p=>p.Key,p=>p.Value==null?null:Convert.ToBase64String(p.Value)),hashes=incoming.ToDictionary(p=>p.Key,p=>Hash(p.Value))};Validate(profile,after);
            var journal=new Dictionary<string,DashboardBackup>{{"before",before},{"after",after}};
            string pending=FileFor(profile,".local/restore-pending.json");
            Atomic(pending,Utf8.GetBytes(AppData.Json.Serialize(journal)));
            try{int i=0;foreach(var item in incoming){WriteOptional(FileFor(profile,item.Key),item.Value);if(fault!=null)fault(++i);}File.Delete(pending);}
            catch{RecoverCore(profile);throw;}
        }
    }
    static void WriteOptional(string file,byte[] bytes){if(bytes==null){if(File.Exists(file))File.Delete(file);}else Atomic(file,bytes);}
    static void RecoverCore(string profile){
        string pending=FileFor(profile,".local/restore-pending.json");if(!File.Exists(pending))return;
        var journal=AppData.Json.Deserialize<Dictionary<string,DashboardBackup>>(Text(ReadBounded(pending,33554432)));
        if(journal==null||journal.Count!=2||!journal.ContainsKey("before")||!journal.ContainsKey("after"))throw new IOException("Invalid interrupted restore journal.");
        var before=Decode(journal["before"]);var after=Decode(journal["after"]);
        foreach(string name in Names){string file=FileFor(profile,name);string current=File.Exists(file)?Hash(ReadBounded(file)):null;if(current!=Hash(before[name])&&current!=Hash(after[name]))throw new IOException("Data changed after an interrupted restore. Recovery files were preserved.");}
        foreach(var item in before)WriteOptional(FileFor(profile,item.Key),item.Value);
        File.Delete(pending);
    }
    internal static void Recover(string profile){RecoverSafe(profile);using(Lock(ChecklistLocation.Config(profile)))using(new Lease(Names.Select(n=>FileFor(profile,n))))RecoverCore(profile);}
}
