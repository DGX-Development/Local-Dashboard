using System;
using System.IO;
using System.Text;
using System.Linq;
internal static class WorkSourceVerification {
    static void Check(bool yes,string message){if(!yes)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static void Reject(Action action,string message){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,message);}
    internal static void Run(string profile){
        var pending=new System.Threading.Tasks.TaskCompletionSource<WorkSourceCache>();
        Check(!WorkSources.CompletesWithin(pending.Task,10).GetAwaiter().GetResult(),"unresponsive read returns control at deadline without publishing");
        pending.SetResult(new WorkSourceCache());
        Check(WorkSources.CompletesWithin(pending.Task,10).GetAwaiter().GetResult(),"completed read is recognized");
        string dir=Path.Combine(profile,"SourceTests");if(Directory.Exists(dir))throw new IOException("Use a fresh source test profile.");Directory.CreateDirectory(dir);
        string a=Path.Combine(dir,"a.md"),b=Path.Combine(dir,"b.txt");File.WriteAllText(a,"# Plain Markdown\nAuthoritative content",new UTF8Encoding(false));File.WriteAllText(b,"Second source");
        string original=DashboardData.Hash(File.ReadAllBytes(a));
        var first=new WorkSourceChoice{schemaVersion=1,kind="local",path=a};var second=new WorkSourceChoice{schemaVersion=1,kind="synced",path=b};
        WorkSources.Save(profile,first,null);string token=WorkSources.Token(profile);
        var cache=WorkSources.Fetch(first);WorkSources.Store(profile,first,token,cache);
        Check(WorkSources.Cached(profile,first).text.Contains("Authoritative"),"read-only refresh and cache round trip");
        string cached=File.ReadAllText(WorkSources.CachePath(profile,first));
        using(var locked=new FileStream(a,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){Reject(()=>WorkSources.Fetch(first),"concurrent writer blocks refresh rather than exposing partial text");}
        Check(File.ReadAllText(WorkSources.CachePath(profile,first))==cached,"failed refresh preserves last good cache");
        WorkSources.Save(profile,second,token);string secondToken=WorkSources.Token(profile);
        Reject(()=>WorkSources.Save(profile,first,token),"stale source selection cannot overwrite a newer choice");
        Reject(()=>WorkSources.Store(profile,first,token,cache),"late response from old source cannot publish");
        Check(WorkSources.Cached(profile,second)==null,"source switching never shows another source cache");
        var secondCache=WorkSources.Fetch(second);WorkSources.Store(profile,second,secondToken,secondCache);
        File.Move(b,b+".offline");Reject(()=>WorkSources.Fetch(second),"offline source reports failure");
        Check(WorkSources.Cached(profile,second).text=="Second source","offline source retains correctly identified snapshot");
        File.Move(b+".offline",b);
        File.WriteAllBytes(b,new byte[]{0xFF,0xFF,0x00});Reject(()=>WorkSources.Fetch(second),"invalid text rejected");
        Check(WorkSources.Cached(profile,second).text=="Second source","invalid text cannot replace cache");
        File.WriteAllBytes(b,new byte[1048577]);Reject(()=>WorkSources.Fetch(second),"oversized file rejected");
        Reject(()=>WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="local",path="https://example.invalid/work.md"},secondToken),"cloud URL cannot pretend to be a local file");
        Reject(()=>WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="local",path=@"C:relative.md"},secondToken),"drive-relative paths rejected");
        Reject(()=>WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="google",path=a},secondToken),"unsupported provider cannot be selected as connected");
        var network=new WorkSourceChoice{schemaVersion=1,kind="network",path=@"\\example.invalid\share\work.md"};WorkSources.Validate(network);
        Check(network.path.StartsWith(@"\\"),"UNC syntax validated without contacting a server");
        File.WriteAllText(WorkSources.CachePath(profile,second),"{}");Reject(()=>WorkSources.Cached(profile,second),"damaged cache rejected instead of silently trusted");
        string backup=Path.Combine(dir,"backup.json");DashboardData.Export(profile,backup);
        var bundle=DashboardData.Load(backup);
        Check(bundle.schemaVersion==2&&!bundle.files.Keys.Any(k=>k.Contains("cache")),"backup includes source settings but excludes cached content");
        WorkSources.Save(profile,first,WorkSources.Token(profile));DashboardData.Restore(profile,bundle);
        Check(WorkSources.Read(profile).path==second.path,"source selection restores without reading external source");
        bundle.schemaVersion=1;bundle.files.Remove(".local/work-list-source.json");bundle.hashes.Remove(".local/work-list-source.json");
        WorkSources.Save(profile,first,WorkSources.Token(profile));DashboardData.Restore(profile,bundle);
        Check(WorkSources.Read(profile).path==first.path,"older backup preserves current source selection");
        Check(DashboardData.Hash(File.ReadAllBytes(a))==original,"authoritative original remains byte-identical");
        Check(!File.Exists(DashboardData.WorkPath(profile)),"no managed-file migration performed");
        Console.WriteLine("Source tests complete. Synthetic local files only; no network, cloud credentials or native windows used.");
    }
}