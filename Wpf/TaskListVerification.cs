using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;

internal static class TaskListVerification {
    static int count;
    static void Check(bool value,string label){if(!value)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static void Reject(Action action,string label){bool caught=false;try{action();}catch(IOException){caught=true;}Check(caught,label);}
    internal static void Run(string profile){
        count=0;
        string simple="1. Organize photographs\n   Sort last year's pictures.\n\n   Notes:\n   Keep the original files.\n";
        var item=ReadableTasks.Parse(simple).Blocks.Single(b=>b.Kind=="task");
        Check(item.Number=="1"&&item.Title=="Organize photographs","number and title remain separate");
        Check(item.Summary=="Sort last year's pictures."&&item.Details.Contains("Keep the original files."),"brief description and longer notes are preserved");
        Check(item.Status==null&&item.Next==null,"plain text does not invent status or next step");
        item=ReadableTasks.Parse("1. Job\n   Status: Waiting for review.\n   Next: Inspect the result.\n\n   Unfamiliar field: Keep this too.").Blocks.Single();
        Check(item.Status=="Waiting for review."&&item.Next=="Inspect the result.","existing Status and Next remain supported");
        Check(item.Details.Contains("Unfamiliar field: Keep this too."),"unknown fields are retained in details");
        string plain="A plain document\nwith no task numbers.\n\n- A bullet\nAnother paragraph.";
        var document=ReadableTasks.Parse(plain);Check(document.Blocks.Count==1&&document.Blocks[0].Raw==plain,"unformatted document falls back to original text");
        string mixed="Introductory instructions\n\n## Current work\n1. Task\n   Description.\n   Notes: Additional words.\n\n## Extra information\nUnnumbered closing text.\n";
        document=ReadableTasks.Parse(mixed);Check(String.Join("\n",document.Blocks.Select(b=>b.Raw))==mixed,"mixed structured and unstructured blocks retain every source line in order");
        item=ReadableTasks.Parse("## Later / on hold\n### Sample project\n1. Future job\n   Plan only.").Blocks.Single(b=>b.Kind=="task");Check(item.Deferred,"project grouping preserves a deferred section");
        item=ReadableTasks.Parse("## Current work\n1. Held task\n   Status: ON HOLD. Do not resume setup.").Blocks.Single(b=>b.Kind=="task");Check(item.Deferred,"explicit task status keeps do-not-start visible");
        item=ReadableTasks.Parse("1. Project\n   Task: First job\n   Status: Ready for review.\n\n   Other job:\n   Status: ON HOLD.").Blocks.Single();
        Check(item.Ambiguous&&item.Status==null&&item.Details.Contains("Ready for review.")&&item.Details.Contains("Other job:"),"multiple legacy job statuses are not flattened into the parent");
        item=ReadableTasks.Parse("1. Job\n   Brief description.\n\n   Notes:\n   Status: Old historical note.").Blocks.Single();Check(item.Status==null&&item.Details.Contains("Status: Old historical note."),"a historical status inside notes stays in notes");
        foreach(string name in new[]{"Project Tasks.original.txt","Personal Tasks.original.txt","Project Tasks.txt","Personal Tasks.txt"}){
            string path=Path.Combine(profile,name);if(!File.Exists(path))throw new IOException("Missing representative list fixture: "+name);
            string hash=ChecklistLocation.Token(path);string text=File.ReadAllText(path);document=ReadableTasks.Parse(text);
            Check(String.Join("\n",document.Blocks.Select(b=>b.Raw))==text.Replace("\r\n","\n").Replace('\r','\n'),name+": complete ordered text coverage");
            Check(document.Blocks.Any(b=>b.Kind=="task"),name+": task blocks found");
            Check(ChecklistLocation.Token(path)==hash,name+": parser leaves source bytes unchanged");
        }
        string area=Path.Combine(profile,"three-lists");Directory.CreateDirectory(area);File.Copy(Path.Combine(profile,"catalog.json"),Path.Combine(area,"catalog.json"));
        string agent=Path.Combine(profile,"Project Tasks.txt"),personal=Path.Combine(profile,"Personal Tasks.txt");
        WorkSources.Save(area,new WorkSourceChoice{schemaVersion=1,kind="local",path=agent},null);
        WorkSources.Save(area,new WorkSourceChoice{schemaVersion=1,kind="local",path=personal},null,true);
        Check(WorkSources.Read(area).path==agent&&WorkSources.Read(area,true).path==personal,"Agent and Personal sources are independent");
        WorkSources.Store(area,WorkSources.Read(area),WorkSources.Token(area),WorkSources.Fetch(WorkSources.Read(area)));
        WorkSources.Store(area,WorkSources.Read(area,true),WorkSources.Token(area,true),WorkSources.Fetch(WorkSources.Read(area,true)),true);
        Check(WorkSources.Cached(area,WorkSources.Read(area)).text==File.ReadAllText(agent)&&WorkSources.Cached(area,WorkSources.Read(area,true)).text==File.ReadAllText(personal),"each source reads its own document");
        Reject(()=>WorkSources.Save(area,WorkSources.Read(area,true),"stale",true),"stale personal-source edits cannot overwrite a selection");
        string checklist=ChecklistLocation.PathFor(area);var entries=AppData.ReadEntries(File.ReadAllText(Path.Combine(area,"catalog.json")));var store=new ChecklistStore(checklist,entries);
        store.Upsert(new WorkPatch{expectedRevision=0,id="kept",title="Keep this checklist item",details="Existing detail"});store.Complete(store.Read().revision,"kept",true);
        string checklistHash=ChecklistLocation.Token(checklist),agentHash=ChecklistLocation.Token(agent),personalHash=ChecklistLocation.Token(personal);
        var reopened=new ChecklistStore(checklist,entries).Read();Check(reopened.items.Single().state=="done"&&reopened.items.Single().details=="Existing detail","checklist entries, notes and Done state persist on reopening");
        var backup=DashboardData.ExportWithLists(area,Path.Combine(profile,"three-lists-backup.json"));
        Check(backup.schemaVersion==4&&backup.lists.Count==3&&backup.lists.Values.All(x=>x.status=="included"),"backup includes all three selected lists");
        Reject(()=>DashboardData.ExportWithLists(area,personal),"backup cannot overwrite the Personal tasks source");
        var restored=DashboardData.RestoreCopies(area,backup,false);
        Check(ChecklistLocation.Token(restored.Personal)==personalHash&&WorkSources.Read(area,true).path==personal,"restore creates a byte-exact personal copy and keeps current selection by default");
        var older=AppData.Json.Deserialize<DashboardBackup>(AppData.Json.Serialize(backup));older.schemaVersion=3;older.lists.Remove("personal");older.files.Remove(".local/personal-tasks-source.json");older.hashes.Remove(".local/personal-tasks-source.json");
        DashboardData.RestoreCopies(area,older,true);Check(WorkSources.Read(area,true).path==personal,"older two-list backups preserve Personal tasks selection");
        restored=DashboardData.RestoreCopies(area,backup,true);Check(WorkSources.Read(area,true).path==restored.Personal&&WorkSources.Read(area).path==restored.Agent&&ChecklistLocation.PathFor(area)==restored.Checklist,"explicit restored-copy selection covers all three lists");
        Check(ChecklistLocation.Token(checklist)==checklistHash&&ChecklistLocation.Token(agent)==agentHash&&ChecklistLocation.Token(personal)==personalHash,"all original task and checklist bytes survive backup and restore");
        VerifyPersonalEditing(profile);
        Console.WriteLine("Readable task-list checks passed: "+count+".");
    }
    static void VerifyPersonalEditing(string profile){
        string area=Path.Combine(profile,"personal-editor");Directory.CreateDirectory(area);
        string file=Path.Combine(area,"personal.txt");string text="# Personal tasks\r\nUpdated: Old date\r\n\r\n1. First title\r\n   Short description.\r\n   Status: Waiting.\r\n   Next: Review.\r\n\r\n   Notes:\r\n   Keep these details.\r\n   Unknown: retain me.\r\n\r\n2. Second title\r\n   Do not change this.\r\n";
        File.WriteAllText(file,text,new UTF8Encoding(true));WorkSources.Save(area,new WorkSourceChoice{schemaVersion=1,kind="local",path=file},null,true);
        var snapshot=PersonalTextStore.Open(area);var block=ReadableTasks.Parse(text).Blocks.First(b=>b.Kind=="task");var draft=new PersonalTaskDraft(text,block);
        Check(draft.Simple&&draft.Summary=="Short description."&&draft.Notes.Contains("Unknown: retain me."),"personal editor maps simple fields without losing unknown notes");
        Check(draft.Apply(draft.Title,draft.Summary,draft.Notes,null)==text,"unchanged task form preserves exact line endings and spacing");
        string changed=draft.Apply("Edited title",draft.Summary,draft.Notes,null);
        Check(changed==text.Replace("1. First title","1. Edited title"),"title edit changes only that exact title");
        changed=draft.Apply(draft.Title,"New short description.","New notes.\nUnknown: retain me.",null);
        Check(changed.Contains("   Status: Waiting.\r\n   Next: Review.")&&changed.EndsWith("2. Second title\r\n   Do not change this.\r\n")&&changed.Contains("   Notes:\r\n   New notes.\r\n   Unknown: retain me."),"description and notes edits preserve optional fields and neighboring tasks");
        string saved=PersonalTextStore.Save(area,snapshot,changed);byte[] bytes=File.ReadAllBytes(file);
        Check(saved=="Personal tasks saved."&&bytes.Take(3).SequenceEqual(new byte[]{239,187,191})&&!File.Exists(PersonalTextStore.RecoveryPath(area)),"personal save keeps UTF-8 BOM and completes its recovery record");
        Check(File.ReadAllText(file).Contains("Updated: "+DateTime.UtcNow.ToString("MMMM",System.Globalization.CultureInfo.InvariantCulture))&&WorkSources.Cached(area,WorkSources.Read(area,true)).text==File.ReadAllText(file),"saved timestamp and displayed cache follow the saved original");
        Reject(()=>PersonalTextStore.Save(area,snapshot,"Stale edit"),"stale document save preserves the newer file");
        snapshot=PersonalTextStore.Open(area);string current=File.ReadAllText(file);string hash=ChecklistLocation.Token(file);
        Reject(()=>PersonalTextStore.Save(area,snapshot,current+"Changed",()=>{throw new IOException("Injected write failure");}),"write failure is reported");
        Check(ChecklistLocation.Token(file)==hash&&!File.Exists(PersonalTextStore.RecoveryPath(area)),"failed save rolls back original bytes and clears completed recovery");
        using(var held=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.Read))Reject(()=>PersonalTextStore.Save(area,snapshot,current+"Changed"),"locked source is not overwritten");
        Check(ChecklistLocation.Token(file)==hash,"locked-file attempt preserves bytes");
        Reject(()=>PersonalTextStore.Save(area,snapshot,new string('x',1048577)),"oversized edit is refused");
        WorkSources.Save(area,new WorkSourceChoice{schemaVersion=1,kind="synced",path=file},WorkSources.Token(area,true),true);
        Reject(()=>PersonalTextStore.Save(area,snapshot,current+"Changed"),"changed source selection rejects an open editor draft");
        foreach(var encoding in new Encoding[]{new UTF8Encoding(false),new UnicodeEncoding(false,true,true),new UnicodeEncoding(true,true,true),new UTF32Encoding(false,true,true),new UTF32Encoding(true,true,true)}){
            File.WriteAllText(file,"1. Original\n   Café and 日本語.\n",encoding);snapshot=PersonalTextStore.Open(area);PersonalTextStore.Save(area,snapshot,snapshot.Text.Replace("Original","Changed"));
            Check(File.ReadAllBytes(file).SequenceEqual(encoding.GetPreamble().Concat(encoding.GetBytes("1. Changed\n   Café and 日本語.\n"))),"encoding and newline preserved: "+encoding.WebName);
        }
        foreach(string name in new[]{"Personal Tasks.txt","Project Tasks.txt","Personal Tasks.original.txt","Project Tasks.original.txt"}){
            string real=File.ReadAllText(Path.Combine(profile,name));foreach(var task in ReadableTasks.Parse(real).Blocks.Where(b=>b.Kind=="task")){
                var d=new PersonalTaskDraft(real,task);Check(d.Apply(d.Title,d.Summary,d.Notes,d.Body)==real,name+" task "+task.Number+": unchanged editor is lossless");
            }
        }
        string unusual="1. Legacy task\n   Brief.\n   Unknown field: keep this\n   Status: First\n   Status: Second\n";draft=new PersonalTaskDraft(unusual,ReadableTasks.Parse(unusual).Blocks.First());
        Check(!draft.Simple&&draft.Apply("New title",null,null,draft.Body)==unusual.Replace("Legacy task","New title"),"ambiguous task uses lossless text editor");
        string added=PersonalTaskDraft.Add(text,"Added task","New description","More notes");
        Check(added.Contains("3. Added task")&&added.Contains(text.Substring(block.Start))&&added.IndexOf("3. Added task")<added.IndexOf("1. First title"),"new task inserts above first task without renumbering existing tasks");
        string empty="1. Bare title";draft=new PersonalTaskDraft(empty,ReadableTasks.Parse(empty).Blocks.First());var filled=ReadableTasks.Parse(draft.Apply(draft.Title,"New summary","New notes",null)).Blocks.Single();
        Check(filled.Summary=="New summary"&&filled.Details.Contains("New notes"),"bare task gains summary before longer notes");
        string plain="Loose text\n- No required fields\nUnfamiliar: preserved\n";File.WriteAllText(file,plain,new UTF8Encoding(false));snapshot=PersonalTextStore.Open(area);PersonalTextStore.Save(area,snapshot,plain+"One more line\n");
        Check(File.ReadAllText(file)==plain+"One more line\n","plain document editing has no schema conversion");
        snapshot=PersonalTextStore.Open(area);string recovery=PersonalTextStore.RecoveryPath(area);File.WriteAllText(recovery,AppData.Json.Serialize(new PersonalEditRecovery{path=file,before=Convert.ToBase64String(Encoding.UTF8.GetBytes("before interrupted save")),after=Convert.ToBase64String(Encoding.UTF8.GetBytes("after interrupted save"))}));
        Reject(()=>PersonalTextStore.Save(area,snapshot,snapshot.Text+"New edit"),"unresolved interrupted save protects recovery record and source");
        File.Delete(recovery);
    }
}

