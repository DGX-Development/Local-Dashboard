using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    void VerifyFirstRun(){
        verifying=nonWindowedVerification=true;
        try{
            if(!layout.items.Select(i=>i.label).SequenceEqual(new[]{"Projects","Documents","Websites","All links"}))throw new Exception("Fresh sidebar is not generic.");
            if(entries.Count!=3||new[]{"software","documents","everyday"}.Any(id=>entries.Count(e=>e.section==id)!=1))throw new Exception("Starter examples differ.");
            foreach(var entry in entries)foreach(var target in entry.targets)AppData.Validate(target);
            foreach(bool personal in new[]{false,true}){
                bool unselected=false;try{WorkSources.Read(profile,personal);}catch(IOException){unselected=true;}
                if(!unselected||WorkSources.Token(profile,personal)!=null)throw new Exception("Fresh task source was selected automatically.");
            }
            CaptureShell(profile,"first-run-dashboard.png",1120,820);
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;
                Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-setup-1.png"),744,681);
                ClickIn(root,"Next");Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-setup-2.png"),744,681);
                foreach(string name in new[]{"Agent tasks list location","Personal tasks list location"})
                    if(Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)==name).Text!="")throw new Exception("Setup prefilled a document source.");
                if(Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist list location").Text!=Path.Combine(profile,".local","current-work.json"))throw new Exception("Checklist default differs.");
                ClickIn(root,"Next");ClickIn(root,"Finish");dialog.Close();return true;
            };
            ShowSetup();
            if(WorkSources.Token(profile)!=null||WorkSources.Token(profile,true)!=null||new ChecklistStore(ChecklistLocation.PathFor(profile),entries).Read().items.Count!=0)throw new Exception("Setup created document choices or checklist entries.");
            string saved=Path.Combine(profile,"Preserved");Directory.CreateDirectory(Path.Combine(saved,".local"));
            string oldPath=Path.Combine(saved,".local","sidebar-layout.json");
            File.WriteAllText(oldPath,"{\"schemaVersion\":1,\"revision\":3,\"items\":[{\"id\":\"software\",\"label\":\"Software projects\"},{\"id\":\"media\",\"label\":\"My custom category\"}],\"orders\":{}}");
            string original=File.ReadAllText(oldPath);var old=new SidebarStore(oldPath).Read();
            if(old.Layout.items.First(i=>i.id=="software").label!="Software projects"||old.Layout.items.First(i=>i.id=="media").label!="My custom category"||File.ReadAllText(oldPath)!=original)throw new Exception("Saved category labels changed.");
            foreach(bool personal in new[]{false,true}){
                var choice=new WorkSourceChoice{schemaVersion=1,kind="local",path=Path.Combine(saved,personal?"personal.txt":"agent.txt")};
                WorkSources.Save(saved,choice,null,personal);if(WorkSources.Read(saved,personal).path!=choice.path)throw new Exception("Saved document selection changed.");
            }
            string originalCatalog=File.ReadAllText(Path.Combine(profile,"catalog.json"));new CatalogDraft(Path.Combine(profile,"catalog.json")).Save();
            if(File.ReadAllText(Path.Combine(profile,"catalog.json"))!=originalCatalog)throw new Exception("Viewing relative examples changed the catalog.");
            if(DashboardHelp.Topics.Any(t=>t.Body.Contains("maintenance guide linked below")||t.Body.Contains("Desktop review uses")||t.Body.Contains("Software projects or Website projects")))throw new Exception("Stale Help remains.");
            Console.WriteLine("First-run checks passed: generic categories and usable examples, blank document selections, empty local checklist, Setup Finish, saved labels/sources preserved, unchanged sample catalog, updated Help. Offscreen only.");
        }finally{Exit();}
    }
    internal static bool NeedsFirstSetup(string profile){return !Directory.Exists(Path.Combine(profile,".local"));}
    void RecordSetupVisit(){DashboardData.Atomic(StatePath("setup.json"),Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"visited\":true}"));}
    void ShowSetup(){ShowSetup(false);}
    void ShowSetup(bool firstRun){
        if(Moving)throw new IOException("Save or cancel the current move first.");
        var root=new DockPanel{Margin=new Thickness(28)};var progress=WorkText("",true);DockPanel.SetDock(progress,Dock.Top);root.Children.Add(progress);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var host=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(host);
        var welcome=new StackPanel();welcome.Children.Add(new TextBlock{Text="Make Dashboard yours",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,18)});welcome.Children.Add(WorkText("A new Dashboard starts with Projects, Documents, Websites and All links. Each of the first three contains one clearly labeled example. Use Edit below the sidebar to choose your own folder, document or website; use Move to rename or reorder categories. Existing profiles keep their saved links and labels. Dashboard also brings three task sections together. You can skip setup and change these choices later in Settings."));welcome.Children.Add(WorkText("Agent tasks",true));welcome.Children.Add(WorkText("A saved text or Markdown list maintained by your agent. Dashboard reads it; it does not send instructions or change the original."));welcome.Children.Add(WorkText("Personal tasks",true));welcome.Children.Add(WorkText("Your separate text or Markdown document. Add or edit tasks in Dashboard, or use your usual editor. Save updates the selected file."));welcome.Children.Add(WorkText("Checklists",true));welcome.Children.Add(WorkText("The list you manage in Dashboard. Add, edit and check off tasks; changes save automatically. Its default location is inside your Dashboard data folder. On a fresh installation, Agent tasks and Personal tasks are unselected until you choose files on the next page. You can leave them empty."));
        Action saveLists;var locations=BuildWorkSource(out saveLists);
        var finish=new StackPanel();finish.Children.Add(new TextBlock{Text="Know where the window goes",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,18)});
        finish.Children.Add(WorkText("X button",true));finish.Children.Add(WorkText(preferenceStore.Value.closeToTray?"Currently, X hides Dashboard in the system tray near the Windows clock. It keeps running.":"Currently, X quits Dashboard."));
        finish.Children.Add(WorkText("Minimize button",true));finish.Children.Add(WorkText(preferenceStore.Value.minimizeToTray?"Currently, minimize also hides Dashboard in the system tray.":"Currently, minimize leaves Dashboard on the taskbar."));
        finish.Children.Add(WorkText("Double-click the tray icon to bring Dashboard back. File > Exit or the tray menu's Exit always quits."));finish.Children.Add(WorkText("Change these choices in Settings > Window. Settings > Appearance controls the theme, accent and task-button labels. Help > Run setup again returns here."));finish.Children.Add(WorkText("Finish saves only the list locations you changed. It does not turn on Windows startup or connect online accounts."));
        var pages=new FrameworkElement[]{welcome,locations,finish};int page=0;bool completed=false;var dialog=Dialog("Set up Local Dashboard",760,720,root);
        var skip=StandardAction(new Button{Content="Skip setup",IsCancel=true});var back=StandardAction(ActionButton("Back",()=>{}));var next=StandardAction(ActionButton("Next",()=>{}));actions.Children.Add(skip);actions.Children.Add(back);actions.Children.Add(next);
        Action render=()=>{progress.Text="Step "+(page+1)+" of 3";host.Content=pages[page];host.ScrollToHome();back.IsEnabled=page>0;next.Content=page==2?"Finish":"Next";};
        back.Click+=(s,e)=>{if(page>0){page--;render();}};
        next.Click+=(s,e)=>Protect(()=>{if(page<2){page++;render();return;}saveLists();RecordSetupVisit();completed=true;if(!nonWindowedVerification)dialog.DialogResult=true;});
        render();try{presentDialog(dialog);}finally{if(firstRun&&!completed)RecordSetupVisit();}
    }
    void ShowIconLicense(){
        string text="";foreach(string resource in new[]{"Fluent.LICENSE","Fluent.NOTICE"})using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(stream==null)throw new IOException("The icon license resource is missing.");using(var reader=new StreamReader(stream))text+=reader.ReadToEnd()+"\r\n\r\n";}
        var root=new DockPanel{Margin=new Thickness(28)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);root.Children.Add(new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});presentDialog(Dialog("Interface icon license",720,620,root));
    }
}
