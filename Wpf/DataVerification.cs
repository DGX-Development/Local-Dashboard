using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
internal static class DataVerification {
    static void Check(bool condition,string label){if(!condition)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Reject(Action action,string label){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,label);}
    static Dictionary<string,string> Fingerprint(string profile){return DashboardData.Names.ToDictionary(n=>n,n=>{string f=DashboardData.FileFor(profile,n);return File.Exists(f)?DashboardData.Hash(File.ReadAllBytes(f)):null;});}
    static bool Same(Dictionary<string,string> a,Dictionary<string,string>b){return a.All(p=>b[p.Key]==p.Value);}
    internal static void Run(string profile){
        SafeBackupVerification.Run(profile);
        string source=Path.Combine(profile,"legacy-work.txt"),target=Path.Combine(profile,"Target");
        if(Directory.Exists(target))throw new IOException("Use a new test profile.");
        Directory.CreateDirectory(Path.Combine(target,".local"));
        File.Copy(Path.Combine(profile,"catalog.json"),Path.Combine(target,"catalog.json"));
        File.WriteAllText(source,"CURRENT WORK\r\nACTIVE\r\n1. Preserved task\r\n   Status: In progress\r\n   Next: Verify migration\r\n",new UTF8Encoding(false));
        byte[] original=File.ReadAllBytes(source);string migrated=DashboardData.MigrateWork(target,source);
        Check(File.ReadAllBytes(migrated).SequenceEqual(original)&&File.ReadAllBytes(source).SequenceEqual(original),"migration exact bytes; original retained");
        File.AppendAllText(source,"Later legacy edit");
        DashboardData.MigrateWork(target,source);
        Check(File.ReadAllBytes(migrated).SequenceEqual(original),"existing managed work is not overwritten by divergent legacy work");
        Check(AssistantWorkSource.Read(target).Path==migrated,"Agent Work reads managed file");
        var entries=AppData.ReadEntries(File.ReadAllText(Path.Combine(target,"catalog.json")));
        var checklist=new ChecklistStore(Path.Combine(target,".local","current-work.json"),entries);
        var doc=checklist.Upsert(new WorkPatch{expectedRevision=0,id="test-task",title="Preserved personal task"});
        checklist.Complete(doc.revision,"test-task",true);
        File.WriteAllText(Path.Combine(target,".local","window-preferences.json"),"{\"closeToTray\":true,\"minimizeToTray\":true,\"startWithWindows\":false,\"accentColor\":\"#FF6600\",\"futureSetting\":\"retain\"}");
        File.WriteAllText(Path.Combine(target,".local","pins.json"),"[\"test-favorite\"]");
        Directory.CreateDirectory(Path.Combine(target,".local","agent-reports"));
        string report=Path.Combine(target,".local","agent-reports","source.json");File.WriteAllText(report,"unchanged independent status");
        string backup=Path.Combine(profile,"backup.json");DashboardData.Export(target,backup);
        var bundle=DashboardData.Load(backup);
        Check(bundle.files.Keys.SequenceEqual(DashboardData.Names),"backup explicit file allowlist excludes status/source overrides");
        var before=Fingerprint(target);
        var broken=DashboardData.Load(backup);broken.hashes["catalog.json"]="bad";
        Reject(()=>DashboardData.Restore(target,broken),"tampered backup rejected");
        Check(Same(before,Fingerprint(target)),"rejected import changes no data");
        broken=DashboardData.Load(backup);broken.files["../escape"]=null;
        Reject(()=>DashboardData.Restore(target,broken),"unknown path rejected");
        var invalid=DashboardData.Load(backup);byte[] bad=Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"revision\":0,\"items\":[{\"id\":\"bad\"}]}");
        invalid.files[".local/current-work.json"]=Convert.ToBase64String(bad);invalid.hashes[".local/current-work.json"]=DashboardData.Hash(bad);
        Reject(()=>DashboardData.Restore(target,invalid),"invalid checklist with valid checksum rejected");
        Reject(()=>DashboardData.Restore(target,DashboardData.Load(backup),i=>{if(i==3)throw new IOException("Injected interruption");}),"mid-restore failure surfaced");
        Check(Same(before,Fingerprint(target)),"mid-restore rollback preserves every original file");
        var imported=DashboardData.Load(backup);var prefs=(Dictionary<string,object>)AppData.Json.DeserializeObject(Encoding.UTF8.GetString(Convert.FromBase64String(imported.files[".local/window-preferences.json"])));
        prefs["startWithWindows"]=true;byte[] incoming=Encoding.UTF8.GetBytes(AppData.Json.Serialize(prefs));imported.files[".local/window-preferences.json"]=Convert.ToBase64String(incoming);imported.hashes[".local/window-preferences.json"]=DashboardData.Hash(incoming);
        DashboardData.Restore(target,imported);
        var result=checklist.Read();
        Check(result.items.Single().state=="done"&&result.revision>2,"Done state preserved and stale checklist revision invalidated");
        Check(File.ReadAllText(Path.Combine(target,".local","window-preferences.json")).Contains("futureSetting")&&!AppData.Json.Deserialize<WindowPreferences>(File.ReadAllText(Path.Combine(target,".local","window-preferences.json"))).startWithWindows,"unknown settings preserved; import cannot enable Windows startup");
        Check(File.ReadAllText(report)=="unchanged independent status","participating report bytes unchanged");
        Check(File.ReadAllBytes(migrated).SequenceEqual(original),"managed Agent Work round trip exact");
        Reject(()=>DashboardData.Export(target,Path.Combine(target,"catalog.json")),"cannot export over live catalog");
        var recoveryBefore=DashboardData.Snapshot(target);var recoveryAfter=DashboardData.Snapshot(target);
        byte[] replacement=Encoding.UTF8.GetBytes("Interrupted imported work");
        recoveryAfter.files[".local/agent-work.txt"]=Convert.ToBase64String(replacement);recoveryAfter.hashes[".local/agent-work.txt"]=DashboardData.Hash(replacement);
        string journal=Path.Combine(target,".local","restore-pending.json");
        File.WriteAllText(journal,AppData.Json.Serialize(new Dictionary<string,DashboardBackup>{{"before",recoveryBefore},{"after",recoveryAfter}}));
        File.WriteAllBytes(migrated,replacement);DashboardData.Recover(target);
        Check(File.ReadAllBytes(migrated).SequenceEqual(original)&&!File.Exists(journal),"interrupted restore recovered on next startup");
        File.WriteAllText(journal,AppData.Json.Serialize(new Dictionary<string,DashboardBackup>{{"before",recoveryBefore},{"after",recoveryAfter}}));
        File.WriteAllText(migrated,"Divergent external edit");
        Reject(()=>DashboardData.Recover(target),"divergent recovery preserved for manual resolution");
        Check(File.ReadAllText(migrated)=="Divergent external edit"&&File.Exists(journal),"recovery does not overwrite divergent data");
        File.WriteAllBytes(migrated,original);DashboardData.Recover(target);
        string desktop=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Current Work.txt");
        if(File.Exists(desktop)){
            string real=Path.Combine(profile,"RealMigration");Directory.CreateDirectory(real);byte[] bytes=DashboardData.ReadBounded(desktop,1048576);
            string copy=DashboardData.MigrateWork(real,desktop);
            Check(File.ReadAllBytes(copy).SequenceEqual(bytes)&&DashboardData.ReadBounded(desktop).SequenceEqual(bytes),"actual Desktop work list copied and verified without changing original");
        }
        byte[] ico=DashboardBrand.IconBytes(Color.FromArgb(255,102,0));File.WriteAllBytes(Path.Combine(profile,"dashboard-orange.ico"),ico);
        using(var reader=new BinaryReader(new MemoryStream(ico))){
            reader.ReadUInt16();Check(reader.ReadUInt16()==1&&reader.ReadUInt16()==9,"ICO has nine independent sizes");
            for(int i=0;i<DashboardBrand.Sizes.Length;i++){int size=reader.ReadByte();if(size==0)size=256;reader.ReadByte();reader.ReadUInt16();reader.ReadUInt16();reader.ReadUInt16();int length=reader.ReadInt32(),offset=reader.ReadInt32();using(var stream=new MemoryStream(ico,offset,length))using(var image=Image.FromStream(stream))Check(image.Width==size&&image.Height==size,"ICO frame "+size);}
        }
        using(var sheet=new Bitmap(640,240))using(var g=Graphics.FromImage(sheet)){
            g.Clear(Color.FromArgb(32,32,32));g.FillRectangle(Brushes.WhiteSmoke,0,120,640,120);
            foreach(int top in new[]{0,120}){int x=20;foreach(int size in new[]{16,20,24,32,40,48,64})using(var image=DashboardBrand.Render(Color.FromArgb(255,102,0),size)){g.DrawImageUnscaled(image,x,top+25);x+=size+22;}}
            sheet.Save(Path.Combine(profile,"icon-native-sizes.png"),ImageFormat.Png);
        }
        Console.WriteLine("Data and icon verification complete. No native window created; no registry, desktop input, regular-app replacement or original-data edits.");
    }
}
