using System.Collections.Generic;
using System.Windows.Media;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    static void CloseoutCheck(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS "+name);}
    void VerifyMixedDestinationIcon(Uri origin)
    {
        string catalog=Path.Combine(profile,"catalog.json");
        string original=File.ReadAllText(catalog);
        var originalPresenter=presentDialog;
        var mixed=new Entry{id="mixed-icon-fixture",name="Media project",section="category-"+Guid.NewGuid().ToString("N"),
            targets=new List<LinkTarget>{
                new LinkTarget{id="folder",kind="folder",path=profile,label="Folder"},
                new LinkTarget{id="site",kind="web",url=origin.AbsoluteUri,label="Website"}
            }};
        try
        {
            var fixture=AppData.ReadEntries(original,profile);
            fixture.Add(mixed);
            File.WriteAllText(catalog,AppData.Json.Serialize(fixture));
            Reload();
            Select(mixed.section);
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                surface.Measure(new Size(1100,780));surface.Arrange(new Rect(0,0,1100,780));surface.UpdateLayout();
                var button=Visuals<Button>(surface).Single(item=>Equals(item.Content,"Use website icon"));
                CloseoutCheck(button.Visibility==Visibility.Collapsed,"a folder destination offers a local image");
                var destinations=Visuals<ListBox>(surface).Single(item=>item.Items.Count==2&&item.Items[0] is LinkTarget);
                destinations.SelectedIndex=1;
                CloseoutCheck(button.Visibility==Visibility.Visible,"a website destination in a custom category offers its favicon");
                ClickIn(surface,"Use website icon");
                ClickIn(surface,"Save");
                dialog.Close();
                return true;
            };
            EditCatalog();
            Reload();
            var saved=entries.Single(entry=>entry.id==mixed.id);
            CloseoutCheck(saved.iconPath==WebsiteIcons.CachePath(profile,origin)&&EntryIcon(saved,profile) is System.Windows.Media.Imaging.BitmapSource,
                "a custom category link keeps its selected website icon after reopening the catalog");
            string backup=Path.Combine(profile,"mixed-icon-backup.json");
            DashboardData.ExportWithLists(profile,backup);
            string fresh=Path.Combine(profile,"MixedFresh");
            Directory.CreateDirectory(fresh);
            DashboardData.RestoreCopies(fresh,DashboardData.Load(backup),false);
            var restored=AppData.ReadEntries(File.ReadAllText(Path.Combine(fresh,"catalog.json")),fresh).Single(entry=>entry.id==mixed.id);
            CloseoutCheck(restored.iconWebsiteTargetId=="site"&&restored.iconPath!=saved.iconPath&&EntryIcon(restored,fresh) is System.Windows.Media.Imaging.BitmapSource,
                "the selected website icon displays after restoring configuration into a fresh profile");
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                surface.Measure(new Size(1100,780));surface.Arrange(new Rect(0,0,1100,780));surface.UpdateLayout();
                Visuals<ListBox>(surface).Single(item=>item.Items.Count==2&&item.Items[0] is LinkTarget).SelectedIndex=1;
                Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Website address")
                    .Text="https://updated.invalid/";
                ClickIn(surface,"Save");
                dialog.Close();
                return true;
            };
            EditCatalog();
            var changed=entries.Single(entry=>entry.id==mixed.id);
            CloseoutCheck(changed.iconPath==null&&WebsiteIcons.Origin(changed).Host=="updated.invalid",
                "changing the selected website discards its old favicon and resolves the new address");
        }
        finally
        {
            File.WriteAllText(catalog,original);
            presentDialog=originalPresenter;
            Reload();
        }
    }

    void VerifyDeepTaskCache(){
        const string formatted="1. Review sample\n   Read the notes.\n   Status: Ready\n   Next: Review\n\n## Later / on hold\n\n2. Deferred example\n   Wait.\n";
        foreach(bool personal in new[]{false,true}){
            string source=Path.Combine(profile,personal?"Personal sample.txt":"Agent sample.txt");
            string text=personal?"Freeform personal notes\nAnother line.\n":formatted;
            File.WriteAllText(source,text);
            var choice=new WorkSourceChoice{schemaVersion=1,kind="local",path=source};
            WorkSources.Save(profile,choice,WorkSources.Token(profile,personal),personal);
            string token=WorkSources.Token(profile,personal);
            string cachePath=WorkSources.CachePath(profile,choice);
            CloseoutCheck(cachePath.Length+37>=260,"fixture reproduces the former cache temporary path length");
            var fetched=WorkSources.Fetch(WorkSources.Read(profile,personal),profile);
            WorkSources.Store(profile,choice,token,fetched,personal);
            WorkSources.Store(profile,choice,token,fetched,personal);
            string loaded=WorkSources.Cached(profile,choice).text;
            CloseoutCheck(loaded==text&&File.ReadAllText(source)==text,(personal?"Personal":"Agent")+" refresh and replacement preserve all source text");
            var parsed=ReadableTasks.Parse(loaded);
            CloseoutCheck(parsed.Original==text&&(personal
                ? !parsed.Blocks.Any(block=>block.Kind=="task")
                : parsed.Blocks.Any(block=>block.Status=="Ready"&&block.Next=="Review")&&parsed.Blocks.Any(block=>block.Kind=="task"&&block.Deferred)),
                personal?"plain-text fallback retains the full document":"formatted tasks retain Status, Next and deferred work");
        }
    }
    void VerifyTaskSelectionLoads()
    {
        Action save;
        var settings=BuildWorkSource(out save);
        for(int list=1;list<=3;list++)
        {
            string file=Path.Combine(profile,"Task list "+list+" newly selected.txt");
            File.WriteAllText(file,"Task list "+list+" selected sample");
            Visuals<TextBox>(settings).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Task list "+list+" file location").Text=file;
        }
        save();
        var frame=new System.Windows.Threading.DispatcherFrame();
        var deadline=DateTime.UtcNow.AddSeconds(9);
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e)=>{
            if(DateTime.UtcNow>=deadline||taskReadStatus.All(status=>status.StartsWith("Read succeeded")))frame.Continue=false;
        };
        timer.Start();
        try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        for(int list=0;list<3;list++)
        {
            var source=WorkSources.Read(profile,list);
            var cache=WorkSources.Cached(profile,source);
            CloseoutCheck(cache!=null&&cache.text=="Task list "+(list+1)+" selected sample","saving the task location loads the selected file without Refresh");
        }
        var agent=WorkSources.Read(profile);
        Action unusedSetupSave;
        var setupSources=BuildWorkSource(out unusedSetupSave,true);
        setupSources.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,"ThemeF2F2F2");
        Capture(setupSources,Path.Combine(profile,"populated-setup-sources.png"),680,440);
        var thirdPicker=Visuals<Button>(setupSources).Single(button=>System.Windows.Automation.AutomationProperties.GetAutomationId(button)=="task-list-3-choose-source");
        CloseoutCheck(thirdPicker.TranslatePoint(new Point(0,thirdPicker.ActualHeight),setupSources).Y<=440&&
            Visuals<TextBox>(setupSources).Count(box=>box.Visibility==Visibility.Visible)==3,
            "three populated task sources fit in the Setup content area without blank messages");
        var personalSource=WorkSources.Read(profile,true);
        File.WriteAllText(agent.path,"Updated Agent tasks from outside Dashboard");
        File.WriteAllText(personalSource.path,"Updated Personal tasks from outside Dashboard");
        taskListView=0;Select("work");
        var tabs=TaskNavigationPanel();
        ((Button)tabs.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var automaticFrame=new System.Windows.Threading.DispatcherFrame();
        var automaticDeadline=DateTime.UtcNow.AddSeconds(9);
        var automaticTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        automaticTimer.Tick+=(s,e)=>{
            if(DateTime.UtcNow>=automaticDeadline||
                (WorkSources.Cached(profile,agent).text.StartsWith("Updated Agent")&&
                 WorkSources.Cached(profile,personalSource).text.StartsWith("Updated Personal")))automaticFrame.Continue=false;
        };
        automaticTimer.Start();
        try{System.Windows.Threading.Dispatcher.PushFrame(automaticFrame);}finally{automaticTimer.Stop();}
        CloseoutCheck(WorkSources.Cached(profile,agent).text.StartsWith("Updated Agent")&&
            WorkSources.Cached(profile,personalSource).text.StartsWith("Updated Personal"),
            "opening Tasks and switching tabs automatically reload both externally edited text lists");
        var names=preferenceStore.Copy();
        names.personalTasksLabel="Personal Tasks";
        preferenceStore.Save(names,false);
        preferenceStore.Reload();
        ((Button)TaskNavigationPanel().Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CloseoutCheck(heading.Text=="Personal Tasks"&&TaskNavigationPanel().Children.OfType<Button>().Any(button=>
            System.Windows.Automation.AutomationProperties.GetName(button)=="Personal Tasks"),
            "a saved Personal Tasks label survives reload in the sidebar and heading");
        var third=WorkSources.Read(profile,2);
        var personalBefore=File.ReadAllText(personalSource.path);
        var originalPresenter=presentDialog;
        try
        {
            var currentTabs=TaskNavigationPanel();
            ((Button)currentTabs.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var before=File.ReadAllText(third.path);
            presentDialog=dialog=>{
                var root=(UIElement)dialog.Content;
                root.Measure(new Size(1100,780));root.Arrange(new Rect(0,0,1100,780));root.UpdateLayout();
                if(dialog.Title=="Edit Tasks"){
                    ClickIn(root,"Add item");
                    ClickIn(root,"Edit item");
                    ClickIn(root,"Edit document");
                    CloseoutCheck(File.ReadAllText(third.path)==before,"item and document edits stay staged until the sidebar editor saves");
                    ClickIn(root,"Save");
                }else{
                    var fields=Visuals<TextBox>(root);
                    if(dialog.Title=="Edit document")
                        fields.Single(box=>System.Windows.Automation.AutomationProperties.GetName(box)=="Document text").Text+="\nA third-list document note.";
                    else fields.Single(box=>System.Windows.Automation.AutomationProperties.GetName(box)=="Title").Text=
                        dialog.Title=="Add item"?"Third list task":"Edited third list task";
                    ClickIn(root,"Done");
                }
                dialog.Close();return true;
            };
            ClickIn(TaskNavigationPanel(),"Edit");
            var saved=File.ReadAllText(third.path);
            CloseoutCheck(saved.Contains("Edited third list task")&&saved.Contains("A third-list document note.")&&
                File.ReadAllText(personalSource.path)==personalBefore,
                "individual and whole-document editing save to the selected third list");
        }
        finally{presentDialog=originalPresenter;}
        var checklist=new ChecklistStore(ChecklistLocation.PathFor(profile),entries);
        var initial=checklist.Read();
        checklist.Upsert(new WorkPatch{expectedRevision=initial.revision,id="automatic-refresh-test",title="Checklist item added outside the view"});
        tabs=TaskNavigationPanel();
        ((Button)tabs.Children[3]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CloseoutCheck(rows.Children.OfType<StackPanel>().Any(row=>Equals(row.Tag,"automatic-refresh-test")),
            "opening Checklists rereads its current file");
        var taskSearch=Find<DockPanel>("SearchBar");
        CloseoutCheck(taskSearch.Children.OfType<Button>().Any(button=>Equals(button.Tag,"task-refresh")&&button.Visibility==Visibility.Visible)&&
            tabs==Find<StackPanel>("TaskNavigation")&&Find<WrapPanel>("WorkNavigation").Visibility==Visibility.Collapsed,
            "full Tasks uses the sidebar for its lists and the shared search row for Refresh");
        search.Text="added outside";
        CloseoutCheck(rows.Children.OfType<StackPanel>().Any(row=>Equals(row.Tag,"automatic-refresh-test")),"checklist search retains matching items");
        CloseoutCheck(Visuals<Button>(rows).Any(button=>Equals(button.Content,"Current (1)"))&&
            Visuals<Button>(rows).Any(button=>Equals(button.Content,"Done (0)")),"checklist tab counts follow search matches");
        ClickIn(rows,"Current (1)");
        var matchingCheck=Visuals<CheckBox>(rows).Single();
        matchingCheck.IsChecked=true;
        CloseoutCheck(Visuals<Button>(rows).Any(button=>Equals(button.Content,"Current (0)"))&&
            Visuals<Button>(rows).Any(button=>Equals(button.Content,"Done (1)")),"filtered counts follow a completed checklist item");
        search.Text="no match fixture";
        CloseoutCheck(!rows.Children.OfType<StackPanel>().Any(row=>Equals(row.Tag,"automatic-refresh-test")),"checklist search hides nonmatching items");
        ((Button)tabs.Children[2]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        CloseoutCheck(search.Text.Length==0&&heading.Text==TaskListLabel(2),"changing lists clears the search and updates the heading");
        search.Text="unmatched search fixture";
        CloseoutCheck(!rows.Children.OfType<StackPanel>().Any(row=>Equals(row.Tag,"readable-task")),"search scopes results to parsed tasks in the selected list");
        search.Text="edited THIRD list";
        CloseoutCheck(rows.Children.OfType<StackPanel>().Count(row=>Equals(row.Tag,"readable-task"))==1,"task search matches the selected list case-insensitively");
        ((Button)tabs.Children[3]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        string defaultChecklist=ChecklistLocation.PathFor(profile);
        string defaultHash=ChecklistLocation.Token(defaultChecklist);
        string customChecklist=Path.Combine(Path.GetDirectoryName(profile),Path.GetFileName(profile)+" selected checklist.json");
        File.Copy(defaultChecklist,customChecklist);
        ChecklistLocation.Choose(profile,customChecklist,ChecklistLocation.Token(ChecklistLocation.Config(profile)),
            defaultHash,ChecklistLocation.Token(customChecklist),entries);
        RenderCompact(true);
        try{
            presentDialog=dialog=>{
                var root=(UIElement)dialog.Content;
                root.Measure(new Size(1100,780));root.Arrange(new Rect(0,0,1100,780));root.UpdateLayout();
                if(dialog.Title=="Edit Tasks"){
                    ClickIn(root,"Add item");
                    var itemRows=Visuals<ListBox>(root).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="List items");
                    int last=itemRows.Items.Count-1;
                    var moved=itemRows.SelectedItem;
                    System.Windows.Input.ComponentCommands.MoveUp.Execute(null,itemRows);
                    CloseoutCheck(itemRows.SelectedIndex==last-1&&ReferenceEquals(itemRows.SelectedItem,moved),
                        "checklist items use the editor reorder command and keep selection");
                    ClickIn(root,"Save");
                }
                else{
                    Visuals<TextBox>(root).Single(box=>System.Windows.Automation.AutomationProperties.GetName(box)=="Title").Text="Saved to selected file";
                    ClickIn(root,"Done");
                }
                dialog.Close();return true;
            };
            ClickIn(TaskNavigationPanel(),"Edit");
        }finally{presentDialog=originalPresenter;}
        Reload();
        CloseoutCheck(ChecklistLocation.PathFor(profile)==customChecklist&&
            new ChecklistStore(customChecklist,entries).Read().items.First().title=="Saved to selected file"&&
            ChecklistLocation.Token(defaultChecklist)==defaultHash,
            "the checklist editor saves to the selected file and reloads it without changing the default file");
        string customHash=ChecklistLocation.Token(customChecklist);
        Action saveDefault;
        var checklistSettings=BuildWorkSource(out saveDefault);
        var sourceActions=Visuals<Button>(checklistSettings).Select(System.Windows.Automation.AutomationProperties.GetAutomationId)
            .Where(id=>id.EndsWith("-choose-source")||id=="choose-checklist-file");
        CloseoutCheck(sourceActions.SequenceEqual(new[]{"task-list-1-choose-source","task-list-2-choose-source","task-list-3-choose-source","choose-checklist-file"}),
            "Tasks settings lists Task list 1, 2 and 3 before Checklists");
        CloseoutCheck(Visuals<TextBlock>(checklistSettings).Any(text=>text.Text==customChecklist),
            "Tasks settings displays the active checklist path");
        ClickIn(checklistSettings,"Use default checklist");
        saveDefault();
        CloseoutCheck(ChecklistLocation.PathFor(profile)==defaultChecklist&&
            ChecklistLocation.Token(defaultChecklist)==defaultHash&&ChecklistLocation.Token(customChecklist)==customHash,
            "switching back to the default checklist preserves both files");
        Select("software");
    }
    static void VerifyEditorReorder(Window dialog,string firstCategory,string firstLink)
    {
        if(!dialog.IsVisible){var surface=(UIElement)dialog.Content;surface.Measure(new Size(904,681));surface.Arrange(new Rect(0,0,904,681));surface.UpdateLayout();}
        var root=(DependencyObject)dialog.Content;
        var categories=Visuals<ListBox>(root).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Section");
        var links=Visuals<ListBox>(root).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Dashboard entries");
        if(!Equals(categories.SelectedValue,"software")||((Entry)links.Items[0]).id!=firstLink)
            throw new InvalidOperationException("Edit did not open to the current category and order.");
        categories.SelectedIndex=0;
        if(!Equals(categories.SelectedValue,firstCategory))
            throw new InvalidOperationException("Edit did not show the current category order.");
        System.Windows.Input.ComponentCommands.MoveDown.Execute(null,categories);
        if(((SidebarItem)categories.Items[1]).id!=firstCategory)
            throw new InvalidOperationException("Category did not move in the editor.");
        categories.SelectedValue="documents";
        categories.SelectedValue="software";
        links.SelectedIndex=0;
        System.Windows.Input.ComponentCommands.MoveDown.Execute(null,links);
        categories.SelectedValue="documents";
        categories.SelectedValue="software";
        if(((Entry)links.Items[1]).id!=firstLink)
            throw new InvalidOperationException("Link order was lost after switching categories.");
    }

    void VerifyCloseout(){
        verifying=nonWindowedVerification=true;window.Left=100;window.Top=100;
        var presenter=presentDialog;
        try{
            var paneScroll=Find<ScrollViewer>("ContentScroll");
            rows.Children.Clear();
            CaptureShell(profile,"scroll-short.png",1100,780);
            double shortWidth=rows.ActualWidth;
            double shortHeader=Find<DockPanel>("SearchBar").ActualWidth;
            var scrollbar=(System.Windows.Controls.Primitives.ScrollBar)paneScroll.Template.FindName("PART_VerticalScrollBar",paneScroll);
            CloseoutCheck(shortWidth>0&&scrollbar.Visibility==Visibility.Hidden,"short content reserves a hidden scrollbar slot");
            rows.Children.Add(new Border{Height=1600});
            CaptureShell(profile,"scroll-long.png",1100,780);
            CloseoutCheck(scrollbar.Visibility==Visibility.Visible&&Math.Abs(shortWidth-rows.ActualWidth)<.5&&
                Math.Abs(shortHeader-Find<DockPanel>("SearchBar").ActualWidth)<.5,
                "overflow shows the scrollbar without shifting rows or search");
            var main=Find<DockPanel>("MainContent");
            double rightInset=main.Margin.Right+scrollbar.ActualWidth+rows.Margin.Right;
            CloseoutCheck(main.Margin.Right==6&&Math.Abs(rightInset-main.Margin.Left)<=3,
                "full pane keeps balanced content margins with the scrollbar near the window edge ("+main.Margin.Left+", "+rightInset+")");
            rows.Children.Clear();
            try{
                presentDialog=dialog=>{
                    var surface=(UIElement)dialog.Content;
                    Capture(surface,Path.Combine(profile,dialog.Title=="Remove item"?"notice-remove.png":"notice-warning.png"),500,250);
                    var buttons=Visuals<Button>(surface);
                    var dismiss=buttons.Single(button=>Equals(button.Content,dialog.Title=="Remove item"?"Cancel":"OK"));
                    CloseoutCheck(dismiss.IsDefault&&dismiss.IsCancel,"notice safely handles Enter and Escape by default");
                    dialog.Close();return false;
                };
                CloseoutCheck(!ShowNotice(window,"Remove item","Remove \"Bravo task\"? This change takes effect when you save.","Remove"),
                    "dismissed removal does not accept the change");
                ShowNotice(window,"Create task file","The selected file is in use. Close it in the other app and try again.");
                presentDialog=dialog=>{ClickIn((DependencyObject)dialog.Content,"Remove");dialog.Close();return true;};
                CloseoutCheck(ShowNotice(window,"Remove item","Remove \"Bravo task\"?","Remove"),"the explicit Remove action accepts the change");
            }finally{presentDialog=presenter;}
            CloseoutCheck(tray.ContextMenuStrip.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().Select(item=>item.Text)
                .SequenceEqual(new[]{"Open Dashboard","Open Tasks","Exit"}),"tray offers direct workspace commands followed by Exit");
            CloseoutCheck(tray.ContextMenuStrip.Items[2] is System.Windows.Forms.ToolStripSeparator&&
                tray.ContextMenuStrip.Items.OfType<System.Windows.Forms.ToolStripMenuItem>().All(item=>item.Padding.Top==3&&item.Padding.Bottom==3),
                "tray commands use compact vertical spacing and separate Exit");
            var trayMenu=tray.ContextMenuStrip;
            trayMenu.Size=trayMenu.PreferredSize;
            CloseoutCheck(trayMenu.Region!=null&&!trayMenu.Region.IsVisible(0,0)&&trayMenu.Region.IsVisible(trayMenu.Width/2,trayMenu.Height/2),
                "tray popup clips its outer corners and preserves its interior");
            SafeBackupVerification.Run(profile);
            VerifyDeepTaskCache();
            VerifyTaskSelectionLoads();
            var renumbered=new StackPanel();
            string gapped="1. First task\n   First description.\n\n6. Remaining task\n   Second description.";
            RenderReadableTaskText(gapped,renumbered);
            Capture(renumbered,Path.Combine(profile,"task-numbering.png"),550,250);
            CloseoutCheck(Visuals<TextBlock>(renumbered).Any(label=>label.Text=="2. Remaining task")&&
                ReadableTasks.Parse(gapped).Blocks.Any(block=>block.Number=="6"),
                "visible task numbering closes gaps and retains source identifiers");
            var descriptionRows=new StackPanel();
            const string descriptions="3. Review artwork\n   The deferred revision is ready to review.\n\n7. Review broadcast\n   Description: Confirm whether the deferred change is still needed.\n\n9. Publish the update\n   Share the approved version.";
            var parsedDescriptions=ReadableTasks.Parse(descriptions);
            var descriptionTasks=parsedDescriptions.Blocks.Where(block=>block.Kind=="task").ToArray();
            CloseoutCheck(parsedDescriptions.Original==descriptions&&descriptionTasks.Select(block=>block.Number).SequenceEqual(new[]{"3","7","9"})&&
                descriptionTasks.All(block=>!block.Deferred)&&descriptionTasks.Take(2).All(block=>block.Summary.Contains("deferred")),
                "plain and labeled descriptions retain deferred wording without classifying the task");
            RenderReadableTaskText(descriptions,descriptionRows);
            var descriptionLabels=Visuals<TextBlock>(descriptionRows).Select(label=>label.Text).ToArray();
            CloseoutCheck(descriptionLabels.Where(text=>text=="1. Review artwork"||text=="2. Review broadcast"||text=="3. Publish the update")
                .SequenceEqual(new[]{"1. Review artwork","2. Review broadcast","3. Publish the update"})&&
                descriptionLabels.Contains("The deferred revision is ready to review.")&&
                descriptionLabels.Contains("Confirm whether the deferred change is still needed.")&&
                !Visuals<Expander>(descriptionRows).Any(expander=>Equals(expander.Header,"Deferred work · Do not start")),
                "description wording leaves tasks visible and numbered in source order");
            var website=new Entry{targets=new List<LinkTarget>{new LinkTarget{kind="web",url="https://sample.invalid/path?private=value"}}};
            var origin=WebsiteIcons.Origin(website);
            CloseoutCheck(origin.AbsoluteUri=="https://sample.invalid/","website icon lookup uses the origin without the link path or query");
            byte[] fixtureIcon;
            using(var stream=new MemoryStream())
            {
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder();
                png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(System.Windows.Media.Imaging.BitmapSource.Create(
                    1,1,96,96,PixelFormats.Bgra32,null,new byte[]{20,130,220,255},4)));
                png.Save(stream);
                fixtureIcon=stream.ToArray();
            }
            var requests=new List<string>();
            var favicon=WebsiteIcons.Fetch(profile,origin,uri=>{
                requests.Add(uri.AbsoluteUri);
                if(uri==origin)return System.Text.Encoding.UTF8.GetBytes("<link rel='icon' href='https://other.invalid/icon.png'><link href='/actual.png' rel='shortcut icon'>");
                return uri.AbsolutePath=="/actual.png"?fixtureIcon:null;
            });
            CloseoutCheck(favicon!=null&&WebsiteIcons.Cached(profile,origin)!=null&&requests.SequenceEqual(new[]{
                "https://sample.invalid/favicon.ico","https://sample.invalid/","https://sample.invalid/actual.png"}),
                "favicons fall back to the site icon, skip other origins and persist in the cache");
            Select("everyday");
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                Capture(surface,Path.Combine(profile,"website-icon-action.png"),1164,811);
                var list=Visuals<ListBox>(surface).Single(item=>System.Windows.Automation.AutomationProperties.GetName(item)=="Dashboard entries");
                var entry=(Entry)list.SelectedItem;
                entry.iconGlyph="folder";
                Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Website address").Text=origin.AbsoluteUri;
                ClickIn(surface,"Use website icon");
                Capture(surface,Path.Combine(profile,"website-icon-action.png"),1000,600);
                var status=Visuals<TextBlock>(surface).Single(label=>label.Text=="Website icon ready. Choose Save to apply it.");
                var address=Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Website address");
                CloseoutCheck(status.TranslatePoint(new Point(0,0),surface).Y<list.TranslatePoint(new Point(0,0),surface).Y&&
                    !Visuals<ScrollViewer>(surface).Any(scroll=>scroll.IsAncestorOf(status))&&
                    Visuals<ScrollViewer>(surface).Any(scroll=>scroll.IsAncestorOf(address)),
                    "icon status stays above the list and outside the independently scrolling fields");
                CloseoutCheck(entry.iconGlyph==null&&Visuals<TextBlock>(surface).Any(label=>label.Text=="Website icon ready. Choose Save to apply it."),
                    "Use website icon previews a found icon and changes the draft selection");
                dialog.Close();return false;
            };
            EditCatalog();
            Select("software");
            if(Environment.GetEnvironmentVariable("LOCAL_DASHBOARD_VERIFY_LIVE_ICONS")=="1")
            {
                foreach(var host in new[]{"pepsi.com","dairyqueen.com"})
                {
                    Select("everyday");
                    string expected="https://"+host+"/";
                    var address=new Uri(expected);
                    CloseoutCheck(WebsiteIcons.Cached(profile,address)==null,host+" starts without a cached icon");
                    presentDialog=dialog=>{
                        var surface=(UIElement)dialog.Content;
                        Capture(surface,Path.Combine(profile,"auto-icon-editor.png"),1164,811);
                        Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Website address").Text=expected;
                        ClickIn(surface,"Save");
                        dialog.Close();
                        return true;
                    };
                    EditCatalog();
                    var frame=new System.Windows.Threading.DispatcherFrame();
                    var deadline=DateTime.UtcNow.AddSeconds(25);
                    var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(35)};
                    timer.Tick+=(sender,args)=>{
                        if(DateTime.UtcNow>=deadline||!pendingWebsiteIcons.Contains(address))frame.Continue=false;
                    };
                    timer.Start();
                    try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
                    var saved=entries.Single(item=>item.targets.Any(target=>target.url==expected));
                    var artwork=Visuals<Image>(rows).Single(image=>Equals(image.Tag,"entry-artwork"));
                    CloseoutCheck(WebsiteIcons.Origin(saved)!=null&&WebsiteIcons.Cached(profile,address)!=null&&
                        artwork.Source is System.Windows.Media.Imaging.BitmapSource,
                        host+" downloads, caches and displays its icon after Save without an icon button click");
                    CaptureShell(profile,"auto-icon-"+host+".png",1100,780);
                }
                foreach(var host in new[]{"robertshare.com","github.com"})
                {
                    Select("everyday");
                    string expected="https://"+host+"/";
                    presentDialog=dialog=>{
                        var surface=(UIElement)dialog.Content;
                        Capture(surface,Path.Combine(profile,"live-icon-"+host+".png"),1164,811);
                        var list=Visuals<ListBox>(surface).Single(item=>System.Windows.Automation.AutomationProperties.GetName(item)=="Dashboard entries");
                        ((Entry)list.SelectedItem).iconGlyph="folder";
                        Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Website address").Text=expected;
                        ClickIn(surface,"Use website icon");
                        var frame=new System.Windows.Threading.DispatcherFrame();
                        var deadline=DateTime.UtcNow.AddSeconds(25);
                        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(35)};
                        timer.Tick+=(sender,args)=>{
                            if(DateTime.UtcNow>=deadline||!Visuals<TextBlock>(surface).Any(label=>label.Text=="Looking for the website icon…"))frame.Continue=false;
                        };
                        timer.Start();
                        try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
                        Capture(surface,Path.Combine(profile,"live-icon-"+host+".png"),1164,811);
                        CloseoutCheck(Visuals<TextBlock>(surface).Any(label=>label.Text=="Website icon ready. Choose Save to apply it."),
                            host+" previews a live website icon from a cold cache");
                        ClickIn(surface,"Save");
                        dialog.Close();
                        return true;
                    };
                    EditCatalog();
                    var saved=entries.Single(item=>item.targets.Any(target=>target.url==expected));
                    CloseoutCheck(saved.iconPath==WebsiteIcons.CachePath(profile,new Uri(expected))&&EntryIcon(saved,profile) is System.Windows.Media.Imaging.BitmapSource,
                        host+" saves and reloads its downloaded icon");
                }
                Select("software");
            }
            VerifyMixedDestinationIcon(origin);
            website.iconGlyph="folder";
            CloseoutCheck(WebsiteIcons.Origin(website)==null,"a chosen built-in icon takes precedence over the favicon");
            website.iconGlyph=null;website.iconPath="custom.png";
            CloseoutCheck(WebsiteIcons.Origin(website)==null,"a chosen local image takes precedence over the favicon");
            CloseoutCheck(WebsiteIcons.Fetch(profile,new Uri("https://missing.invalid/"),uri=>null)==null,"unavailable website icons retain the fallback");

            window.Width=1180;window.Height=850;
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                var root=(DependencyObject)surface;
                for(int index=0;index<4;index++)
                {
                    Capture(surface,Path.Combine(profile,"editor-roomier.png"),1164,811);
                    ClickIn(root,"Add category");
                    Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Category name").Text="Sample category "+(index+1);
                }
                Capture(surface,Path.Combine(profile,"editor-roomier.png"),1164,811);
                var list=Visuals<ListBox>(root).Single(item=>System.Windows.Automation.AutomationProperties.GetName(item)=="Section");
                var viewport=Visuals<ScrollViewer>(list).Single();
                CloseoutCheck(dialog.Width==1180&&dialog.Height==850&&list.Items.Count>=7&&viewport.ScrollableHeight<1,
                    "editor matches the main window and fits at least seven categories at the tested size");
                for(int index=0;index<5;index++)ClickIn(root,"Add category");
                Capture(surface,Path.Combine(profile,"editor-roomier-scroll.png"),1164,811);
                viewport.ScrollToBottom();viewport.UpdateLayout();
                var name=Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Category name");
                var save=Visuals<Button>(root).Single(button=>Equals(button.Content,"Save"));
                CloseoutCheck(viewport.VerticalOffset>0&&name.TranslatePoint(new Point(0,name.ActualHeight),surface).Y<
                    save.TranslatePoint(new Point(0,0),surface).Y,"overflow scrolls only the categories and keeps the controls above Save");
                dialog.Close();return false;
            };EditCatalog();
            CloseoutCheck(Equals(((Image)Find<MenuItem>("BackupMenu").Icon).Tag,"config-backup")&&Equals(((Image)Find<MenuItem>("RestoreMenu").Icon).Tag,"config-restore"),"File menu uses the approved backup and restore icon pair");
            string preview=SetupTaskPreview("# Project tasks\nUpdated: November 9\nKeep Dashboard first until accepted.\n## Current projects\n1. Test the preview\n   Use a sample.");
            CloseoutCheck(preview=="1. Test the preview","setup preview shows task names without file headings or agent instructions");
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;
                if(dialog.Title=="Choose an icon")
                {
                    Capture(dialog.Content as UIElement,Path.Combine(profile,"picker-common.png"),504,511);
                    var viewport=Visuals<ScrollViewer>(root).Single(view=>Equals(view.Tag,"icon-picker-grid"));
                    var footer=Visuals<Border>(root).Single(area=>Equals(area.Tag,"icon-picker-footer"));
                    CloseoutCheck(viewport.ScrollableHeight<1,"Common fits at the default picker size");
                    var commonGrid=Visuals<System.Windows.Controls.Primitives.UniformGrid>(root).Single();
                    CloseoutCheck(commonGrid.Columns==5&&commonGrid.Children.Count==15,"Common shows three full rows of five");
                    var first=(Button)commonGrid.Children[0];
                    var last=(Button)commonGrid.Children[4];
                    CloseoutCheck(Math.Abs(first.TranslatePoint(new Point(0,0),commonGrid).X-2)<1&&
                        Math.Abs(commonGrid.ActualWidth-last.TranslatePoint(new Point(last.ActualWidth,0),commonGrid).X-2)<1,
                        "icon tiles fill the row evenly");
                    ClickIn(root,"All icons (103)");
                    Capture(dialog.Content as UIElement,Path.Combine(profile,"picker-all-compact.png"),504,420);
                    viewport.ScrollToBottom();
                    viewport.UpdateLayout();
                    CloseoutCheck(viewport.ScrollableHeight>0&&viewport.VerticalOffset>0,"All icons scrolls at a smaller picker size");
                    double gridBottom=viewport.TranslatePoint(new Point(0,viewport.ActualHeight),(UIElement)root).Y;
                    double footerTop=footer.TranslatePoint(new Point(0,0),(UIElement)root).Y;
                    CloseoutCheck(footerTop-gridBottom>=12,"the footer stays separate from the scrolling grid");
                    CloseoutCheck(pickerArt.Value.Count==103&&pickerArt.Value.Keys.All(HasDashboardGlyph),"full shared icon collection is bundled");
                    var searchIcons=Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Search built-in icons");
                    searchIcons.Text="website";
                    CloseoutCheck(Visuals<Button>(root).Any(button=>System.Windows.Automation.AutomationProperties.GetName(button)=="Link"),"icon search reaches beyond Common");
                    ClickIn(root,"Common");
                    Visuals<Button>(root).Single(button=>System.Windows.Automation.AutomationProperties.GetName(button)=="Websites").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    dialog.Close();return true;
                }
                Capture(dialog.Content as UIElement,Path.Combine(profile,"followup-editor.png"),904,681);
                ClickIn(root,"Add category");
                Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Category name").Text="Example category";
                ClickIn(root,"Change category icon");
                ClickIn(root,"Save");dialog.Close();return true;
            };EditCatalog();
            var category=sidebarStore.Read().Layout.items.Single(item=>item.label=="Example category");
            CloseoutCheck(SidebarStore.Custom(category.id)&&category.icon=="globe","category name and bundled icon survive unified Save");
            CloseoutCheck(Find<StackPanel>("Navigation").Children.OfType<Button>().Any(button=>Equals(button.Tag,category.id)),"custom category is visible in the sidebar");
            CloseoutCheck(section==category.id,"new category remains selected");
            Select("all");
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;
                Capture(dialog.Content as UIElement,Path.Combine(profile,"seed-choose-category.png"),904,681);
                var categories=Visuals<ListBox>(root).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Section");
                CloseoutCheck(categories.SelectedItem==null&&!categories.Items.OfType<SidebarItem>().Any(item=>item.id=="all"),"All links is outside editing and no category is chosen for the user");
                CloseoutCheck(Visuals<Button>(root).Single(button=>Equals(button.Content,"Add link")).Visibility==Visibility.Collapsed,"adding a link requires a real category");
                dialog.Close();return false;
            };EditCatalog();
            Select("documents");
            var workspaces=Find<StackPanel>("WorkspaceNavigation");
            ((Button)workspaces.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CloseoutCheck(section=="work"&&Find<StackPanel>("Navigation").Visibility==Visibility.Collapsed&&Find<StackPanel>("SidebarFooter").IsEnabled,
                "Tasks hides Dashboard controls while Settings stays available");
            CloseoutCheck(!workspaces.Children.OfType<Button>().Any(System.Windows.Controls.Primitives.Selector.GetIsSelected)&&
                TaskNavigationPanel().Children.OfType<Button>().Count(System.Windows.Controls.Primitives.Selector.GetIsSelected)==1,
                "only the current list carries the Tasks sidebar selection");
            CaptureShell(profile,"workspace-tasks.png",1000,650);
            ((Button)workspaces.Children[0]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CloseoutCheck(section=="documents"&&Find<StackPanel>("Navigation").Visibility==Visibility.Visible&&Find<StackPanel>("Navigation").IsEnabled,"Dashboard returns to the previous category");
            Select("software");
            string catalog=Path.Combine(profile,"catalog.json"),original=DashboardData.Hash(File.ReadAllBytes(catalog));
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;
                var list=Visuals<ListBox>(root).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Dashboard entries");
                CloseoutCheck(list.SelectedItem!=null,"editor opens with an actual current item selected");
                ClickIn(root,"Add link");
                CloseoutCheck(list.SelectedItem is Entry&&((Entry)list.SelectedItem).name=="New link"&&
                    list.SelectedIndex==list.Items.Count-1,"Add selects the new link at the bottom of the category");
                var surface=(UIElement)dialog.Content;
                Capture(surface,Path.Combine(profile,"editor-links-roomier.png"),1164,811);
                var nameField=Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Name");
                for(int index=0;index<7;index++)
                {
                    nameField.Text="Draft "+index;
                    list.SelectedIndex=list.Items.Count/2;
                    ClickIn(root,"Add link");
                }
                Capture(surface,Path.Combine(profile,"editor-links-roomier.png"),1164,811);
                var viewport=Visuals<ScrollViewer>(list).Single();
                CloseoutCheck(list.SelectedIndex==list.Items.Count-1,"adding from the middle still appends the new link");
                var addedRow=(ListBoxItem)list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem);
                var addedTop=addedRow.TranslatePoint(new Point(0,0),viewport).Y;
                CloseoutCheck(list.ActualHeight>250&&viewport.VerticalOffset>0&&addedTop>=0&&
                    addedTop+addedRow.ActualHeight<=viewport.ActualHeight&&nameField.ActualHeight>0,
                    "links list shows more rows and scrolls the newly selected link into view");
                Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-editor.png"),844,581);
                CloseoutCheck(viewport.ViewportHeight>=96,"small Edit Dashboard keeps room for two link rows");
                dialog.Close();return false;
            };EditCatalog();
            CloseoutCheck(DashboardData.Hash(File.ReadAllBytes(catalog))==original,"Cancel leaves catalog byte exact");
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-editor.png"),844,581);ClickIn(root,"Add link");
                Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Name").Text="Closeout example";
                var type=Visuals<ComboBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Opens");
                CloseoutCheck(type.IsEnabled,"new link type can be changed");type.SelectedIndex=2;
                var selected=(Entry)Visuals<ListBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Dashboard entries").SelectedItem;
                CloseoutCheck(selected.targets.Single().kind=="web","Website selection updates the draft link type");
                Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Website address").Text="https://example.com/closeout";
                ClickIn(root,"Save");dialog.Close();return true;
            };EditCatalog();
            CloseoutCheck(entries.Any(e=>e.name=="Closeout example"&&e.targets.Single().url=="https://example.com/closeout"),"editor Save creates and reloads a website link");
            presentDialog=dialog=>{
                var root=(UIElement)dialog.Content;
                Capture(root,Path.Combine(profile,"editor-folder-link.png"),1000,750);
                string before=DashboardData.Hash(File.ReadAllBytes(catalog));
                ClickIn(root,"Add link");
                ClickIn(root,"Save");
                CloseoutCheck(Visuals<TextBlock>(root).Any(text=>text.Text=="Choose a folder for this link.")&&
                    DashboardData.Hash(File.ReadAllBytes(catalog))==before,
                    "saving a blank folder link explains what is needed and preserves the draft");
                Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Name").Text="Closeout folder";
                Visuals<TextBox>(root).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Folder").Text=profile;
                ClickIn(root,"Save");dialog.Close();return true;
            };EditCatalog();
            CloseoutCheck(entries.Any(entry=>entry.name=="Closeout folder"&&entry.targets.Single().path==profile),
                "the same draft saves and reloads after supplying a folder");
            var initialOrder=sidebarStore.Read().Layout;
            CloseoutCheck(OrderedDashboardEntries(entries.Where(entry=>entry.section=="software"),initialOrder,"software").Last().name=="Closeout folder",
                "new link remains last after saving and reloading");
            var firstCategory=initialOrder.items.First(item=>item.id!="all").id;
            var firstLink=(string)((Border)rows.Children[0]).Tag;
            CloseoutCheck(!Visuals<Button>(Find<StackPanel>("Navigation")).Any(button=>Equals(button.Content,"Move")),"Dashboard has a single Edit entry point");
            presentDialog=dialog=>{
                VerifyEditorReorder(dialog,firstCategory,firstLink);
                Capture((UIElement)dialog.Content,Path.Combine(profile,"editor-reorder.png"),904,681);
                dialog.Close();return false;
            };EditCatalog();
            CloseoutCheck(sidebarStore.Read().Layout.items[0].id==firstCategory&&Equals(((Border)rows.Children[0]).Tag,firstLink),"Cancel preserves category and link order");
            presentDialog=dialog=>{
                VerifyEditorReorder(dialog,firstCategory,firstLink);
                ClickIn((DependencyObject)dialog.Content,"Save");
                dialog.Close();return true;
            };EditCatalog();
            var savedOrder=sidebarStore.Read().Layout;
            CloseoutCheck(savedOrder.items[1].id==firstCategory&&savedOrder.orders["software"][1]==firstLink&&
                Equals(((Border)rows.Children[1]).Tag,firstLink),"Save applies both draft orders to Dashboard");
            var changes=new CatalogDraft(catalog);
            var newLink=changes.Entries.Single(entry=>entry.name=="Closeout example");
            var destination=newLink.targets.Single();
            destination.kind="document";
            destination.url=null;
            destination.path=Path.Combine(profile,"Examples","Example document.txt");
            changes.Save();
            CloseoutCheck(new CatalogDraft(catalog).Entries.Single(entry=>entry.id==newLink.id).targets.Single().kind=="document","existing link type changes without changing its ID");
            var categoryLink=new CatalogDraft(catalog);
            var added=categoryLink.Add(category.id,"web");
            added.targets.Single().url="https://example.com/category";
            categoryLink.Save();
            Reload();
            CloseoutCheck(entries.Any(entry=>entry.id==added.id&&entry.section==category.id),"links save and reload under a custom category");
            var layoutBefore=sidebarStore.Read();
            var removeLayout=SidebarStore.Copy(layoutBefore.Layout);
            removeLayout.items.RemoveAll(item=>item.id==category.id);
            removeLayout.customized=true;
            var moveDraft=new CatalogDraft(catalog);
            moveDraft.Entries.Single(item=>item.id==added.id).section="documents";
            string catalogBefore=DashboardData.Hash(File.ReadAllBytes(catalog));
            bool interrupted=false;
            try{DashboardData.SaveDashboardEdit(profile,moveDraft,sidebarStore,layoutBefore,removeLayout,index=>{throw new IOException("Test interruption");});}
            catch(IOException error){interrupted=error.Message=="Test interruption";}
            CloseoutCheck(interrupted&&DashboardData.Hash(File.ReadAllBytes(catalog))==catalogBefore&&sidebarStore.Read().Token==layoutBefore.Token,
                "interrupted category removal rolls back link reassignment");
            DashboardData.SaveDashboardEdit(profile,moveDraft,sidebarStore,layoutBefore,removeLayout);
            Reload();
            CloseoutCheck(!layout.items.Any(item=>item.id==category.id)&&entries.Single(item=>item.id==added.id).section=="documents",
                "category removal retains reassigned link identity");
            foreach(bool personal in new[]{false,true})
            {
                var choice=WorkSources.Read(profile,personal);
                string hash=DashboardData.Hash(File.ReadAllBytes(choice.path));
                string token=WorkSources.Token(profile,personal);
                WorkSources.Save(profile,null,token,personal);
                bool disconnected=false;
                try{WorkSources.Read(profile,personal);}catch(TaskSourceNotSelectedException){disconnected=true;}
                CloseoutCheck(disconnected&&DashboardData.Hash(File.ReadAllBytes(choice.path))==hash,"disconnect keeps original task file");
                WorkSources.Save(profile,choice,WorkSources.Token(profile,personal),personal);
            }
            var rawRows=AppData.Json.Deserialize<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string,object>>>(File.ReadAllText(catalog));
            var sample=rawRows.Single(row=>Equals(row["id"],"example-project"));
            var retainedTarget=((System.Collections.IList)sample["targets"]).Cast<System.Collections.Generic.Dictionary<string,object>>().Single();
            retainedTarget["futureField"]="keep";
            sample["targets"]=new[]{retainedTarget,new System.Collections.Generic.Dictionary<string,object>{{"id","second"},{"kind","web"},{"label","Website"},{"url","https://example.com"}}};
            DashboardData.Atomic(catalog,System.Text.Encoding.UTF8.GetBytes(AppData.Json.Serialize(rawRows)));
            var prune=new CatalogDraft(catalog);
            prune.Entries.Single(item=>item.id=="example-project").targets.RemoveAll(target=>target.id=="second");
            prune.Save();
            rawRows=AppData.Json.Deserialize<System.Collections.Generic.List<System.Collections.Generic.Dictionary<string,object>>>(File.ReadAllText(catalog));
            retainedTarget=((System.Collections.IList)rawRows.Single(row=>Equals(row["id"],"example-project"))["targets"]).Cast<System.Collections.Generic.Dictionary<string,object>>().Single();
            CloseoutCheck(Equals(retainedTarget["futureField"],"keep")&&((string)retainedTarget["path"]).StartsWith("@app/"),"removing one destination preserves another destination and its metadata");
            var taskSource=WorkSources.Read(profile,true);
            const string firstTask="1. Delete this\r\n   Task notes\r\n\r\n";
            const string secondTask="2. Keep this\r\n   Keep these notes\r\n";
            File.WriteAllText(taskSource.path,firstTask+secondTask);
            var snapshot=PersonalTextStore.Open(profile);
            var deleteBlock=ReadableTasks.Parse(snapshot.Text).Blocks.First(block=>block.Kind=="task");
            PersonalTextStore.Save(profile,snapshot,snapshot.Text.Remove(deleteBlock.Start,deleteBlock.Length));
            CloseoutCheck(File.ReadAllText(taskSource.path)==secondTask,"deleting a Personal task preserves the neighboring task text");
            var checklist=new ChecklistStore(ChecklistLocation.PathFor(profile),entries);
            var currentChecklist=checklist.Read();
            var itemAdded=checklist.Upsert(new WorkPatch{expectedRevision=currentChecklist.revision,id="delete-test",title="Delete fixture"});
            var completed=checklist.Complete(itemAdded.revision,"delete-test",true);
            var deleted=checklist.Delete(completed.revision,"delete-test");
            CloseoutCheck(deleted.items.All(item=>item.id!="delete-test")&&deleted.revision==completed.revision+1,"checklist can delete a completed item");
            window.Width=1000;window.Height=700;window.Left=100;window.Top=120;
            SetCompact(true);window.Left=320;window.Top=160;window.Width=600;window.Height=610;SetCompact(false);
            CloseoutCheck(window.Left==100&&window.Top==120&&window.Width==1000&&window.Height==700,"Expand restores independent full placement");
            SetCompact(true);CloseoutCheck(window.Left==320&&window.Top==160&&window.Width==600&&window.Height==610,"Compact restores independent placement");
            nonWindowedVerification=false;SaveWindowBounds();nonWindowedVerification=true;
            fullBounds=new Rect();compactBounds=null;LoadFullBounds();LoadCompactBounds();
            CloseoutCheck(fullBounds.Left==100&&fullBounds.Top==120&&compactBounds.Value.Left==320&&compactBounds.Value.Top==160,"both placements reload from their saved files");
            SetCompact(false);window.Left=100;window.Top=120;window.Width=1000;window.Height=700;
            presentDialog=dialog=>{var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-window-settings.png"),764,661);ClickIn(root,"Window");Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-window-settings.png"),764,661);ClickIn(root,"Reset window sizes");dialog.Close();return false;};Settings();
            CloseoutCheck(window.Left==100&&window.Top==120&&window.Width==1000&&window.Height==700,"Cancel leaves window reset unapplied");
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                Capture(surface,Path.Combine(profile,"appearance-checkbox.png"),764,661);
                var checkbox=Visuals<CheckBox>(surface).Single(box=>Equals(box.Content,"Show All links in the sidebar"));
                var scroll=Visuals<ScrollViewer>(surface).First(viewer=>viewer.IsAncestorOf(checkbox));
                var viewport=(ScrollContentPresenter)scroll.Template.FindName("PART_ScrollContentPresenter",scroll);
                double top=checkbox.TranslatePoint(new Point(0,0),viewport).Y;
                CloseoutCheck(top>=0&&top+checkbox.ActualHeight<=viewport.ActualHeight+.5&&scroll.ScrollableHeight<=.5,
                    "default Appearance fits without scrolling and Show All links is fully visible");
                var customColor=Visuals<Expander>(surface).Single(expander=>Equals(expander.Header,"Custom color"));
                customColor.IsExpanded=true;
                surface.UpdateLayout();
                dialog.Dispatcher.Invoke(new Action(()=>{}),System.Windows.Threading.DispatcherPriority.Background);
                Capture(surface,Path.Combine(profile,"appearance-custom-color.png"),764,661);
                var sliders=Visuals<Slider>(customColor).ToArray();
                Console.WriteLine("Scroll "+scroll.VerticalOffset+"/"+scroll.ScrollableHeight+" viewport "+viewport.ActualHeight+" sliders "+String.Join(",",sliders.Select(slider=>slider.TranslatePoint(new Point(0,0),viewport).Y)));
                CloseoutCheck(scroll.VerticalOffset>0&&sliders.Length==3&&sliders.All(slider=>{
                    double y=slider.TranslatePoint(new Point(0,0),viewport).Y;
                    return y>=-.5&&y+slider.ActualHeight<=viewport.ActualHeight+.5;
                }),"expanding Custom color scrolls all three sliders into view");
                dialog.Close();return false;
            };Settings();
            presentDialog=dialog=>{var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-window-settings.png"),764,661);ClickIn(root,"Window");Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-window-settings.png"),764,661);ClickIn(root,"Reset window sizes");ClickIn(root,"Save");dialog.Close();return true;};Settings();
            CloseoutCheck(window.Width>=760&&window.Width<=1280&&window.Height>=540&&window.Height<=860&&compactBounds.HasValue,"Save applies usable full and compact defaults");
            var pin=Find<StackPanel>("FullWindowActions").Children.OfType<OnTopButton>().Single();
            var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(pin);
            var toggle=(System.Windows.Automation.Provider.IToggleProvider)peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.Toggle);
            var currentCategory=Find<StackPanel>("Navigation").Children.OfType<Button>().Single(button=>Equals(button.Tag,section));
            CloseoutCheck(!System.Windows.Controls.Primitives.Selector.GetIsSelected((Button)Find<StackPanel>("WorkspaceNavigation").Children[0])&&
                System.Windows.Controls.Primitives.Selector.GetIsSelected(currentCategory),"only the current category carries the Dashboard sidebar selection");
            var pinImage=(Image)pin.Content;
            CaptureShell(profile,"workspace-dashboard.png",1000,650);
            CloseoutCheck(toggle.ToggleState==System.Windows.Automation.ToggleState.Off&&Equals(pinImage.Tag,"pin"),"On top starts with outline pin and accessible off state");
            toggle.Toggle();
            SetCompact(true);
            CaptureShell(profile,"workspace-compact.png",580,690);
            var menuPin=Find<MenuItem>("TopMenu");
            menuPin.ApplyTemplate();
            var menuIcon=(ContentPresenter)menuPin.Template.FindName("IconContent",menuPin);
            var menuCheck=(TextBlock)menuPin.Template.FindName("CheckMark",menuPin);
            CloseoutCheck(menuPin.IsChecked&&Equals(((Image)menuPin.Icon).Tag,"pin-active")&&
                menuIcon.Visibility==Visibility.Visible&&menuCheck.Visibility==Visibility.Collapsed,"View menu shows the filled pin while checked");
            CloseoutCheck(window.Topmost&&toggle.ToggleState==System.Windows.Automation.ToggleState.On&&Equals(pinImage.Tag,"pin-active"),"filled pin and accessible on state persist in compact view");
            toggle.Toggle();
            SetCompact(false);
            CloseoutCheck(!window.Topmost&&toggle.ToggleState==System.Windows.Automation.ToggleState.Off&&Equals(pinImage.Tag,"pin"),"On top returns to outline pin when turned off");
            presentDialog=dialog=>{
                var surface=(UIElement)dialog.Content;
                Capture(surface,Path.Combine(profile,"help-selection.png"),884,581);
                var topics=Visuals<ListBox>(surface).Single();
                var topic=(ListBoxItem)topics.ItemContainerGenerator.ContainerFromItem(topics.SelectedItem);
                var marker=topic==null?null:topic.Template.FindName("ItemFocus",topic) as Border;
                CloseoutCheck(marker!=null&&marker.Visibility==Visibility.Visible&&marker.Child is System.Windows.Shapes.Path,
                    "Help uses the wraparound selection accent");
                var helpTopics=topics.Items.Cast<DashboardHelp.Topic>().ToArray();
                foreach(var item in helpTopics)
                {
                    topics.SelectedItem=item;
                    if(!Visuals<TextBlock>(surface).Any(text=>text.Text==item.Title+"\n\n"+item.Body))
                        throw new Exception("Help could not display "+item.Title);
                }
                CloseoutCheck(helpTopics.Length==DashboardHelp.Topics.Length,"all Help articles open and display their complete text");
                var filter=Visuals<TextBox>(surface).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)=="Search help");
                filter.Text="agent";
                CloseoutCheck(topics.Items.Cast<DashboardHelp.Topic>().Select(item=>item.Title).SequenceEqual(new[]{"Get help from an agent"}),
                    "Help search for agent finds the optional workflow without retired reports");
                filter.Text="restore from a backup";
                CloseoutCheck(topics.Items.Cast<DashboardHelp.Topic>().Any(item=>item.Title=="Configuration backups")&&
                    topics.Items.Cast<DashboardHelp.Topic>().Any(item=>item.Title=="Optional setup"),"Help search finds restore in setup and configuration backups");
                dialog.Close();
                return false;
            };
            ShowHelp();
            var previousResource=shellBrand.IconResource;ApplyAccent(System.Drawing.Color.FromArgb(245,204,137));
            CloseoutCheck(shellBrand.IconResource==previousResource&&DashboardData.Hash(File.ReadAllBytes(previousResource.Substring(0,previousResource.Length-2)))==DashboardData.Hash(DashboardBrand.IconBytes(DashboardAccent.Selected)),"accent updates the stable relaunch icon");
            ApplyAccent(SavedAccent());
            Console.WriteLine("Closeout checks finished. Offscreen UI and data only; visible taskbar and native placement still require review.");
        }finally{presentDialog=presenter;}
    }
}
