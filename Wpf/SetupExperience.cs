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
                if(dialog.Title=="Choose source")
                {
                    Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-source-chooser.png"),504,361);
                    foreach(string provider in new[]{"Google Drive","Dropbox","OneDrive"})
                        if(Visuals<Button>(root).Single(button=>Equals(button.Content,provider)).IsEnabled)
                            throw new Exception("Unconnected provider is enabled.");
                    if(!Visuals<Button>(root).Single(button=>Equals(button.Content,"File on this PC")).IsEnabled)
                        throw new Exception("Local source is unavailable.");
                    dialog.Close();return false;
                }
                if(dialog.Title=="Help & user guide")
                {
                    Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-ai-guide.png"),884,581);
                    if(!Visuals<TextBlock>(root).Any(text=>text.Text.Contains(DashboardHelp.AiSetupPrompt))||
                       !Visuals<Button>(root).Any(button=>Equals(button.Content,"Copy setup prompt")&&button.Visibility==Visibility.Visible))
                        throw new Exception("Setup did not open the AI guide and prompt.");
                    dialog.Close();return false;
                }
                Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-setup-1.png"),744,681);
                ClickIn(root,"AI setup guide");
                ClickIn(root,"Next");Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-setup-link.png"),744,681);
                ClickIn(root,"Next");Capture(dialog.Content as UIElement,Path.Combine(profile,"first-run-setup-2.png"),744,681);
                foreach(string name in new[]{"Agent tasks list location","Personal tasks list location"})
                    if(Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)==name).Text!="")throw new Exception("Setup prefilled a document source.");
                if(Visuals<TextBox>(root).Any(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist list location"))throw new Exception("Setup still asks users to choose checklist storage.");
                Visuals<Button>(root).First(button=>Equals(button.Content,"Choose source…")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
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
        var root=new DockPanel{Margin=new Thickness(28)};var progress=WorkText("",true);DockPanel.SetDock(progress,Dock.Top);root.Children.Add(progress);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var host=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(host);
        var dialog=Dialog("Set up Local Dashboard",760,720,root);
        var welcome=new StackPanel();
        welcome.Children.Add(SetupHeading("Try a Dashboard link"));
        welcome.Children.Add(WorkText("Open a sample to see what a Dashboard link does."));
        var samples=new WrapPanel{Margin=new Thickness(0,10,0,14)};
        foreach(var sample in entries.Where(entry=>new[]{"example-project","example-document","example-website"}.Contains(entry.id)))
        {
            var entry=sample;
            var target=entry.targets.FirstOrDefault();
            if(target!=null)samples.Children.Add(SourceAction(entry.name,()=>Protect(()=>AppData.Open(entries,entry.id,target.id,profile))));
        }
        welcome.Children.Add(samples);
        welcome.Children.Add(WorkText("Want help planning your Dashboard?"));
        welcome.Children.Add(SourceAction("AI setup guide",()=>ShowHelp(DashboardHelp.AiSetupTitle,dialog)));

        var catalog=new CatalogDraft(Path.Combine(profile,"catalog.json"));
        Func<Entry> saveLink;
        var firstLink=BuildSetupLink(out saveLink);
        Action saveLists;
        var locations=BuildWorkSource(out saveLists,true);
        locations.Children.Insert(0,SetupHeading("Choose your task sources"));
        locations.Children.Insert(1,WorkText("This step is optional. You can choose or change a source later in Settings."));
        var finish=new StackPanel();
        finish.Children.Add(SetupHeading("Ready to use Dashboard"));
        finish.Children.Add(WorkText("Finish saves your first link and any task sources you chose."));
        finish.Children.Add(WorkText("Use Edit in the Dashboard sidebar to add another link or organize your categories."));
        finish.Children.Add(WorkText("Switch to Tasks from the sidebar when you want to work with your lists. You can rename the task buttons later in Settings > Tasks."));
        var pages=new FrameworkElement[]{welcome,firstLink,locations,finish};
        int page=0;
        bool completed=false;
        Entry added=null;
        var skip=StandardAction(new Button{Content="Skip setup",IsCancel=true});var back=StandardAction(ActionButton("Back",()=>{}));var next=StandardAction(ActionButton("Next",()=>{}));actions.Children.Add(skip);actions.Children.Add(back);actions.Children.Add(next);
        Action render=()=>{progress.Text="Step "+(page+1)+" of "+pages.Length;host.Content=pages[page];host.ScrollToHome();back.IsEnabled=page>0;next.Content=page==pages.Length-1?"Finish":"Next";};
        back.Click+=(s,e)=>{if(page>0){page--;render();}};
        next.Click+=(s,e)=>Protect(()=>{
            if(page<pages.Length-1){page++;render();return;}
            var pending=saveLink();
            if(pending!=null&&added==null)
            {
                var target=pending.targets.Single();
                var entry=catalog.Add(pending.section,target.kind);
                entry.name=pending.name;entry.targets[0].path=target.path;entry.targets[0].url=target.url;
                try{catalog.Save();added=entry;}catch{catalog.Remove(entry);throw;}
            }
            saveLists();
            RecordSetupVisit();completed=true;
            if(!nonWindowedVerification)dialog.DialogResult=true;
        });
        render();try{presentDialog(dialog);}finally{if(firstRun&&!completed)RecordSetupVisit();}
        if(added!=null)
        {
            Reload();Select(added.section);
            var row=rows.Children.OfType<Border>().FirstOrDefault(item=>Equals(item.Tag,added.id));
            if(row!=null)
            {
                row.BringIntoView();
                var button=Visuals<Button>(row).FirstOrDefault();
                if(button!=null)button.Focus();
            }
        }
    }
    static TextBlock SetupHeading(string text)
    {
        return new TextBlock{Text=text,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,12)};
    }

    StackPanel BuildSetupLink(out Func<Entry> readDraft)
    {
        var body=new StackPanel();
        body.Children.Add(SetupHeading("Add your first link"));
        body.Children.Add(WorkText("What do you open often? Add one here, or leave it blank and choose Next."));
        var name=new TextBox{MaxLength=120};
        Field(body,"Name",name);
        body.Children.Add(WorkText("Opens",true));
        var kind=new ComboBox{ItemsSource=new[]{"Folder","Document","Website"},SelectedIndex=0};
        body.Children.Add(kind);
        var location=new TextBox();
        Func<string> kindValue=()=>new[]{"folder","document","web"}[Math.Max(0,kind.SelectedIndex)];
        var browse=SourceAction("Browse…",()=>Browse(location,new LinkTarget{kind=kindValue()}));
        var locationLabel=Field(body,"Folder",location,browse);
        body.Children.Add(WorkText("Category",true));
        var category=new ComboBox{ItemsSource=layout.items.Where(item=>item.id!="all"&&SidebarStore.Known(item.id)).ToList(),
            ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding label}'/></DataTemplate>")};
        body.Children.Add(category);
        Action suggest=()=>{
            locationLabel.Text=(string)kind.SelectedItem=="Website"?"Website address":(string)kind.SelectedItem;
            browse.Visibility=kindValue()=="web"?Visibility.Collapsed:Visibility.Visible;
            System.Windows.Automation.AutomationProperties.SetName(location,locationLabel.Text);
            string preferred=kindValue()=="web"?"everyday":kindValue()=="document"?"documents":"software";
            category.SelectedItem=category.Items.OfType<SidebarItem>().FirstOrDefault(item=>item.id==preferred)
                ??category.Items.OfType<SidebarItem>().FirstOrDefault();
        };
        kind.SelectionChanged+=(sender,args)=>suggest();
        suggest();
        Func<Entry> draft=()=>{
            if(String.IsNullOrWhiteSpace(name.Text)&&String.IsNullOrWhiteSpace(location.Text))return null;
            var group=category.SelectedItem as SidebarItem;
            if(group==null)throw new IOException("Choose a Dashboard category, or add the link later in Edit.");
            if(String.IsNullOrWhiteSpace(name.Text)||String.IsNullOrWhiteSpace(location.Text))throw new IOException("Enter a name and location for your link.");
            string type=kindValue(),address=location.Text.Trim();
            var target=new LinkTarget{id="open",kind=type,path=type=="web"?null:address,url=type=="web"?address:null};
            AppData.Validate(target);
            return new Entry{id="setup-draft",name=name.Text.Trim(),section=group.id,targets=new System.Collections.Generic.List<LinkTarget>{target}};
        };
        body.Children.Add(SourceAction("Try link",()=>Protect(()=>{
            var entry=draft();
            if(entry==null)throw new IOException("Enter a name and location first.");
            AppData.Open(new System.Collections.Generic.List<Entry>{entry},entry.id,"open",profile);
        })));
        readDraft=draft;
        return body;
    }

    void ShowIconLicense(){
        string text="";foreach(string resource in new[]{"Fluent.LICENSE","Fluent.NOTICE"})using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(stream==null)throw new IOException("The icon license resource is missing.");using(var reader=new StreamReader(stream))text+=reader.ReadToEnd()+"\r\n\r\n";}
        var root=new DockPanel{Margin=new Thickness(28)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);root.Children.Add(new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});presentDialog(Dialog("Interface icon license",720,620,root));
    }
}
