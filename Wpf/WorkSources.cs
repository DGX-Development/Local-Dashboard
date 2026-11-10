using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

internal sealed class WorkSourceChoice {
    public int schemaVersion {get;set;}
    public string kind {get;set;}
    public string path {get;set;}
}
internal sealed class WorkSourceCache {
    public int schemaVersion {get;set;}
    public string source {get;set;}
    public string text {get;set;}
    public string hash {get;set;}
    public string refreshedAt {get;set;}
    public string rawSnapshot {get;set;}public string rawHash {get;set;}
    public string revision {get;set;}
    public List<SourceTaskRecord> records {get;set;}
}
internal sealed class TaskSourceNotSelectedException : IOException {
    internal TaskSourceNotSelectedException(bool personal):base("Choose a "+(personal?"Personal tasks":"Agent tasks")+" file in Settings > Tasks."){}
}
internal static class WorkSources {
    static readonly UTF8Encoding Utf8=new UTF8Encoding(false,true);
    internal static string Config(string profile,bool personal=false){return Path.Combine(profile,".local",personal?"personal-tasks-source.json":"work-list-source.json");}
    internal static string Token(string profile,bool personal=false){return File.Exists(Config(profile,personal))?DashboardData.Hash(DashboardData.ReadBounded(Config(profile,personal),16384)):null;}
    static bool Disconnected(WorkSourceChoice choice){return choice!=null&&choice.schemaVersion==1&&choice.kind=="none"&&String.IsNullOrEmpty(choice.path);}
    internal static void Validate(WorkSourceChoice choice){
        if(Disconnected(choice))return;
        if(choice!=null&&choice.schemaVersion==1&&CloudSources.IsCloud(choice.kind)){CloudSources.Validate(choice);return;}if(choice==null||choice.schemaVersion!=1||!new[]{"local","synced","network"}.Contains(choice.kind)||String.IsNullOrWhiteSpace(choice.path))throw new IOException("Choose a supported file source.");
        string p=choice.path;
        if(p.Any(Char.IsControl)||p.Contains("\"")||p.StartsWith(@"\\?\")||p.StartsWith(@"\\.\")||!Path.IsPathRooted(p)||(!p.StartsWith(@"\\")&&(p.Length<3||p[1]!=':'||p[2]!='\\')))throw new IOException("Enter a full Windows file path or UNC network path.");
        if(!new[]{".txt",".md"}.Contains(Path.GetExtension(p).ToLowerInvariant()))throw new IOException("Choose a TXT or Markdown file, not a folder or cloud URL.");
        if(choice.kind=="network"&&!p.StartsWith(@"\\"))throw new IOException(@"Use a UNC path such as \\server\share\work.md for Network share.");
        choice.path=Path.GetFullPath(p);
    }
    internal static WorkSourceChoice Read(string profile,bool personal=false){
        if(File.Exists(Config(profile,personal))){var c=AppData.Json.Deserialize<WorkSourceChoice>(Utf8.GetString(DashboardData.ReadBounded(Config(profile,personal),16384)));Validate(c);if(Disconnected(c))throw new TaskSourceNotSelectedException(personal);return c;}
        if(personal)throw new TaskSourceNotSelectedException(true);
        string path=DashboardData.WorkPath(profile),legacy=Path.Combine(profile,".local","current-work-source.json");
        if(!File.Exists(path)){
            path=null;
            if(File.Exists(legacy)){var c=AppData.Json.Deserialize<AssistantWorkSource>(File.ReadAllText(legacy));if(c==null||c.schemaVersion!=1)throw new IOException("Invalid existing source configuration.");path=c.path;}
        }
        if(String.IsNullOrWhiteSpace(path))throw new TaskSourceNotSelectedException(false);
        var result=new WorkSourceChoice{schemaVersion=1,kind=path!=null&&path.StartsWith(@"\\")?"network":"local",path=path};Validate(result);return result;
    }
    internal static string Identity(WorkSourceChoice choice){Validate(choice);return CloudSources.IsCloud(choice.kind)?choice.kind+":"+choice.path:choice.path.ToUpperInvariant();}
    internal static string CachePath(string profile,WorkSourceChoice choice){return Path.Combine(profile,".local","work-list-cache",DashboardData.Hash(Utf8.GetBytes(Identity(choice)))+".json");}
    internal static void Save(string profile,WorkSourceChoice choice,string expected,bool personal=false){
        if(choice==null)choice=new WorkSourceChoice{schemaVersion=1,kind="none"};
        Validate(choice);string config=Config(profile,personal);
        using(var mutex=new Mutex(false,ChecklistStore.LockName(config))){bool locked=false;try{
            try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
            if(!locked)throw new IOException("Source selection is busy. Try again.");
            if(Token(profile,personal)!=expected)throw new IOException("Source selection changed in another window. Reopen this dialog; nothing was overwritten.");
            DashboardData.Atomic(config,Utf8.GetBytes(AppData.Json.Serialize(choice)));
        }finally{if(locked)mutex.ReleaseMutex();}}
    }
    internal static WorkSourceCache Cached(string profile,WorkSourceChoice choice){
        string file=CachePath(profile,choice);if(!File.Exists(file))return null;
        var cache=AppData.Json.Deserialize<WorkSourceCache>(Utf8.GetString(DashboardData.ReadBounded(file,8388608)));
        DateTimeOffset date;
        if(cache==null||cache.schemaVersion!=1||cache.source!=Identity(choice)||cache.text==null||Utf8.GetByteCount(cache.text)>2097152||cache.hash!=DashboardData.Hash(Utf8.GetBytes(cache.text))||!DateTimeOffset.TryParse(cache.refreshedAt,out date))throw new IOException("Cached work list is invalid. Refresh from the original; the source was not changed.");
        if(choice.kind=="google-docs"&&cache.rawSnapshot==null)throw new IOException("Structured source snapshot is missing. Refresh the original.");if(cache.rawSnapshot!=null){if(cache.rawHash!=DashboardData.Hash(Utf8.GetBytes(cache.rawSnapshot)))throw new IOException("Structured snapshot checksum failed.");var parsed=CloudSources.ParseGoogle(cache.rawSnapshot);if(parsed.text!=cache.text)throw new IOException("Structured snapshot differs from its text. Refresh the original.");cache.records=parsed.records;cache.revision=parsed.revision;}else cache.records=null;return cache;
    }
    internal static async Task<bool> CompletesWithin(Task<WorkSourceCache> read,int milliseconds){return await Task.WhenAny(read,Task.Delay(milliseconds))==read;}
    internal static WorkSourceCache Fetch(WorkSourceChoice choice,string profile=null){
        if(CloudSources.IsCloud(choice.kind)){if(profile==null)throw new IOException("Provider reads require the selected data profile.");return CloudSources.Fetch(profile,choice);}
        Validate(choice);
        // Deny concurrent writes during this bounded read; the source is never opened for writing.
        byte[] bytes=DashboardData.ReadBounded(choice.path,1048576);string text;
        using(var reader=new StreamReader(new MemoryStream(bytes),Utf8,true))text=reader.ReadToEnd();
        if(text.IndexOf('\0')>=0||Utf8.GetByteCount(text)>2097152)throw new IOException("Work list is not supported text.");
        return new WorkSourceCache{schemaVersion=1,source=Identity(choice),text=text,hash=DashboardData.Hash(Utf8.GetBytes(text)),refreshedAt=DateTimeOffset.UtcNow.ToString("o")};
    }
    internal static void Store(string profile,WorkSourceChoice choice,string expected,WorkSourceCache cache,bool personal=false){
        string config=Config(profile,personal);using(var mutex=new Mutex(false,ChecklistStore.LockName(config))){bool locked=false;try{
            try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
            if(!locked||Token(profile,personal)!=expected||Identity(Read(profile,personal))!=Identity(choice))throw new IOException("Source changed while refreshing. Refresh the selected source; no cached result was replaced.");
            DashboardData.Atomic(CachePath(profile,choice),Utf8.GetBytes(AppData.Json.Serialize(cache)));
        }finally{if(locked)mutex.ReleaseMutex();}}
    }
    internal static readonly string ProviderHelp="Local file: choose an existing TXT or Markdown file.\n\nSynced folder: choose a local file managed by OneDrive, Dropbox or Google Drive for desktop. Their installed software handles sign-in and sync. Dashboard only reads the local file and cannot confirm cloud upload. Keep the file available offline. Native Google Docs shortcuts are not text files.\n\nNetwork share: choose an existing UNC file using your Windows permissions. No server or network settings are changed.\n\nGoogle Drive (including Google documents), Dropbox and OneDrive: read-only adapters and explicit browser sign-in require a registered application and consent; authenticated end-to-end verification remains pending. Tokens are memory-only. A chat connection does not connect this app.\n\nSFTP and HTTPS/WebDAV: unavailable in this build; require a verified server connection and authentication. Plain FTP is not offered.\n\nRefresh reads the selected original; it never uploads or edits it. Cached content is a last-read snapshot, not another master. Checklists remains separate.";
}
