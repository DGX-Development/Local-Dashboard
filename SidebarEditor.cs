using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
public sealed class SidebarItem : System.ComponentModel.INotifyPropertyChanged {
    public string id{get;set;}
    public string icon{get;set;}
    public string iconPath{get;set;}
    string categoryLabel;
    public string label {
        get { return categoryLabel; }
        set {
            if(categoryLabel==value)return;
            categoryLabel=value;
            var changed=PropertyChanged;
            if(changed!=null)changed(this,new System.ComponentModel.PropertyChangedEventArgs("label"));
        }
    }
    public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
}
public sealed class SidebarLayout {public int schemaVersion{get;set;}public long revision{get;set;}public bool customized{get;set;}public List<SidebarItem> items{get;set;}public Dictionary<string,List<string>> orders{get;set;}}
internal sealed class SidebarSnapshot {internal SidebarLayout Layout;internal string Token;}
internal sealed class SidebarStore {
    internal static readonly string FilePath=Path.Combine(AppData.Root,".local","sidebar-layout.json");
    internal static readonly string[,] Groups={{"software","Projects","categories"},{"media","Media","reports"},{"website","Web projects","folder"},{"documents","Documents","dev-guide"},{"everyday","Websites","globe"},{"workflows","Workflows","workflow"},{"secondary","Other folders","folder"},{"all","All links","categories"}};
    internal static bool Custom(string id){
        Guid key;
        return id!=null&&id.StartsWith("category-",StringComparison.Ordinal)&&Guid.TryParseExact(id.Substring(9),"N",out key);
    }
    internal static bool Known(string id){return Custom(id)||Enumerable.Range(0,Groups.GetLength(0)).Any(i=>Groups[i,0]==id);}
    internal static string DefaultIcon(string id){
        for(int i=0;i<Groups.GetLength(0);i++)if(Groups[i,0]==id)return Groups[i,2];
        return "folder";
    }
    internal static string DefaultLabel(string id){for(int i=0;i<Groups.GetLength(0);i++)if(Groups[i,0]==id)return Groups[i,1];return Custom(id)?"Imported category":id;}
    static string LegacyDefaultLabel(string id){
        switch(id){
            case "software":return "Software projects";
            case "website":return "Website projects";
            case "documents":return "Development documents";
            case "everyday":return "Websites I use";
            case "secondary":return "Other workspaces";
            case "all":return "All links";
            default:return null;
        }
    }
    internal static bool ValidLabel(string value){return !String.IsNullOrWhiteSpace(value)&&value.Trim().Length<=60&&!value.Any(Char.IsControl);}
    readonly string file;internal SidebarStore(string path){file=path;}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    internal static SidebarLayout Defaults(){return new SidebarLayout{schemaVersion=1,revision=0,orders=new Dictionary<string,List<string>>(),items=new[]{"software","documents","everyday","all"}.Select(id=>new SidebarItem{id=id,label=DefaultLabel(id)}).ToList()};}
    internal static SidebarLayout Copy(SidebarLayout layout){return AppData.Json.Deserialize<SidebarLayout>(AppData.Json.Serialize(layout));}
    static string Hash(string text){using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));}
    void Validate(SidebarLayout value){Require(value!=null&&value.schemaVersion==1&&value.revision>=0&&value.items!=null&&value.items.Count<=100,"Unsupported sidebar layout.");var ids=new HashSet<string>(StringComparer.Ordinal);foreach(var item in value.items)Require(item!=null&&!String.IsNullOrWhiteSpace(item.id)&&item.id.Length<=100&&!item.id.Any(Char.IsControl)&&ids.Add(item.id)&&ValidLabel(item.label),"Invalid or duplicate sidebar group.");if(value.orders==null)value.orders=new Dictionary<string,List<string>>();foreach(var order in value.orders)Require(!String.IsNullOrWhiteSpace(order.Key)&&order.Key.Length<=100&&!order.Key.Any(Char.IsControl)&&order.Value!=null&&order.Value.Count<=500&&order.Value.Distinct(StringComparer.Ordinal).Count()==order.Value.Count&&order.Value.All(id=>!String.IsNullOrWhiteSpace(id)&&id.Length<=100&&!id.Any(Char.IsControl)),"Invalid project order.");}
    internal SidebarSnapshot Read(){if(!File.Exists(file)){
        var initial=Defaults();string catalog=Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(file)),"catalog.json");
        if(File.Exists(catalog)){
            var entries=AppData.Json.Deserialize<List<Entry>>(File.ReadAllText(catalog));
            foreach(string id in entries.Select(e=>e.section).Distinct().Where(Known))
                if(!initial.items.Any(i=>i.id==id))initial.items.Insert(initial.items.Count-1,new SidebarItem{id=id,label=DefaultLabel(id)});
        }
        return new SidebarSnapshot{Layout=initial,Token="missing"};
    }Require(new FileInfo(file).Length<=128*1024,"Sidebar layout is too large.");string text;using(var stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete))using(var reader=new StreamReader(stream))text=reader.ReadToEnd();var value=AppData.Json.Deserialize<SidebarLayout>(text);Validate(value);if(value.orders==null)value.orders=new Dictionary<string,List<string>>();foreach(var order in value.orders){Require(!String.IsNullOrWhiteSpace(order.Key)&&order.Key.Length<=100&&!order.Key.Any(Char.IsControl)&&order.Value!=null&&order.Value.Count<=500&&order.Value.Distinct(StringComparer.Ordinal).Count()==order.Value.Count&&order.Value.All(id=>!String.IsNullOrWhiteSpace(id)&&id.Length<=100&&!id.Any(Char.IsControl)),"Invalid project order.");}foreach(var item in Defaults().items.Where(item=>!value.customized||item.id=="all")){
        var existing=value.items.FirstOrDefault(i=>i.id==item.id);
        if(existing==null)value.items.Add(item);
        // Saved labels belong to the user; never replace them with new defaults.
    }return new SidebarSnapshot{Layout=value,Token=Hash(text)};}
    internal string Prepare(SidebarSnapshot expected,SidebarLayout draft)
    {
        Validate(draft);
        var current=Read();
        Require(current.Layout.revision==expected.Layout.revision&&current.Token==expected.Token,"Sidebar changed elsewhere. Cancel to load the latest layout, then edit again.");
        Require(current.Layout.revision<Int64.MaxValue,"Sidebar revision limit reached.");
        var raw=File.Exists(file)?AppData.Json.DeserializeObject(File.ReadAllText(file)) as Dictionary<string,object>:new Dictionary<string,object>();
        Require(raw!=null,"Invalid sidebar data.");
        var saved=new Dictionary<string,Dictionary<string,object>>();
        object old;
        if(raw.TryGetValue("items",out old)&&old is System.Collections.IEnumerable)
            foreach(var obj in (System.Collections.IEnumerable)old)
            {
                var row=obj as Dictionary<string,object>;
                if(row!=null&&row.ContainsKey("id"))saved[Convert.ToString(row["id"])]=row;
            }
        var rows=new List<object>();
        foreach(var item in draft.items)
        {
            Dictionary<string,object> row;
            if(!saved.TryGetValue(item.id,out row))row=new Dictionary<string,object>();
            row["id"]=item.id;row["label"]=item.label.Trim();
            if(item.icon==null)row.Remove("icon");else row["icon"]=item.icon;
            if(item.iconPath==null)row.Remove("iconPath");else row["iconPath"]=item.iconPath;
            rows.Add(row);
        }
        raw["schemaVersion"]=1;raw["revision"]=current.Layout.revision+1;
        if(draft.customized)raw["customized"]=true;
        raw["items"]=rows;raw["orders"]=draft.orders??new Dictionary<string,List<string>>();
        string json=AppData.Json.Serialize(raw);
        Require(Encoding.UTF8.GetByteCount(json)<=128*1024,"Sidebar layout is too large.");
        return json;
    }
    internal SidebarSnapshot Save(SidebarSnapshot expected,SidebarLayout draft)
    {
        using(var mutex=new Mutex(false,ChecklistStore.LockName(file)))
        {
            bool locked;
            try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
            Require(locked,"Sidebar is busy. Nothing was saved; try again.");
            try
            {
                string json=Prepare(expected,draft);
                string directory=Path.GetDirectoryName(file);
                Directory.CreateDirectory(directory);
                string temporary=Path.Combine(directory,Guid.NewGuid().ToString("N")+".tmp");
                try{File.WriteAllText(temporary,json,new UTF8Encoding(false));if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}
                finally{if(File.Exists(temporary))File.Delete(temporary);}
                return Read();
            }
            finally{mutex.ReleaseMutex();}
        }
    }
    internal static void SelfTest(){string folder=Path.Combine(AppData.Root,".dev","Review","SidebarTests");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"sidebar-test.json");if(File.Exists(file))File.Delete(file);var store=new SidebarStore(file);var initial=store.Read();Require(!File.Exists(file),"Reading defaults created a file.");var draft=Copy(initial.Layout);draft.items.Reverse();draft.items.First(i=>i.id=="media").label="My media";var first=store.Save(initial,draft);Require(new SidebarStore(file).Read().Layout.items[0].id==draft.items[0].id&&first.Layout.items.First(i=>i.id=="media").label=="My media","Order/alias did not survive reopen.");bool stale=false;try{store.Save(initial,initial.Layout);}catch{stale=true;}Require(stale&&store.Read().Layout.revision==1,"Stale save overwritten layout.");string old=File.ReadAllText(file);File.WriteAllText(file,old+" ");bool direct=false;try{store.Save(first,draft);}catch{direct=true;}Require(direct&&File.ReadAllText(file)==old+" ","Same-revision direct edit was overwritten.");var fresh=store.Read();var raw=(Dictionary<string,object>)AppData.Json.DeserializeObject(File.ReadAllText(file));raw["futureData"]="retain";((Dictionary<string,object>)((object[])raw["items"])[0])["futureItem"]=true;File.WriteAllText(file,AppData.Json.Serialize(raw));fresh=store.Read();store.Save(fresh,Copy(fresh.Layout));var retained=(Dictionary<string,object>)AppData.Json.DeserializeObject(File.ReadAllText(file));Require(retained.ContainsKey("futureData")&&((Dictionary<string,object>)((object[])retained["items"])[0]).ContainsKey("futureItem"),"Unknown fields lost.");File.WriteAllText(file,"{invalid");bool invalid=false;try{store.Save(fresh,draft);}catch{invalid=true;}Require(invalid&&File.ReadAllText(file)=="{invalid","Invalid layout overwritten.");Console.WriteLine("Sidebar store checks passed: defaults, stable-ID aliases/order, restart, revision/hash conflicts and unknown/invalid-data preservation. Live layout untouched.");}
}
internal sealed class SidebarEditor:Panel {
    const string DragFormat="LocalDashboard.Sidebar.Group";
    readonly Dictionary<string,Button> buttons;readonly Dictionary<string,Panel> rows=new Dictionary<string,Panel>();readonly Dictionary<string,Label> grips=new Dictionary<string,Label>();readonly Button edit=DashboardForm.LinkButton("Edit"),move=DashboardForm.LinkButton("Move"),save=DashboardForm.LinkButton("Save"),cancel=DashboardForm.LinkButton("Cancel");readonly SidebarStore store;SidebarSnapshot snapshot;SidebarLayout draft;bool editing,testing;
    internal Action Changed;internal Action ManageLinks;internal Action<string> Message;internal bool IsEditing{get{return editing;}}internal string ErrorText{get;private set;}internal int ContentHeight{get{return 40+buttons.Count*40;}}
    internal SidebarEditor(Dictionary<string,Button> navigation,string path){buttons=navigation;store=new SidebarStore(path);BackColor=Color.FromArgb(38,38,38);Controls.Add(edit);Controls.Add(move);Controls.Add(save);Controls.Add(cancel);edit.AccessibleName="Edit dashboard links";move.AccessibleName="Move sidebar and list entries";move.Font=new Font("Segoe UI Variable Text",9.5f);move.TabStop=true;edit.Font=new Font("Segoe UI Variable Text",9.5f);save.Font=cancel.Font=edit.Font;edit.TabStop=true;edit.Click+=(s,e)=>{if(ManageLinks!=null)ManageLinks();};move.Click+=(s,e)=>{BeginEdit();if(editing)save.Focus();};save.Click+=(s,e)=>{SaveEdit();if(!editing)move.Focus();};cancel.Click+=(s,e)=>{CancelEdit();move.Focus();};
        foreach(var pair in buttons){string id=pair.Key;var row=new Panel{Height=34,BackColor=BackColor,AccessibleName="Sidebar group "+id};rows[id]=row;Controls.Add(row);var button=pair.Value;row.Controls.Add(button);button.AccessibleDescription="Select group. Choose Move below the list to reorder.";var grip=new Label{Text="⋮⋮",TextAlign=ContentAlignment.MiddleCenter,ForeColor=DashboardForm.Muted,Cursor=Cursors.SizeAll,AccessibleName="Drag "+SidebarStore.DefaultLabel(id)};grips[id]=grip;row.Controls.Add(grip);grip.Visible=false;
            button.KeyDown+=(s,e)=>{if(editing&&e.Control&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){MoveBy(id,e.KeyCode==Keys.Up?-1:1);button.Focus();e.Handled=e.SuppressKeyPress=true;}};
            ReorderDrag.Source(grip,DragFormat,()=>id,()=>editing,()=>LabelFor(id));
            ReorderDrag.Target(row,DragFormat,value=>editing&&buttons.ContainsKey(value)&&value!=id,(value,after)=>MoveBefore(value,id,after));}
        Resize+=(s,e)=>Arrange();try{snapshot=store.Read();draft=SidebarStore.Copy(snapshot.Layout);}catch{snapshot=new SidebarSnapshot{Layout=SidebarStore.Defaults(),Token="invalid"};draft=SidebarStore.Copy(snapshot.Layout);ErrorText="Saved sidebar layout is unavailable. No saved data was changed.";}Apply();}
    internal void ReloadSaved(){if(editing)return;snapshot=store.Read();draft=SidebarStore.Copy(snapshot.Layout);Apply();}
    internal string LabelFor(string id){var item=draft.items.FirstOrDefault(i=>i.id==id);return item==null?SidebarStore.DefaultLabel(id):item.label;}
    internal void BeginEdit(){try{snapshot=store.Read();draft=SidebarStore.Copy(snapshot.Layout);editing=true;ErrorText=null;Apply();if(Message!=null)Message("Move mode: drag handles or press Ctrl+Up/Down. Save or Cancel.");}catch(Exception error){Fail(error);}}
    internal List<string> OrderFor(string section){List<string> result;return draft.orders!=null&&draft.orders.TryGetValue(section,out result)?result:new List<string>();}
    internal void MoveProject(string section,string id,string target,List<string> initial,bool after=false){if(!editing||id==target)return;if(draft.orders==null)draft.orders=new Dictionary<string,List<string>>();List<string> order;if(!draft.orders.TryGetValue(section,out order))order=new List<string>(initial);foreach(string value in initial)if(!order.Contains(value))order.Add(value);if(!order.Contains(id)||!order.Contains(target))return;order.Remove(id);order.Insert(order.IndexOf(target)+(after?1:0),id);draft.orders[section]=order;if(Changed!=null)Changed();}
    internal void MoveProjectBy(string section,string id,int direction,List<string> initial){if(!editing)return;var order=OrderFor(section).Where(initial.Contains).Concat(initial.Where(i=>!OrderFor(section).Contains(i))).ToList();int from=order.IndexOf(id),to=from+direction;if(from<0||to<0||to>=order.Count)return;if(draft.orders==null)draft.orders=new Dictionary<string,List<string>>();string value=order[from];order[from]=order[to];order[to]=value;draft.orders[section]=order;if(Changed!=null)Changed();}
    internal void MoveBy(string id,int direction){if(!editing)return;var ordered=draft.items.Where(i=>buttons.ContainsKey(i.id)).ToList();int from=ordered.FindIndex(i=>i.id==id),to=from+direction;if(from<0||to<0||to>=ordered.Count)return;int a=draft.items.IndexOf(ordered[from]),b=draft.items.IndexOf(ordered[to]);var swap=draft.items[a];draft.items[a]=draft.items[b];draft.items[b]=swap;Apply();}
    internal void MoveBefore(string id,string target,bool after=false){if(!editing||id==target||!buttons.ContainsKey(id)||!buttons.ContainsKey(target))return;var item=draft.items.First(i=>i.id==id);draft.items.Remove(item);draft.items.Insert(draft.items.FindIndex(i=>i.id==target)+(after?1:0),item);Apply();}
    internal void SaveEdit(){if(!editing)return;try{snapshot=store.Save(snapshot,draft);draft=SidebarStore.Copy(snapshot.Layout);editing=false;Apply();if(Message!=null)Message("Sidebar saved and locked.");}catch(Exception error){Fail(error);}}
    internal void CancelEdit(){if(!editing)return;editing=false;try{snapshot=store.Read();draft=SidebarStore.Copy(snapshot.Layout);}catch{draft=SidebarStore.Copy(snapshot.Layout);}Apply();if(Message!=null)Message("Sidebar edits discarded; sidebar locked.");}
    void Fail(Exception error){ErrorText=error.Message;if(Message!=null)Message(ErrorText);if(!testing)MessageBox.Show(FindForm(),ErrorText,"Edit sidebar",MessageBoxButtons.OK,MessageBoxIcon.Information);}
    void Apply(){edit.Visible=move.Visible=!editing;save.Visible=cancel.Visible=editing;foreach(var id in buttons.Keys){buttons[id].Text=LabelFor(id);grips[id].Visible=editing;foreach(Control control in rows[id].Controls)control.AllowDrop=editing;rows[id].AllowDrop=editing;}Arrange();if(Changed!=null)Changed();}
    internal void Arrange(){int bottom=buttons.Count*40+2;edit.SetBounds(Math.Max(0,Width-72),bottom,72,30);move.SetBounds(Math.Max(0,Width-152),bottom,72,30);save.Bounds=move.Bounds;cancel.Bounds=edit.Bounds;move.TabIndex=buttons.Count;edit.TabIndex=buttons.Count+1;save.TabIndex=buttons.Count;cancel.TabIndex=buttons.Count+1;int y=0,tab=0;foreach(var item in draft==null?SidebarStore.Defaults().items:draft.items){if(!buttons.ContainsKey(item.id))continue;var row=rows[item.id];row.SetBounds(0,y,Width,34);int inset=editing?24:0;buttons[item.id].SetBounds(inset,0,Math.Max(40,Width-inset),34);row.TabIndex=tab++;buttons[item.id].TabIndex=0;grips[item.id].SetBounds(0,0,22,34);y+=40;}}
    internal void VerifyEditControl(){if(save.Bounds!=move.Bounds||cancel.Bounds!=edit.Bounds||!save.TabStop||!cancel.TabStop)throw new Exception("Move actions shifted or lost keyboard access.");if(edit.Text!="Edit"||edit.Width>90||edit.Right>ClientSize.Width||edit.Top<rows.Values.Max(r=>r.Bottom)||!edit.TabStop||String.IsNullOrWhiteSpace(edit.AccessibleName)||edit.Font.Size<9)throw new Exception("Small Edit control bounds/accessibility failed.");}
    internal void BeginViaControlForTest(){move.PerformClick();if(!editing)throw new Exception("Move control did not unlock.");}
    internal void VerifyLocked(){VerifyEditControl();if(editing||grips.Values.Any(g=>g.Visible)||buttons.Values.Any(b=>b.AllowDrop))throw new Exception("Sidebar is not locked.");}
    internal void TestRename(string id,string label){draft.items.First(i=>i.id==id).label=label;Apply();}
    internal static void SelfTest(){string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","editor-test.json");Directory.CreateDirectory(Path.GetDirectoryName(file));if(File.Exists(file))File.Delete(file);var controls=Enumerable.Range(0,SidebarStore.Groups.GetLength(0)).ToDictionary(i=>SidebarStore.Groups[i,0],i=>DashboardForm.LinkButton(SidebarStore.Groups[i,1]));using(var editor=new SidebarEditor(controls,file)){editor.testing=true;editor.Size=new Size(216,editor.ContentHeight);editor.VerifyLocked();editor.MoveBefore("media","software");if(editor.draft.items[0].id!="software")throw new Exception("Locked reorder changed layout.");editor.BeginEdit();editor.TestRename("media","My media");editor.MoveBy("media",-1);editor.MoveProjectBy("software","b",-1,new List<string>{"a","b"});editor.CancelEdit();editor.VerifyLocked();if(File.Exists(file)||editor.LabelFor("media")!="Broadcast & Media"||editor.OrderFor("software").Count!=0)throw new Exception("Cancel saved draft.");editor.BeginEdit();editor.MoveBefore("software","media",true);if(editor.draft.items[0].id!="media")throw new Exception("Lower-half sidebar drop did not move below adjacent target.");editor.MoveBefore("software","media");editor.MoveProject("software","a","b",new List<string>{"a","b","c"},true);if(editor.OrderFor("software")[1]!="a")throw new Exception("Lower-half project drop failed.");editor.MoveProject("software","a","b",new List<string>{"a","b","c"});editor.TestRename("media","My media");editor.MoveBefore("media","software");editor.MoveProject("software","b","a",new List<string>{"a","b"});editor.SaveEdit();editor.VerifyLocked();var reopened=new SidebarStore(file).Read();if(reopened.Layout.items[0].id!="media"||reopened.Layout.items[0].label!="My media"||reopened.Layout.orders["software"][0]!="b")throw new Exception("Editor save lost order/name.");editor.BeginEdit();var other=new SidebarStore(file);var observed=other.Read();var change=SidebarStore.Copy(observed.Layout);change.items.First(i=>i.id=="software").label="Other window";other.Save(observed,change);editor.SaveEdit();if(!editor.IsEditing||String.IsNullOrEmpty(editor.ErrorText)||other.Read().Layout.items.First(i=>i.id=="software").label!="Other window")throw new Exception("Concurrent sidebar save was overwritten.");editor.CancelEdit();editor.VerifyLocked();}Console.WriteLine("Sidebar editor checks passed: normal lock, inline rename, drag-order method, keyboard reorder, Save/relock, Cancel/discard and conflicting-window protection. Fixtures only.");}
}





