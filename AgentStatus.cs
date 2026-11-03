using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

public sealed class StatusProducer {public string id{get;set;} public string name{get;set;} public string heartbeatAt{get;set;} public string connection{get;set;}}
public sealed class StatusTask {internal StatusSnapshot Source;public string id{get;set;} public string projectPath{get;set;} public string agent{get;set;} public string task{get;set;} public string state{get;set;} public string updatedAt{get;set;}}
public sealed class StatusSnapshot {internal int SourceCount;internal int InvalidSources;public int schemaVersion{get;set;} public string generatedAt{get;set;} public StatusProducer producer{get;set;} public List<StatusTask> tasks{get;set;}}
internal static class AgentFeed {
    internal const int LeaseSeconds=120;
    internal static readonly string FilePath=Path.Combine(AppData.Root,".local","agent-status.json");
    internal static readonly string[] States={"Working","Waiting for review","Waiting for user","Blocked","Done"};
    static void Require(bool pass){if(!pass)throw new Exception("Invalid status snapshot.");}
    static void Text(string value,int length){Require(!String.IsNullOrWhiteSpace(value)&&value.Length<=length&&!value.Any(Char.IsControl));}
    internal static DateTimeOffset Time(string value){DateTimeOffset result;Require(value!=null&&(value.EndsWith("Z",StringComparison.OrdinalIgnoreCase)||System.Text.RegularExpressions.Regex.IsMatch(value,@"[+-]\d\d:\d\d$"))&&DateTimeOffset.TryParse(value,CultureInfo.InvariantCulture,DateTimeStyles.None,out result));return DateTimeOffset.Parse(value,CultureInfo.InvariantCulture);}
    internal static string Project(string path){return Path.GetFileName(Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));}
    internal static StatusSnapshot Parse(string json,List<Entry> entries,DateTimeOffset now){
        Require(json!=null&&json.Length<=1024*1024);
        var feed=AppData.Json.Deserialize<StatusSnapshot>(json);Require(feed!=null&&feed.schemaVersion==1&&feed.producer!=null&&feed.tasks!=null&&feed.tasks.Count<=200);
        Text(feed.producer.id,100);Text(feed.producer.name,100);Require(feed.producer.connection=="online"||feed.producer.connection=="offline");
        var generated=Time(feed.generatedAt);var heartbeat=Time(feed.producer.heartbeatAt);Require(generated<=now.AddSeconds(30)&&heartbeat<=generated.AddSeconds(30));
        var allowed=new HashSet<string>(entries.Where(e=>e.section=="software"||e.section=="website"||e.section=="secondary").SelectMany(e=>e.targets).Where(t=>t.kind=="folder").Select(t=>Path.GetFullPath(t.path).TrimEnd('\\','/')),StringComparer.OrdinalIgnoreCase);
        var ids=new HashSet<string>(StringComparer.Ordinal);
        foreach(var task in feed.tasks){Require(task!=null);Text(task.id,100);Require(ids.Add(task.id));Text(task.agent,100);Text(task.task,240);Require(States.Contains(task.state));Text(task.projectPath,1000);Require(Path.IsPathRooted(task.projectPath)&&allowed.Contains(Path.GetFullPath(task.projectPath).TrimEnd('\\','/'))&&Directory.Exists(task.projectPath));Require(Time(task.updatedAt)<=generated.AddSeconds(30));}
        return feed;
    }
    internal static bool Fresh(StatusSnapshot feed,StatusTask task,DateTimeOffset now){if(task!=null&&task.Source!=null)feed=task.Source;return feed.producer.connection=="online"&&(now-Time(feed.generatedAt)).TotalSeconds<=LeaseSeconds&&(now-Time(feed.producer.heartbeatAt)).TotalSeconds<=LeaseSeconds&&(task==null||(now-Time(task.updatedAt)).TotalSeconds<=LeaseSeconds);}
    internal static string Stamp(string value){return Time(value).ToLocalTime().ToString("MMM d, HH:mm:ss zzz");}
    static StatusSnapshot ReadOne(string path,List<Entry> entries){using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){Require(stream.Length<=1024*1024);using(var reader=new StreamReader(stream))return Parse(reader.ReadToEnd(),entries,DateTimeOffset.UtcNow);}}
    internal static StatusSnapshot Read(List<Entry> entries){var paths=new List<string>();if(File.Exists(FilePath))paths.Add(FilePath);string reports=Path.Combine(AppData.Root,".local","agent-reports");if(Directory.Exists(reports))paths.AddRange(Directory.EnumerateFiles(reports,"*.json").Take(201));Require(paths.Count<=200);if(paths.Count==0)return null;var feeds=new List<StatusSnapshot>();int invalid=0;var ids=new HashSet<string>(StringComparer.Ordinal);foreach(string path in paths){try{var feed=ReadOne(path,entries);if(path!=FilePath)Require(String.Equals(Path.GetFileNameWithoutExtension(path),feed.producer.id,StringComparison.Ordinal));Require(ids.Add(feed.producer.id));feeds.Add(feed);}catch{invalid++;}}Require(feeds.Count>0);return Combine(feeds,invalid);}
    internal static StatusSnapshot Combine(List<StatusSnapshot> feeds,int invalid){Require(feeds.Count>0&&feeds.Select(f=>f.producer.id).Distinct(StringComparer.Ordinal).Count()==feeds.Count);var merged=new StatusSnapshot{schemaVersion=1,SourceCount=feeds.Count,InvalidSources=invalid,generatedAt=feeds.OrderByDescending(f=>Time(f.generatedAt)).First().generatedAt,producer=new StatusProducer{id="participating-reports",name="Participating task reports",connection="online",heartbeatAt=feeds.OrderByDescending(f=>Time(f.producer.heartbeatAt)).First().producer.heartbeatAt},tasks=new List<StatusTask>()};foreach(var feed in feeds)foreach(var task in feed.tasks){task.Source=feed;merged.tasks.Add(task);}Require(merged.tasks.Count<=200);return merged;}
    internal static void SelfTest(List<Entry> entries){
        var now=DateTimeOffset.UtcNow;var folder=entries.Where(e=>e.section=="software").SelectMany(e=>e.targets).First(t=>t.kind=="folder").path;
        var feed=new StatusSnapshot {schemaVersion=1,generatedAt=now.ToString("o"),producer=new StatusProducer{id="test",name="Validation fixture",heartbeatAt=now.ToString("o"),connection="online"},tasks=new List<StatusTask>{new StatusTask{id="one",projectPath=folder,agent="Fixture",task="Validation only",state="Working",updatedAt=now.ToString("o")}}};
        Parse(AppData.Json.Serialize(feed),entries,now);Require(Fresh(feed,feed.tasks[0],now));Require(!Fresh(feed,feed.tasks[0],now.AddSeconds(121)));feed.producer.connection="offline";Require(!Fresh(feed,null,now));feed.producer.connection="online";
        foreach(string state in States){feed.tasks[0].state=state;Parse(AppData.Json.Serialize(feed),entries,now);}feed.tasks[0].state="invented";Reject(feed,entries,now);feed.tasks[0].state="Working";
        feed.tasks.Add(feed.tasks[0]);Reject(feed,entries,now);feed.tasks.RemoveAt(1);
        feed.tasks[0].projectPath=Path.GetPathRoot(AppData.Root);Reject(feed,entries,now);feed.tasks[0].projectPath=folder;
        feed.generatedAt=now.AddMinutes(5).ToString("o");Reject(feed,entries,now);feed.generatedAt=now.ToString("o");
        feed.tasks[0].updatedAt=now.AddMinutes(-5).ToString("o");Require(!Fresh(feed,feed.tasks[0],now));
        feed.tasks[0].updatedAt=now.ToString("o");var other=Parse(AppData.Json.Serialize(feed),entries,now);other.producer.id="other";other.producer.connection="offline";var merged=Combine(new List<StatusSnapshot>{feed,other},1);Require(merged.SourceCount==2&&merged.InvalidSources==1&&Fresh(merged,merged.tasks[0],now)&&!Fresh(merged,merged.tasks[1],now));feed.tasks[0].updatedAt=now.AddMinutes(-3).ToString("o");Require(!Fresh(merged,merged.tasks[0],now));bool conflict=false;try{Combine(new List<StatusSnapshot>{feed,feed},0);}catch{conflict=true;}Require(conflict);bool malformed=false;try{Parse("{invalid",entries,now);}catch{malformed=true;}Require(malformed);
        Console.WriteLine("Status validation passed: states, exact project paths, duplicate IDs, future timestamps, offline and expired leases. No fixture written to the live feed.");
    }
    static void Reject(StatusSnapshot feed,List<Entry> entries,DateTimeOffset now){bool rejected=false;try{Parse(AppData.Json.Serialize(feed),entries,now);}catch{rejected=true;}Require(rejected);}
}
internal sealed class AgentStatusView:Panel {
    readonly Label heading=new Label(),summary=new Label();readonly Panel rows=new Panel();readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
    List<Entry> entries;string previous;
    internal AgentStatusView(){
        BackColor=DashboardForm.Background;Dock=DockStyle.Fill;
        heading.Text="Agent Status";heading.Font=new Font("Segoe UI Variable Display",15,FontStyle.Bold);heading.ForeColor=DashboardForm.Ink;heading.SetBounds(16,12,300,30);Controls.Add(heading);
        summary.ForeColor=DashboardForm.Muted;summary.UseMnemonic=false;summary.SetBounds(16,46,380,52);Controls.Add(summary);
        rows.AutoScroll=true;rows.BackColor=BackColor;Controls.Add(rows);
        Resize+=(s,e)=>{summary.Width=Math.Max(100,Width-32);rows.SetBounds(0,104,Width,Math.Max(0,Height-104));PositionRows();};
        timer.Interval=5000;timer.Tick+=(s,e)=>{if(Visible)RefreshFeed(false);};timer.Start();
    }
    internal void Reload(List<Entry> catalog){entries=catalog;RefreshFeed(true);}
    void AddText(string text,int height,Color color,bool bold){var label=new Label{Text=text,Height=height,ForeColor=color,Font=new Font("Segoe UI Variable Text",bold?10:10,bold?FontStyle.Bold:FontStyle.Regular),UseMnemonic=false,AutoEllipsis=true,Padding=new Padding(16,4,12,0)};rows.Controls.Add(label);}
    void RefreshFeed(bool force){
        if(entries==null)return;StatusSnapshot feed=null;string message;
        try{feed=AgentFeed.Read(entries);message=feed==null?"No status feed connected.\nA producer must explicitly report task states.":feed.SourceCount>0?"Participating task reports · "+feed.SourceCount+" source(s)"+(feed.InvalidSources>0?" · "+feed.InvalidSources+" invalid":"")+"\nLatest report "+AgentFeed.Stamp(feed.generatedAt):feed.producer.name+" · "+(feed.producer.connection=="offline"?"Offline":AgentFeed.Fresh(feed,null,DateTimeOffset.UtcNow)?"Reporting":"Stale / disconnected")+"\nLast update "+AgentFeed.Stamp(feed.generatedAt);}
        catch{message="Status feed unavailable or invalid.\nNo current task state can be verified.";}
        var now=DateTimeOffset.UtcNow;string key=message+(feed==null?"":AppData.Json.Serialize(feed)+feed.InvalidSources+String.Join("",feed.tasks.Select(t=>AgentFeed.Fresh(feed,t,now)?"1":"0")));
        if(!force&&key==previous)return;previous=key;summary.Text=message;
        rows.SuspendLayout();foreach(Control control in rows.Controls.Cast<Control>().ToArray())control.Dispose();rows.AutoScrollPosition=Point.Empty;
        if(feed==null){AddText("Connect a producer to see active projects here.",52,DashboardForm.Muted,false);}
        else if(feed.tasks.Count==0)AddText("No tasks reported in this snapshot.",48,DashboardForm.Muted,false);
        else foreach(var group in feed.tasks.GroupBy(t=>Path.GetFullPath(t.projectPath).TrimEnd('\\','/'),StringComparer.OrdinalIgnoreCase).OrderBy(g=>AgentFeed.Project(g.Key),StringComparer.CurrentCultureIgnoreCase)){
            AddText(AgentFeed.Project(group.Key),32,DashboardForm.Ink,true);
            foreach(var task in group.OrderBy(t=>Array.IndexOf(AgentFeed.States,t.state))){bool fresh=AgentFeed.Fresh(feed,task,now);var row=new Panel{Height=128,BackColor=DashboardForm.Surface};
                var badge=new Label{Text=(fresh?"":"Last reported: ")+task.state,ForeColor=SystemInformation.HighContrast?SystemColors.WindowText:fresh?(task.state=="Blocked"?Color.FromArgb(244,170,155):task.state.StartsWith("Waiting")?Color.FromArgb(233,201,136):DashboardForm.Accent):DashboardForm.Muted,Location=new Point(12,7),Height=22,UseMnemonic=false};row.Controls.Add(badge);
                var name=new Label{Text=task.agent+" · "+task.task,Location=new Point(12,32),Height=38,ForeColor=DashboardForm.Ink,UseMnemonic=false,AutoEllipsis=true};row.Controls.Add(name);
                var age=new Label{Text=(fresh?"Verified ":"Stale / offline · ")+AgentFeed.Stamp(task.updatedAt),Location=new Point(12,102),Height=22,ForeColor=DashboardForm.Muted,UseMnemonic=false};row.Controls.Add(age);var origin=new Label{Text=task.Source==null?feed.producer.name:task.Source.producer.name,Location=new Point(12,78),Height=20,ForeColor=DashboardForm.Muted,UseMnemonic=false,AutoEllipsis=true};row.Controls.Add(origin);
                row.Resize+=(s,e)=>{badge.Width=name.Width=age.Width=origin.Width=Math.Max(50,row.Width-24);};rows.Controls.Add(row);
            }
        }
        rows.ResumeLayout();PositionRows();
    }
    void PositionRows(){int y=0;Point offset=rows.AutoScrollPosition;foreach(Control row in rows.Controls){row.SetBounds(16+offset.X,y+offset.Y,Math.Max(80,rows.ClientSize.Width-32-SystemInformation.VerticalScrollBarWidth),row.Height);y+=row.Height+5;}rows.AutoScrollMinSize=new Size(0,y+8);}
    protected override void Dispose(bool disposing){if(disposing){timer.Stop();timer.Dispose();}base.Dispose(disposing);}
}