internal sealed partial class WpfDashboard {
    void VerifyReviewPolish(){
        verifying=nonWindowedVerification=true;window.Left=100;window.Top=100;
        try{
            foreach(bool personal in new[]{false,true}){
                var choice=new WorkSourceChoice{schemaVersion=1,kind="local",path=Path.Combine(profile,personal?"Personal Tasks.txt":"Project Tasks.txt")};
                WorkSources.Save(profile,choice,WorkSources.Token(profile,personal),personal);
                WorkSources.Store(profile,choice,WorkSources.Token(profile,personal),WorkSources.Fetch(choice),personal);
            }
            agentUpdatesView=false;
            for(int i=0;i<3;i++){
                assistantWorkView=i==0;personalTasksView=i==1;Select("work");RenderCompact(true);
                CaptureShell(profile,"review-tasks-"+i+".png",1120,820);
                if(heading.Text!="Tasks")throw new Exception("Tasks heading changed with selection.");
                var tabs=Find<StackPanel>("WorkNavigation").Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
                if(!tabs.Select(b=>(string)b.Content).SequenceEqual(new[]{"Agent tasks","Personal tasks","Checklists"}))throw new Exception("Task labels or order differ.");
                for(int n=0;n<3;n++){
                    var marker=(Border)tabs[n].Template.FindName("NavigationMarker",tabs[n]);
                    if(marker.Visibility!=(i==n?Visibility.Visible:Visibility.Collapsed))throw new Exception("Task selection marker differs.");
                    var content=(ContentPresenter)tabs[n].Template.FindName("ButtonContent",tabs[n]);
                    if(tabs[n].ActualHeight!=36||Math.Abs(content.TranslatePoint(new Point(),tabs[n]).Y+content.ActualHeight/2-tabs[n].ActualHeight/2)>1)throw new Exception("Task button height or vertical alignment differs.");
                }
                if(Visuals<Expander>(rows).Any(e=>e.IsExpanded))throw new Exception("Details opened by default.");
            }
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;
                Capture(dialog.Content as UIElement,Path.Combine(profile,"review-"+SafeName(dialog.Title)+".png"),(int)dialog.Width-16,(int)dialog.Height-39);
                if(dialog.Title=="Report an issue"){
                    var fields=Visuals<TextBox>(root).ToArray();if(fields.Length!=3||Visuals<Label>(root).Count()!=3)throw new Exception("Issue form fields differ.");
                    foreach(var field in fields)field.Text="";
                    string report=ComposeIssueReport("2.2.0","","","");
                    if(!report.Contains("What happened instead?")||!report.StartsWith("Local Dashboard 2.2.0"))throw new Exception("Clearing answers removed report labels.");
                }
                if(dialog.Title=="About Local Dashboard"){
                    var mark=Visuals<Image>(root).Single(i=>Equals(i.Tag,"publisher"));
                    if(mark.Width!=140)throw new Exception("Publisher mark size differs.");
                    if(!Visuals<TextBlock>(root).Any(t=>t.Text=="Version 2.2.0")||Visuals<TextBlock>(root).Any(t=>t.Text.Contains("Development build")))throw new Exception("Release About wording differs.");
                }
                if(dialog.Title=="Local Dashboard settings"){
                    foreach(var page in new[]{"Tasks","Appearance"}){
                        ClickIn(root,page);Capture(dialog.Content as UIElement,Path.Combine(profile,"review-settings-"+page+".png"),764,661);
                        foreach(var b in Visuals<Button>(root).Where(b=>Equals(b.Tag,"settings-category"))){
                            var marker=(Border)b.Template.FindName("NavigationMarker",b);
                            if((marker.Visibility==Visibility.Visible)!=Equals(b.Content,page))throw new Exception("Settings selection marker differs.");
                        }
                    }
                }
                if(dialog.IsVisible||new System.Windows.Interop.WindowInteropHelper(dialog).Handle!=IntPtr.Zero)throw new Exception("Native dialog created.");
                dialog.Close();return false;
            };
            Settings("Appearance");ShowAbout();ShowHelp();ShowReportIssue();
            var normalPresenter=presentDialog;string catalogBefore=File.ReadAllText(Path.Combine(profile,"catalog.json"));
            presentDialog=dialog=>{
                Capture(dialog.Content as UIElement,Path.Combine(profile,"release-edit-from-"+section+".png"),844,581);
                var root=dialog.Content as DependencyObject;
                var picker=Visuals<ComboBox>(root).Single();var items=Visuals<ListBox>(root).Single(l=>System.Windows.Automation.AutomationProperties.GetName(l)=="Dashboard entries");
                var name=Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Display name");
                if(picker.SelectedItem==null||items.SelectedItem==null||String.IsNullOrWhiteSpace(name.Text))throw new Exception("Editor initial selection is blank.");
                picker.SelectedValue="workflows";Capture(dialog.Content as UIElement,Path.Combine(profile,"release-edit-empty.png"),844,581);
                if(items.Items.Count==0&&(!Visuals<TextBlock>(root).Any(t=>t.Visibility==Visibility.Visible&&t.Text.StartsWith("No links in this category"))||name.IsEnabled))throw new Exception("Editor empty state is missing.");
                dialog.Close();return false;
            };
            Select("work");EditCatalog();Select("software");EditCatalog();presentDialog=normalPresenter;
            if(File.ReadAllText(Path.Combine(profile,"catalog.json"))!=catalogBefore)throw new Exception("Opening editor changed the catalog.");
            if(window.Title!="Local Dashboard"||((Image)Find<MenuItem>("CompactMenu").Icon).Tag.ToString()!="compact")throw new Exception("Release title or Compact icon differs.");
            foreach(string theme in new[]{"Dark","Light"}){
                DashboardTheme.Apply(theme);
                foreach(var menu in Find<Menu>("AppMenu").Items.OfType<MenuItem>().Where(m=>m.HasItems)){
                    menu.ApplyTemplate();
                    var popup=(System.Windows.Controls.Primitives.Popup)menu.Template.FindName("PART_Popup",menu);
                    var chrome=(FrameworkElement)popup.Child;
                    chrome.Measure(new Size(500,700));chrome.Arrange(new Rect(new Point(),chrome.DesiredSize));chrome.UpdateLayout();
                    var surface=(Border)menu.Template.FindName("PopupSurface",menu);
                    if(surface.BorderThickness!=new Thickness(0)||popup.HorizontalOffset+surface.Margin.Left!=0||popup.VerticalOffset+surface.Margin.Top!=5)throw new Exception("Menu surface placement or border changed.");
                    if(!(surface.Effect is System.Windows.Media.Effects.DropShadowEffect))throw new Exception("Menu shadow is missing.");
                    int menuWidth=(int)Math.Ceiling(chrome.DesiredSize.Width),menuHeight=(int)Math.Ceiling(chrome.DesiredSize.Height);
                    popup.Child=null;var proof=new Border{Background=(System.Windows.Media.Brush)Application.Current.Resources["Theme202020"],Padding=new Thickness(12),Child=chrome};
                    Capture(proof,Path.Combine(profile,"menu-"+theme+"-"+SafeName(menu.Header.ToString())+".png"),menuWidth+24,menuHeight+24);
                    proof.Child=null;popup.Child=chrome;
                    if(popup.IsOpen)throw new Exception("Menu inspection opened a native popup.");
                }
            }
            DashboardTheme.Apply("Dark");
            Select("software");CaptureShell(profile,"review-software.png",1120,820);
            foreach(var button in Visuals<Button>(Find<StackPanel>("Navigation")).Where(b=>b.Content is DockPanel)){
                foreach(var label in ((DockPanel)button.Content).Children.OfType<TextBlock>())if(label.VerticalAlignment!=VerticalAlignment.Center)throw new Exception("Sidebar label is not centered in icon row.");
            }
            foreach(var b in Visuals<Button>(window.Content as DependencyObject).Where(b=>b.ActualHeight>0)){
                if(b.ActualHeight!=(Equals(b.Tag,"sidebar-small-action")?30:36))throw new Exception("Button height differs: "+b.Content);
            }
            if(window.IsVisible||new System.Windows.Interop.WindowInteropHelper(window).Handle!=IntPtr.Zero)throw new Exception("Native window created.");
            Console.WriteLine("Review polish smoke passed: runtime XAML, three Tasks sections, persistent selection markers, collapsed details, Settings, Help and smaller About mark. Offscreen only; no native windows.");
        }finally{refreshTimer.Stop();tray.Dispose();trayIcon.Dispose();}
    }
    void VerifyPersonalEditorBehavior(){
        var presenter=presentDialog;string file=Path.Combine(profile,"personal-editor-ui.txt");
        string text="1. Organize family photos\n   Sort pictures into dated folders.\n   Status: Planning.\n\n   Notes:\n   Keep the originals.\n\n2. Review the garden plan\n   Check the planting list.\n";
        string checklistHash=ChecklistLocation.Token(ChecklistLocation.PathFor(profile));
        File.WriteAllText(file,text);WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="local",path=file},WorkSources.Token(profile,true),true);
        WorkSources.Store(profile,WorkSources.Read(profile,true),WorkSources.Token(profile,true),WorkSources.Fetch(WorkSources.Read(profile,true)),true);
        personalTasksView=true;assistantWorkView=false;agentUpdatesView=false;Select("work");
        Func<Window,string,TextBox> field=(dialog,name)=>{var root=(UIElement)dialog.Content;root.Measure(new Size(684,671));root.Arrange(new Rect(0,0,684,671));root.UpdateLayout();return Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)==name);};
        Action<Window,string> click=(dialog,name)=>ClickIn(dialog.Content as DependencyObject,name);
        try{
            if(heading.Text!="Tasks"||Find<WrapPanel>("CompactToolbar").Children.OfType<Button>().Any(b=>Equals(b.Tag,"work-settings")))throw new Exception("Personal heading or toolbar Settings removal failed.");
            CaptureShell(profile,"editor-personal-tasks.png",1120,820);
            presentDialog=dialog=>{
                Capture(dialog.Content as UIElement,Path.Combine(profile,"editor-personal-form.png"),684,671);AuditControlSizes(dialog.Content as DependencyObject);
                field(dialog,"Title").Text="Cancelled title";click(dialog,"Cancel");return false;
            };
            var block=ReadableTasks.Parse(text).Blocks.First(b=>b.Kind=="task");EditPersonalTask(text,block);
            if(File.ReadAllText(file)!=text)throw new Exception("Cancel changed the personal document.");
            presentDialog=dialog=>{
                field(dialog,"Title").Text="Sort family photos";field(dialog,"Brief description").Text="Start with last year's pictures.";field(dialog,"Longer notes (optional)").Text="Keep originals and a backup.";
                click(dialog,"Save");return true;
            };EditPersonalTask(text,block);
            string saved=File.ReadAllText(file);if(!saved.Contains("Sort family photos")||!saved.Contains("Status: Planning.")||!saved.Contains("Keep originals and a backup.")||!saved.Contains("2. Review the garden plan"))throw new Exception("Personal form save lost task information.");
            presentDialog=dialog=>{
                field(dialog,"Title").Text="Stale draft";File.AppendAllText(file,"Outside edit retained.\n");click(dialog,"Save");
                if(field(dialog,"Title").Text!="Stale draft"||!Visuals<TextBlock>(dialog.Content as DependencyObject).Any(t=>t.Text.Contains("file changed outside")))throw new Exception("Conflict did not retain and explain the open draft.");
                Capture(dialog.Content as UIElement,Path.Combine(profile,"editor-conflict.png"),684,671);click(dialog,"Cancel");return false;
            };EditPersonalTask(saved,ReadableTasks.Parse(saved).Blocks.First(b=>b.Kind=="task"));
            if(File.ReadAllText(file)!=saved+"Outside edit retained.\n")throw new Exception("Conflicting editor overwrote an outside edit.");
            presentDialog=dialog=>{field(dialog,"Title").Text="Plan the weekend";field(dialog,"Brief description").Text="Choose a few things to do.";field(dialog,"Longer notes (optional)").Text="Check the weather.";click(dialog,"Save");return true;};EditPersonalTask(null,null);
            if(!File.ReadAllText(file).Contains("3. Plan the weekend"))throw new Exception("Add personal task failed.");
            presentDialog=dialog=>{var doc=field(dialog,"Document text");doc.Text+="\nA plain note with no fields.\n";click(dialog,"Save");return true;};EditPersonalTask(null,null,true);
            if(!File.ReadAllText(file).Contains("A plain note with no fields."))throw new Exception("Full document editing failed.");
            personalTasksView=false;assistantWorkView=true;RenderCompact(true);if(heading.Text!="Tasks"||Visuals<Button>(rows).Any(b=>Equals(b.Tag,"personal-edit")))throw new Exception("Agent tasks must remain read-only.");
            assistantWorkView=false;RenderCompact(true);if(heading.Text!="Tasks"||ChecklistLocation.Token(ChecklistLocation.PathFor(profile))!=checklistHash)throw new Exception("Checklists label or data preservation failed.");
            SetCompact(true);if(Find<WrapPanel>("CompactToolbar").Children.OfType<Button>().Any(b=>Equals(b.Tag,"work-settings")))throw new Exception("Compact toolbar retained Settings.");SetCompact(false);
            if(!Visuals<Button>(Find<StackPanel>("SidebarFooter")).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Settings")||Find<MenuItem>("SettingsMenu")==null)throw new Exception("Permanent Settings routes were lost.");
            if(new System.Windows.Interop.WindowInteropHelper(window).Handle!=IntPtr.Zero)throw new Exception("Editor tests created a native window.");
            Console.WriteLine("Personal editor UI passed: matched headings, Save/Cancel/Add/text editing, retained conflict draft, read-only Agent tasks, unchanged checklist, toolbar removal and remaining Settings routes. No native window created.");
        }finally{presentDialog=presenter;personalTasksView=false;assistantWorkView=true;Select("software");}
    }
    void VerifyTaskListBehavior(){
        bool oldAgent=assistantWorkView,oldPersonal=personalTasksView,oldReports=agentUpdatesView;
        try{
            agentUpdatesView=false;assistantWorkView=true;personalTasksView=false;Select("work");RenderCompact(true);
            var navigation=Find<StackPanel>("WorkNavigation");var tabs=navigation.Children.OfType<WrapPanel>().Single().Children.OfType<Button>().ToArray();
            if(tabs.Length!=3||!tabs.Select(b=>(string)b.Content).SequenceEqual(new[]{TaskLabel(true),PersonalTaskLabel(),TaskLabel(false)}))throw new Exception("Task sections are out of order.");
            string input="## Active projects\n### Sample project\n1. Review a draft\n   Read the short description.\n   Status: Waiting for review.\n   Next: Inspect the preview.\n\n   Notes:\n   Keep every supporting detail.\n\n## Later / on hold\n### Future project\n2. Deferred task\n   Do not start now.\n";
            rows.Children.Clear();RenderReadableTaskText(input,rows);
            if(Visuals<Expander>(rows).Any(e=>e.IsExpanded)||!Visuals<TextBlock>(rows).Any(t=>t.Text=="Read the short description.")||!Visuals<Expander>(rows).Any(e=>Equals(e.Header,"Deferred work · Do not start")))throw new Exception("Readable task fields or collapsed notes are missing.");
            CaptureShell(profile,"task-lists-agent-preview.png",1120,820);
            double y=navigation.TranslatePoint(new Point(),window).Y;Find<ScrollViewer>("ContentScroll").ScrollToVerticalOffset(100);window.UpdateLayout();
            if(Math.Abs(navigation.TranslatePoint(new Point(),window).Y-y)>.01)throw new Exception("Task buttons move when content scrolls.");
            personalTasksView=true;assistantWorkView=false;RenderCompact(true);rows.Children.Clear();RenderReadableTaskText("1. Organize family photos\n   Sort pictures into dated folders.\n\n   Notes:\n   Keep the original files.\n\n2. Review the garden plan\n   Check the planting list.",rows);CaptureShell(profile,"task-lists-personal-preview.png",1120,820);
            SetCompact(true);rows.Children.Clear();RenderReadableTaskText(File.ReadAllText(Path.Combine(profile,"Personal Tasks.txt")),rows);Find<ScrollViewer>("ContentScroll").ScrollToHome();CaptureShell(profile,"task-lists-compact-preview.png",(int)window.MinWidth,760);SetCompact(false);
            assistantWorkView=true;personalTasksView=false;RenderCompact(true);rows.Children.Clear();RenderReadableTaskText(File.ReadAllText(Path.Combine(profile,"Project Tasks.txt")),rows);Find<ScrollViewer>("ContentScroll").ScrollToHome();CaptureShell(profile,"task-lists-agent-real.png",1120,820);
            assistantWorkView=false;personalTasksView=true;RenderCompact(true);rows.Children.Clear();RenderReadableTaskText(File.ReadAllText(Path.Combine(profile,"Personal Tasks.txt")),rows);Find<ScrollViewer>("ContentScroll").ScrollToHome();CaptureShell(profile,"task-lists-personal-real.png",1120,820);
            personalTasksView=false;assistantWorkView=false;RenderCompact(true);CaptureShell(profile,"task-lists-checklist-preview.png",1120,820);
            Console.WriteLine("Task layout passed: three ordered fixed buttons, visible descriptions, optional fields, collapsed notes, deferred warning and unchanged checklist renderer.");
        }finally{assistantWorkView=oldAgent;personalTasksView=oldPersonal;agentUpdatesView=oldReports;Select("software");}
    }
}
