using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal static class SafeBackupVerification {
    static void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static void Reject(Action action,string label){bool rejected=false;try{action();}catch{rejected=true;}Check(rejected,label);}
    internal static void Run(string profile){
        string target=Path.Combine(profile,"SafeBackup");if(Directory.Exists(target))throw new IOException("Use a fresh backup test profile.");Directory.CreateDirectory(Path.Combine(target,".local"));File.Copy(Path.Combine(profile,"catalog.json"),Path.Combine(target,"catalog.json"));
        string agent=Path.Combine(profile,"external-agent.md"),personal=Path.Combine(profile,"external-checklist.json");File.WriteAllText(agent,"# Agent tasks\r\n- Keep the original\r\n",new UTF8Encoding(false));
        var entries=AppData.ReadEntries(File.ReadAllText(Path.Combine(target,"catalog.json")));new ChecklistStore(personal,entries).Upsert(new WorkPatch{expectedRevision=0,id="safe-copy",title="Keep my checklist"});
        ChecklistLocation.Choose(target,personal,null,null,ChecklistLocation.Token(personal),entries);WorkSources.Save(target,new WorkSourceChoice{schemaVersion=1,kind="local",path=agent},WorkSources.Token(target));
        string personalTasks=Path.Combine(profile,"external-personal.txt");File.WriteAllText(personalTasks,"Personal tasks\r\n1. Keep the appointment\r\n   Description: Original personal text.\r\n",new UTF8Encoding(false));
        WorkSources.Save(target,new WorkSourceChoice{schemaVersion=1,kind="local",path=personalTasks},WorkSources.Token(target,true),true);
        string originalPersonalTasks=ChecklistLocation.Token(personalTasks),personalSourceToken=WorkSources.Token(target,true);
        string originalAgent=ChecklistLocation.Token(agent),originalPersonal=ChecklistLocation.Token(personal),sourceToken=WorkSources.Token(target),locationToken=ChecklistLocation.Token(ChecklistLocation.Config(target));
        string backupFile=Path.Combine(profile,"all-task-lists.json");var bundle=DashboardData.ExportWithLists(target,backupFile);
        Check(bundle.schemaVersion==5&&new[]{"agent","personal","checklist"}.All(role=>bundle.lists[role].status=="included"),"backup includes all three external task lists");
        Check(bundle.lists["personal"].sha256==originalPersonalTasks,"personal task copy is byte exact");
        Reject(()=>DashboardData.ExportWithLists(target,personalTasks),"backup cannot overwrite selected Personal tasks");
        Check(bundle.lists["agent"].sha256==originalAgent&&bundle.lists["checklist"].sha256==originalPersonal,"backup copies are byte exact");
        Reject(()=>DashboardData.ExportWithLists(target,personal),"backup cannot overwrite selected checklist");Reject(()=>DashboardData.ExportWithLists(target,agent),"backup cannot overwrite selected Agent tasks");
        var broken=DashboardData.Load(backupFile);broken.lists["agent"].sha256="bad";Reject(()=>DashboardData.RestoreCopies(target,broken,true),"tampered task copy is rejected before restore");
        broken=DashboardData.Load(backupFile);broken.files["../escape"]=null;Reject(()=>DashboardData.RestoreCopies(target,broken,true),"safe restore refuses unexpected configuration paths");
        var restored=DashboardData.RestoreCopies(target,bundle,false);
        Check(ChecklistLocation.Token(restored.Personal)==originalPersonalTasks&&WorkSources.Token(target,true)==personalSourceToken,"default restore creates an exact personal copy and keeps its selection");
        Check(ChecklistLocation.Token(restored.Agent)==originalAgent&&ChecklistLocation.Token(restored.Checklist)==originalPersonal,"restore creates exact separate task copies");
        Check(WorkSources.Token(target)==sourceToken&&ChecklistLocation.Token(ChecklistLocation.Config(target))==locationToken,"default restore keeps both current list selections");
        Check(ChecklistLocation.Token(agent)==originalAgent&&ChecklistLocation.Token(personal)==originalPersonal,"restore preserves both original external lists");
        var selected=DashboardData.RestoreCopies(target,bundle,true);
        Check(WorkSources.Read(target,true).path==selected.Personal&&ChecklistLocation.Token(personalTasks)==originalPersonalTasks,"explicit restore selects the separate personal copy and preserves original");
        Check(WorkSources.Read(target).path==selected.Agent&&ChecklistLocation.PathFor(target)==selected.Checklist,"explicit selection uses restored copies");
        Check(selected.Folder!=restored.Folder,"repeated restore creates a new destination");
        var checklist=new ChecklistStore(selected.Checklist,entries);var state=checklist.Read();checklist.Complete(state.revision,"safe-copy",true);Check(ChecklistLocation.Token(personal)==originalPersonal,"editing restored checklist leaves original unchanged");
        string beforeSource=WorkSources.Token(target),beforePersonalSource=WorkSources.Token(target,true),beforeLocation=ChecklistLocation.Token(ChecklistLocation.Config(target)),catalog=ChecklistLocation.Token(Path.Combine(target,"catalog.json"));
        Reject(()=>DashboardData.RestoreCopies(target,bundle,true,i=>{if(i==5)throw new IOException("Injected restore interruption");}),"safe restore surfaces interrupted write");
        Check(beforeSource==WorkSources.Token(target)&&beforeLocation==ChecklistLocation.Token(ChecklistLocation.Config(target))&&catalog==ChecklistLocation.Token(Path.Combine(target,"catalog.json")),"safe restore rollback restores configuration and selections");
        Check(beforePersonalSource==WorkSources.Token(target,true)&&ChecklistLocation.Token(personalTasks)==originalPersonalTasks,"interrupted restore also preserves personal selection and original");
        Check(!File.Exists(Path.Combine(target,".local","safe-restore-pending.json")),"successful rollback clears recovery record");
        WorkSources.Save(target,new WorkSourceChoice{schemaVersion=1,kind="local",path=Path.Combine(profile,"missing-agent.txt")},WorkSources.Token(target));
        Check(DashboardData.CurrentRestoreList(target,"agent",entries)==RestoreListStatus.Unavailable&&DashboardData.CurrentRestoreList(target,"personal",entries)==RestoreListStatus.Available,"restore distinguishes an unavailable file from a working file");
        var missing=DashboardData.ExportWithLists(target,Path.Combine(profile,"missing-task-list.json"));Check(missing.lists["agent"].status=="unavailable"&&missing.lists["checklist"].status=="included"&&DashboardData.TaskCopySummary(missing).Contains("not included"),"unavailable Agent tasks is reported while checklist is included");
        string unchanged=WorkSources.Token(target);DashboardData.RestoreCopies(target,missing,true);Check(WorkSources.Token(target)==unchanged,"missing restored list preserves its current selection");
        var legacy=DashboardData.Snapshot(target);var legacyCopies=DashboardData.RestoreCopies(target,legacy,false);Check(legacyCopies.Checklist!=null,"older backup can restore checklist as a separate copy");
        Check(ChecklistLocation.Token(agent)==originalAgent&&ChecklistLocation.Token(personal)==originalPersonal,"all backup tests leave original task lists unchanged");
        using(var held=new FileStream(agent,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
            WorkSources.Save(target,new WorkSourceChoice{schemaVersion=1,kind="local",path=agent},WorkSources.Token(target));var unavailable=DashboardData.ExportWithLists(target,Path.Combine(profile,"locked-task-list.json"));Check(unavailable.lists["agent"].status=="unavailable","locked task file is clearly omitted without changing it");
        }
        string pref=Path.Combine(target,".local","window-preferences.json");File.WriteAllText(pref,"{\"startWithWindows\":true}");DashboardData.RestoreCopies(target,bundle,false);Check(AppData.Json.Deserialize<WindowPreferences>(File.ReadAllText(pref)).startWithWindows,"backup with no window preferences preserves local startup choice");
        string fresh=Path.Combine(profile,"Fresh");
        Directory.CreateDirectory(fresh);
        string assets=Path.Combine(target,".local","project-icons");
        Directory.CreateDirectory(assets);
        string icon=Path.Combine(assets,"fixture.png");
        File.WriteAllBytes(icon,Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jL1sAAAAASUVORK5CYII="));
        entries[0].iconPath=icon;
        var missingFolder=Path.Combine(profile,"Unavailable folder");
        entries[0].targets[0].path=missingFolder;
        File.WriteAllText(Path.Combine(target,"catalog.json"),AppData.Json.Serialize(entries));
        var sidebar=new SidebarStore(Path.Combine(target,".local","sidebar-layout.json"));
        var sideBefore=sidebar.Read();
        var sideDraft=SidebarStore.Copy(sideBefore.Layout);
        sideDraft.items[0].label="Recovered projects";
        sideDraft.items[0].iconPath=icon;
        sidebar.Save(sideBefore,sideDraft);
        File.WriteAllText(pref,"{\"theme\":\"Light\",\"startWithWindows\":true}");
        File.WriteAllText(Path.Combine(target,".local","pins.json"),AppData.Json.Serialize(new[]{entries[0].id}));
        WorkSources.Save(target,new WorkSourceChoice{schemaVersion=1,kind="local",path=personalTasks},WorkSources.Token(target,true),true);
        var full=DashboardData.ExportWithLists(target,Path.Combine(profile,"full-backup.json"));
        Check(full.icons.Count==1&&full.icons[icon].sha256==DashboardData.Hash(File.ReadAllBytes(icon)),"backup includes one copy of an icon shared by a link and category");
        File.Delete(icon);
        Check(new[]{"agent","personal","checklist"}.All(role=>DashboardData.CurrentRestoreList(fresh,role,entries)==RestoreListStatus.Unset),
            "fresh profile has no active files to displace during restore");
        var recovered=DashboardData.RestoreCopies(fresh,full,new[]{"agent","personal","checklist"});
        var freshEntries=AppData.ReadEntries(File.ReadAllText(Path.Combine(fresh,"catalog.json")),fresh);
        var recoveredIcon=freshEntries[0].iconPath;
        var restoredSidebar=new SidebarStore(Path.Combine(fresh,".local","sidebar-layout.json")).Read().Layout;
        Check(restoredSidebar.items[0].label=="Recovered projects"&&restoredSidebar.items[0].iconPath==recoveredIcon&&
            File.Exists(recoveredIcon)&&DashboardData.Hash(File.ReadAllBytes(recoveredIcon))==full.icons[icon].sha256,
            "fresh restore reconnects shared category and link icons without the old file");
        Check(freshEntries[0].targets[0].path==missingFolder&&!Directory.Exists(missingFolder),"missing external link path is preserved without creating the folder");
        var restoredPreferences=AppData.Json.Deserialize<WindowPreferences>(File.ReadAllText(Path.Combine(fresh,".local","window-preferences.json")));
        Check(restoredPreferences.theme=="Light"&&!restoredPreferences.startWithWindows&&
            AppData.Json.Deserialize<string[]>(File.ReadAllText(Path.Combine(fresh,".local","pins.json"))).Contains(entries[0].id),
            "fresh restore recovers settings and favorites while preserving local startup choice");
        Check(WorkSources.Read(fresh).path==recovered.Agent&&WorkSources.Read(fresh,true).path==recovered.Personal&&
            ChecklistLocation.PathFor(fresh)==recovered.Checklist&&ChecklistLocation.Token(recovered.Agent)==originalAgent&&
            ChecklistLocation.Token(recovered.Personal)==originalPersonalTasks&&new ChecklistStore(recovered.Checklist,freshEntries).Read().items.Count==1,
            "fresh restore reconnects both byte-exact task lists and the checklist");
        string keptPersonal=WorkSources.Read(fresh,true).path;
        var recovery=DashboardData.Load(DashboardData.CreateRestoreRecoveryBackup(fresh));
        Check(recovery.icons.Values.Any(item=>item.status=="included")&&recovery.lists.Values.All(item=>item.status=="included"),
            "before-restore recovery backup includes the current icon and task copies");
        foreach(bool personalRole in new[]{false,true})
        {
            var choice=WorkSources.Read(fresh,personalRole);
            WorkSources.Store(fresh,choice,WorkSources.Token(fresh,personalRole),WorkSources.Fetch(choice,fresh),personalRole);
            Check(!String.IsNullOrWhiteSpace(WorkSources.Cached(fresh,choice).text),"a recovered task list can be loaded into Dashboard");
        }
        var mixed=DashboardData.RestoreCopies(fresh,full,new[]{"agent"});
        Check(WorkSources.Read(fresh).path==mixed.Agent&&WorkSources.Read(fresh,true).path==keptPersonal&&
            ChecklistLocation.PathFor(fresh)==recovered.Checklist,"restore choices operate independently");
        var old=DashboardData.Load(Path.Combine(profile,"full-backup.json"));old.schemaVersion=4;old.icons=null;
        DashboardData.RestoreCopies(fresh,old,new string[0]);
        Check(WorkSources.Read(fresh,true).path==keptPersonal&&DashboardData.BackupIconSummary(old).Contains("paths only"),
            "older version 4 backup restores and explains its missing icon payload");

        var names=new[]{"catalog.json",".local/window-preferences.json",".local/sidebar-layout.json",".local/pins.json",".local/work-list-source.json",".local/checklist-location.json"};
        var journal=new SafeRestoreJournal{schemaVersion=1,before=new Dictionary<string,string>(),after=new Dictionary<string,string>()};
        foreach(string name in names){string file=Path.Combine(target,name.Replace('/',Path.DirectorySeparatorChar));journal.before[name]=journal.after[name]=File.Exists(file)?Convert.ToBase64String(File.ReadAllBytes(file)):null;}
        journal.after[".local/window-preferences.json"]=Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"startWithWindows\":true,\"theme\":\"Light\"}"));
        string pending=Path.Combine(target,".local","safe-restore-pending.json");File.WriteAllText(pending,AppData.Json.Serialize(journal));File.WriteAllBytes(pref,Convert.FromBase64String(journal.after[".local/window-preferences.json"]));DashboardData.RecoverSafe(target);
        Check(Convert.ToBase64String(File.ReadAllBytes(pref))==journal.before[".local/window-preferences.json"]&&!File.Exists(pending),"safe interrupted restore recovers on startup");
        File.WriteAllText(pending,AppData.Json.Serialize(journal));File.WriteAllText(pref,"{\"theme\":\"Dark\",\"externalChange\":true}");Reject(()=>DashboardData.RecoverSafe(target),"safe recovery refuses divergent later edits");Check(File.ReadAllText(pref).Contains("externalChange")&&File.Exists(pending),"divergent recovery preserves file and journal");File.WriteAllBytes(pref,Convert.FromBase64String(journal.before[".local/window-preferences.json"]));DashboardData.RecoverSafe(target);
    }
}
internal sealed partial class WpfDashboard {
    void VerifyNextBehavior(){
        var presenter=presentDialog;string setupPath=StatePath("setup.json"),setupBefore=File.Exists(setupPath)?File.ReadAllText(setupPath):null;
        string sourceBefore=WorkSources.Token(profile),locationBefore=ChecklistLocation.Token(ChecklistLocation.Config(profile));
        try{
            presentDialog=dialog=>{Capture(dialog.Content as UIElement,Path.Combine(profile,"next-settings.png"),764,661);if(lastSettingsPage!="Appearance")throw new Exception("Ordinary Settings did not open Appearance.");var root=dialog.Content as DependencyObject;
                foreach(var dot in Visuals<Border>(root).Where(b=>Equals(b.Tag,"accent-dot"))){var button=Ancestor<Button>(dot);var center=dot.TranslatePoint(new Point(7,7),button);if(Math.Abs(center.Y-button.ActualHeight/2)>1)throw new Exception("Accent dot is not vertically centered.");}
                ClickIn(root,"Tasks");dialog.Close();return false;};Settings();Settings();
            bool hasPublisher=false;presentDialog=dialog=>{Capture(dialog.Content as UIElement,Path.Combine(profile,"next-about.png"),544,611);hasPublisher=Visuals<Image>(dialog.Content as DependencyObject).Any(i=>Equals(i.Tag,"publisher")&&i.Source is DrawingImage);dialog.Close();return false;};ShowAbout();if(!hasPublisher)throw new Exception("About publisher is not vector artwork.");
            presentDialog=dialog=>{var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"next-setup-1.png"),744,681);ClickIn(root,"Next");Capture(dialog.Content as UIElement,Path.Combine(profile,"next-setup-2.png"),744,681);AuditControlSizes(root);ClickIn(root,"Next");Capture(dialog.Content as UIElement,Path.Combine(profile,"next-setup-3.png"),744,681);ClickIn(root,"Back");ClickIn(root,"Next");dialog.Close();return false;};ShowSetup();
            if(sourceBefore!=WorkSources.Token(profile)||locationBefore!=ChecklistLocation.Token(ChecklistLocation.Config(profile))||(File.Exists(setupPath)?File.ReadAllText(setupPath):null)!=setupBefore)throw new Exception("Skipping repeated setup changed stored choices.");
            presentDialog=dialog=>{Capture(dialog.Content as UIElement,Path.Combine(profile,"next-license.png"),704,581);if(!Visuals<TextBox>(dialog.Content as DependencyObject).Any(t=>t.Text.Contains("Permission is hereby granted")))throw new Exception("Fluent license not packaged.");dialog.Close();return false;};ShowIconLicense();
            if(NeedsFirstSetup(profile))throw new Exception("Existing profile incorrectly offered first setup.");
            string fresh=Path.Combine(profile,"FreshSetup");Directory.CreateDirectory(fresh);if(!NeedsFirstSetup(fresh))throw new Exception("Fresh profile was not offered setup.");
            presentDialog=dialog=>{Capture(dialog.Content as UIElement,Path.Combine(profile,"next-setup-finish.png"),744,681);var root=dialog.Content as DependencyObject;ClickIn(root,"Next");ClickIn(root,"Next");ClickIn(root,"Finish");dialog.Close();return true;};ShowSetup();
            if(!File.Exists(setupPath)||sourceBefore!=WorkSources.Token(profile)||locationBefore!=ChecklistLocation.Token(ChecklistLocation.Config(profile)))throw new Exception("Setup Finish failed or changed untouched selections.");
            Console.WriteLine("Next build UI checks passed: Appearance entry, centered swatches, vector publisher, setup navigation/skip and embedded Fluent license.");
        }finally{presentDialog=presenter;}
    }
    static T Ancestor<T>(DependencyObject item) where T:DependencyObject {for(var p=VisualTreeHelper.GetParent(item);p!=null;p=VisualTreeHelper.GetParent(p))if(p is T)return (T)p;throw new Exception("Expected parent not found.");}
}
