using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    static void CloseoutCheck(bool result,string name){if(!result)throw new Exception(name);Console.WriteLine("PASS "+name);}
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
        foreach(string role in new[]{"Agent","Personal"})
        {
            string file=Path.Combine(profile,role+" newly selected.txt");
            File.WriteAllText(file,role+" selected sample");
            Visuals<TextBox>(settings).Single(field=>System.Windows.Automation.AutomationProperties.GetName(field)==role+" tasks list location").Text=file;
        }
        save();
        var frame=new System.Windows.Threading.DispatcherFrame();
        var deadline=DateTime.UtcNow.AddSeconds(9);
        var timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e)=>{
            if(DateTime.UtcNow>=deadline||(workReadStatus.StartsWith("Read succeeded")&&personalReadStatus.StartsWith("Read succeeded")))frame.Continue=false;
        };
        timer.Start();
        try{System.Windows.Threading.Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        foreach(bool personal in new[]{false,true})
        {
            var source=WorkSources.Read(profile,personal);
            var cache=WorkSources.Cached(profile,source);
            CloseoutCheck(cache!=null&&cache.text==(personal?"Personal":"Agent")+" selected sample","saving the task location loads the selected file without Refresh");
        }
        assistantWorkView=true;personalTasksView=false;agentUpdatesView=false;Select("work");
        var taskRow=Find<StackPanel>("WorkNavigation").Children.OfType<WrapPanel>().First();
        CloseoutCheck(System.Windows.Automation.AutomationProperties.GetName((Button)taskRow.Children[3])=="Refresh","Refresh follows the three task selectors");
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
            SafeBackupVerification.Run(profile);
            VerifyDeepTaskCache();
            VerifyTaskSelectionLoads();
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
            CloseoutCheck(!System.Windows.Controls.Primitives.Selector.GetIsSelected((Button)workspaces.Children[0])&&
                System.Windows.Controls.Primitives.Selector.GetIsSelected((Button)workspaces.Children[1]),"Tasks is the only selected sidebar workspace");
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
                CloseoutCheck(list.SelectedItem is Entry&&((Entry)list.SelectedItem).name=="New link","Add selects an editable new link");
                Capture(dialog.Content as UIElement,Path.Combine(profile,"closeout-editor.png"),844,581);
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
            var initialOrder=sidebarStore.Read().Layout;
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
                dialog.Close();
                return false;
            };
            ShowHelp();
            var stable=shellBrand.IconResource;ApplyAccent(System.Drawing.Color.FromArgb(245,204,137));
            CloseoutCheck(shellBrand.IconResource==stable&&DashboardData.Hash(File.ReadAllBytes(stable.Substring(0,stable.Length-2)))==DashboardData.Hash(DashboardBrand.IconBytes(DashboardAccent.Selected)),"accent updates stable shell icon bytes without changing resource path");
            ApplyAccent(SavedAccent());
            Console.WriteLine("Closeout checks finished. Offscreen UI and data only; visible taskbar and native placement still require review.");
        }finally{presentDialog=presenter;}
    }
}
