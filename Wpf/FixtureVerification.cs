using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

internal sealed partial class WpfDashboard
{
    bool verifying;
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern IntPtr SendMessage(IntPtr handle,uint message,IntPtr wParam,IntPtr lParam);
    void VerifyVisibleIconSave(){
        VerifyDialog(Settings,"Local Dashboard settings","visible-settings-save.png",dialog=>{
            Visuals<TextBox>(dialog).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color").Text="#FF6600";
            WaitForAppearancePreview();
            if(DashboardAccent.Format(DashboardAccent.Selected)!="#FF6600")throw new Exception("Orange Preview failed.");
            Click(dialog,"Save");
        });
        preferenceStore.Reload();
        if(preferenceStore.Value.accentColor!="#FF6600")throw new Exception("Visible Settings Save failed.");
        VerifyNativeIcon();
        Console.WriteLine("Visible Settings saved custom #FF6600; inspect the actual taskbar before exiting.");
    }
    void VerifyNativeIcon(){
        var expected=DashboardAccent.Selected;var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if(ShellTaskbarBrand.ProcessIdentity()!=shellBrand.AppId)throw new Exception("Process and window taskbar identities differ.");
        if(shellBrand.Read(hwnd,2)!=shellBrand.RelaunchCommand)throw new Exception("Shell relaunch command differs from the selected profile command.");
        if(shellBrand.Read(hwnd,5)!=shellBrand.AppId||shellBrand.Read(hwnd,3)!=shellBrand.IconResource)throw new Exception("Shell taskbar identity or icon resource differs.");
        var iconPath=shellBrand.IconResource.Substring(0,shellBrand.IconResource.Length-2);if(!File.Exists(iconPath)||Path.GetFileName(iconPath)!="dashboard-taskbar.ico"||DashboardData.Hash(File.ReadAllBytes(iconPath))!=DashboardData.Hash(DashboardBrand.IconBytes(expected)))throw new Exception("Stable shell icon resource is missing or has wrong colour bytes.");
        Console.WriteLine("Shell taskbar properties read back: "+shellBrand.AppId+" / "+shellBrand.IconResource+". Actual taskbar appearance still requires visual verification.");
        foreach(int kind in new[]{0,1}){
            var handle=SendMessage(hwnd,0x007F,new IntPtr(kind),IntPtr.Zero);if(handle==IntPtr.Zero)throw new Exception("Missing native window icon kind "+kind);
            using(var icon=System.Drawing.Icon.FromHandle(handle))using(var image=icon.ToBitmap()){
                var counts=new Dictionary<int,int>();for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++){var pixel=image.GetPixel(x,y);if(pixel.A<128||pixel.R+pixel.G+pixel.B<260)continue;int key=pixel.ToArgb();counts[key]=counts.ContainsKey(key)?counts[key]+1:1;}
                var actual=System.Drawing.Color.FromArgb(counts.OrderByDescending(p=>p.Value).First().Key);
                if(Math.Abs(actual.R-expected.R)>8||Math.Abs(actual.G-expected.G)>8||Math.Abs(actual.B-expected.B)>8)throw new Exception("Native icon does not match accent: "+actual+" vs "+expected);
                Console.WriteLine("Native "+(kind==0?"small":"large")+" window icon: "+DashboardAccent.Format(actual));
            }
        }
    }
    void VerifyReports(){
        string folder=Path.Combine(ReportRoot,".local","agent-reports");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"ui-report-fixture.json");
        if(File.Exists(file))throw new Exception("Refusing to overwrite an existing report fixture.");
        var path=entries.Where(e=>e.section=="software"||e.section=="website"||e.section=="secondary").SelectMany(e=>e.targets).Where(t=>t.kind=="folder").Select(t=>t.path).First(Directory.Exists);
        var now=DateTimeOffset.UtcNow.ToString("o");
        var feed=new StatusSnapshot{schemaVersion=1,generatedAt=now,producer=new StatusProducer{id="ui-report-fixture",name="Isolated fixture source",connection="online",heartbeatAt=now},tasks=new List<StatusTask>{new StatusTask{id="fixture",agent="Fixture only",projectPath=path,task="Report injection fixture",state="Working",updatedAt=now}}};
        try{
            File.WriteAllText(file,AppData.Json.Serialize(feed));Select("reports");
            if(!Visuals<TextBlock>(rows).Any(t=>t.Text=="Report injection fixture")||!Visuals<TextBlock>(rows).Any(t=>t.Text.Contains("Recently reported: Working")))throw new Exception("Report injection did not appear.");
            feed.tasks[0].state="Waiting for review";File.WriteAllText(file,AppData.Json.Serialize(feed));RenderCompact(false);
            if(!Visuals<TextBlock>(rows).Any(t=>t.Text.Contains("Recently reported: Waiting for review")))throw new Exception("Report update did not appear.");
            now=DateTimeOffset.UtcNow.AddMinutes(-3).ToString("o");feed.generatedAt=feed.producer.heartbeatAt=feed.tasks[0].updatedAt=now;File.WriteAllText(file,AppData.Json.Serialize(feed));RenderCompact(false);
            if(!Visuals<TextBlock>(rows).Any(t=>t.Text.Contains("Stale")&&t.Text.Contains("Waiting for review")))throw new Exception("Stale report looked fresh.");
            CaptureShell(profile,"reports-source-freshness.png",460,760);
            Console.WriteLine("Report checks passed: isolated report source, file injection, update, source label and two-minute stale treatment. Fixture file removed.");
        }finally{File.Delete(file);Select("software");}
    }

    void VerifyAssistantWork(){
        string config=StatePath("current-work-source.json"),source=Path.Combine(profile,"assistant-fixture.txt"),checklist=StatePath("current-work.json");
        File.WriteAllText(source,"CURRENT WORK\r\nACTIVE\r\n1. Twitch alerts\r\n   Status: Planning only; overlays complete.\r\n   Next: Review provider choice.\r\n2. Dashboard\r\n   Status: Review underway.\r\n   Next: Verify the interface.");
        File.WriteAllText(config,AppData.Json.Serialize(new AssistantWorkSource{schemaVersion=1,path=source}));
        File.WriteAllText(source,File.ReadAllText(source).Replace("Planning only; overlays complete.",String.Join(" ",Enumerable.Repeat("Planning only; overlays complete. Long fixture detail for the disclosure control.",8))));
        string sourceBefore=File.ReadAllText(source);
        var store=new ChecklistStore(checklist,entries);var doc=store.Read();store.Upsert(new WorkPatch{expectedRevision=doc.revision,id="personal-test",title="Preserved fixture",details="Existing details"});
        WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="local",path=source},WorkSources.Token(profile));
        SetCompact(true);Select("work");RefreshWorkAndWait();
        if(Visuals<TextBox>(rows).Any(t=>!t.IsReadOnly)||!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Twitch alerts"))throw new Exception("Quinn work did not render separated read-only items.");
        CaptureShell(profile,"quinn-work.png",460,760);AuditScrollbars(window);
        var disclosure=Visuals<Expander>(rows).First();disclosure.ApplyTemplate();
        var header=disclosure.Template.FindName("HeaderSite",disclosure) as System.Windows.Controls.Primitives.ToggleButton;
        if(header==null||!header.Focusable||!header.IsTabStop||header.FocusVisualStyle!=null)throw new Exception("Disclosure header lost keyboard navigation or retained native focus chrome.");
        header.ApplyTemplate();var frame=header.Template.FindName("DisclosureFrame",header) as Border;
        if(frame==null||frame.CornerRadius.TopLeft!=7||frame.BorderThickness.Left!=1)throw new Exception("Disclosure frame is not the intended custom control.");
        var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.CreatePeerForElement(disclosure);
        var provider=peer.GetPattern(System.Windows.Automation.Peers.PatternInterface.ExpandCollapse) as System.Windows.Automation.Provider.IExpandCollapseProvider;
        if(provider==null)throw new Exception("Expander lost its accessibility ExpandCollapse pattern.");
        provider.Expand();window.UpdateLayout();if(!disclosure.IsExpanded)throw new Exception("Accessible expand failed.");
        CaptureShell(profile,"disclosure-expanded.png",460,760);provider.Collapse();window.UpdateLayout();if(disclosure.IsExpanded)throw new Exception("Accessible collapse failed.");
        if(heading.Margin.Bottom<18||Find<StackPanel>("WorkNavigation").Children.OfType<WrapPanel>().First().Margin.Bottom<8)throw new Exception("Work heading/control spacing is still cramped.");
        Console.WriteLine("2.1.2 disclosure passed: custom rounded chrome, tab/focus properties, preserved accessibility ExpandCollapse provider, expand/collapse, and heading/control spacing.");
        File.WriteAllText(source,sourceBefore.Replace("Twitch alerts","Refreshed alerts"));RefreshWorkAndWait();
        if(!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Refreshed alerts"))throw new Exception("Refresh did not reread source.");
        ClickWork("Checklists");
        var add=Visuals<TextBox>(rows).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="New checklist item");add.Text="Inline added task";ClickWork("Add checklist item");
        var added=store.Read().items.Single(i=>i.title=="Inline added task");
        var itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));ClickIn(itemRow,"Edit");
        Visuals<TextBox>(itemRow).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist title").Text="Inline edited task";
        Visuals<TextBox>(itemRow).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist details").Text="Editable details";
        CaptureShell(profile,"inline-edit.png",460,760);AuditScrollbars(window);ClickIn(itemRow,"Save");
        if(store.Read().items.Single(i=>i.id==added.id).details!="Editable details")throw new Exception("Inline edit did not persist.");
        itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));ClickIn(itemRow,"Edit");Visuals<TextBox>(itemRow).First().Text="Cancelled title";ClickIn(itemRow,"Cancel");
        if(store.Read().items.Any(i=>i.title=="Cancelled title"))throw new Exception("Cancel saved a title.");
        itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));Visuals<CheckBox>(itemRow).Single().IsChecked=true;
        if(store.Read().items.Single(i=>i.id==added.id).state!="done")throw new Exception("Completion failed.");
        Visuals<Button>(rows).Single(x=>x.Content is string&&((string)x.Content).StartsWith("Done (")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));ClickIn(itemRow,"Edit");Visuals<TextBox>(itemRow).First().Text="Edited while Done";ClickIn(itemRow,"Save");
        if(store.Read().items.Single(i=>i.id==added.id).state!="done")throw new Exception("Editing Done restored completion.");
        itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));Visuals<CheckBox>(itemRow).Single().IsChecked=false;
        if(store.Read().items.Single(i=>i.id==added.id).state!="current")throw new Exception("Restore failed.");
        doneView=false;RenderCompact(true);itemRow=rows.Children.OfType<StackPanel>().Single(r=>Equals(r.Tag,added.id));ClickIn(itemRow,"Edit");var beforeConflict=store.Read();store.Upsert(new WorkPatch{expectedRevision=beforeConflict.revision,id=added.id,title="External concurrent update",details="Retain me"});
        Visuals<TextBox>(itemRow).First().Text="Stale editor";ClickIn(itemRow,"Save");
        if(store.Read().items.Single(i=>i.id==added.id).title!="External concurrent update"||!Visuals<TextBlock>(itemRow).Any(t=>t.Text.Contains("changed since")))throw new Exception("Stale inline edit was not safely rejected.");
        ClickIn(itemRow,"Cancel");CaptureShell(profile,"my-checklist.png",460,760);
        ClickWork("Agent tasks");File.Delete(source);RefreshWorkAndWait();if(!Visuals<TextBlock>(rows).Any(t=>t.Text.StartsWith("Refresh failed;")))throw new Exception("Missing source not reported.");
        if(!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Refreshed alerts"))throw new Exception("Offline refresh discarded cached work.");
        ClickWork("Checklists");if(!Visuals<CheckBox>(rows).Any())throw new Exception("Source failure blocked checklist.");
        File.WriteAllText(source,sourceBefore);ClickWork("Agent tasks");
        foreach(var size in new[]{new Size(520,420),new Size(580,760),new Size(700,800)}){window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();AuditHeader();AuditScrollbars(window);Capture(window.Content as UIElement,Path.Combine(profile,"work-"+size.Width+".png"),(int)size.Width-16,(int)size.Height-39);}
        window.Width=580;window.Height=760;window.UpdateLayout();
        foreach(int scale in new[]{150,200}){var target=window.Content as FrameworkElement;var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(target.ActualWidth*scale/100),(int)(target.ActualHeight*scale/100),96.0*scale/100,96.0*scale/100,PixelFormats.Pbgra32);image.Render(target);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(profile,"work-render-"+scale+".png")))encoder.Save(stream);}
        var dpi=VisualTreeHelper.GetDpi(window);Console.WriteLine("Actual window DPI: "+dpi.PixelsPerInchX+" x "+dpi.PixelsPerInchY);
        if(File.ReadAllText(source)!=sourceBefore)throw new Exception("Rendering changed authoritative source.");
        Select("software");foreach(var size in new[]{new Size(760,540),new Size(1280,860)}){window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();AuditHeader();AuditScrollbars(window);}
        Console.WriteLine("Experience checks passed: structured read-only Quinn list, refresh/missing source, inline add/edit/cancel, completion/Done edit/restore, stale edit rejection, toolbar geometry and all discovered scrollbar templates at compact/full sizes.");
    }
    void RefreshWorkAndWait(){
        RefreshWorkSource(personalTasksView);
        var frame=new DispatcherFrame();var until=DateTime.UtcNow.AddSeconds(10);
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(s,e)=>{if(!workReadStatus.StartsWith("Reading selected")||DateTime.UtcNow>=until)frame.Continue=false;};
        timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        if(workReadStatus.StartsWith("Reading selected"))throw new Exception("Refresh did not finish within the fixture deadline.");
        window.UpdateLayout();
    }
    void ClickWork(string label){var header=Find<StackPanel>("WorkNavigation");ClickIn(Visuals<Button>(header).Any(b=>Equals(b.Content,label))?header:rows,label);}
    static void ClickIn(DependencyObject root,string label){Visuals<Button>(root).Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    void AuditHeader(){
        var panel=Find<WrapPanel>("CompactToolbar");if(panel.Children.Count!=4||!panel.Children.OfType<Button>().Any(b=>Equals(b.Tag,"always-on-top")))throw new Exception("Missing header actions or pin control.");
        foreach(Button b in panel.Children.OfType<Button>().Where(x=>x.Visibility==Visibility.Visible)){if(b.ActualWidth>145||b.ActualHeight<25||b.HorizontalContentAlignment!=HorizontalAlignment.Center)throw new Exception("Header actions are not consistently sized and centered.");var group=(StackPanel)b.Content;var point=group.TranslatePoint(new Point(0,0),b);if(Math.Abs((b.ActualWidth-group.ActualWidth)/2-point.X)>2||Math.Abs((b.ActualHeight-group.ActualHeight)/2-point.Y)>2)throw new Exception("Header label/icon group is off center.");if(b.TranslatePoint(new Point(b.ActualWidth,0),panel).X>panel.ActualWidth+1)throw new Exception("Header action overflows.");}
    }
    static void AuditScrollbars(DependencyObject root){
        foreach(var bar in Visuals<System.Windows.Controls.Primitives.ScrollBar>(root)){
            bar.ApplyTemplate();if(bar.Style==null)throw new Exception("Native scrollbar escaped Forge style.");var track=bar.Template.FindName("PART_Track",bar) as System.Windows.Controls.Primitives.Track;if(track==null||track.Thumb==null)throw new Exception("Scrollbar track missing.");track.Thumb.ApplyTemplate();var border=Visuals<Border>(track.Thumb).FirstOrDefault();if(border==null||border.CornerRadius.TopLeft!=4)throw new Exception("Scrollbar thumb is not Forge rounded charcoal.");
        }
    }

    void VerifyViewControls(){
        Select("software");window.UpdateLayout();
        var header=Find<WrapPanel>("CompactToolbar");
        var toggle=header.Children.OfType<Button>().Single(b=>Equals(b.Tag,"view-toggle"));
        if(System.Windows.Automation.AutomationProperties.GetName(toggle)!="Tasks"||Visuals<Button>(Find<StackPanel>("SidebarFooter")).Any(b=>System.Windows.Automation.AutomationProperties.GetName(b).Equals("Tasks",StringComparison.OrdinalIgnoreCase)))throw new Exception("Dashboard has a redundant or incorrect work control.");
        var bounds=new Rect(window.Left,window.Top,window.Width,window.Height);
        for(int pass=0;pass<3;pass++){
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
            if(Compact||System.Windows.Automation.AutomationProperties.GetName(toggle)!="Dashboard"||heading.Text!="Tasks")throw new Exception("Work navigation or return label failed.");
            if(new Rect(window.Left,window.Top,window.Width,window.Height)!=bounds)throw new Exception("Normal navigation resized or moved the window.");
            var modes=Find<StackPanel>("WorkNavigation").Children.OfType<WrapPanel>().First();
            var refresh=modes.Children.OfType<Button>().Last();
            if(System.Windows.Automation.AutomationProperties.GetName(refresh)!="Refresh"||refresh.Height!=36)
                throw new Exception("Refresh is missing from the task selector row.");
            ClickWork("Checklists");window.UpdateLayout();
            modes=Find<StackPanel>("WorkNavigation").Children.OfType<WrapPanel>().First();
            if(modes.Children.OfType<Button>().Any(button=>System.Windows.Automation.AutomationProperties.GetName(button)=="Refresh"))
                throw new Exception("Checklists shows a task-file Refresh button.");
            ClickWork("Agent tasks");toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
            if(Compact||System.Windows.Automation.AutomationProperties.GetName(toggle)!="Tasks")throw new Exception("Dashboard return failed.");
        }
        SetCompact(true);window.UpdateLayout();
        if(!Compact||section!="work"||Find<Border>("Sidebar").IsVisible)throw new Exception("Explicit compact mode failed.");
        window.Topmost=true;UpdateHeaderActions();if(!window.Topmost)throw new Exception("Compact On top failed.");window.Topmost=false;
        CaptureShell(profile,"explicit-compact.png",460,660);
        SetCompact(false);window.UpdateLayout();
        if(Compact||new Rect(window.Left,window.Top,window.Width,window.Height)!=bounds)throw new Exception("Expand failed to restore exact full bounds.");
        window.WindowState=WindowState.Maximized;SetCompact(true);SetCompact(false);if(window.WindowState!=WindowState.Maximized)throw new Exception("Expand lost maximized state.");window.WindowState=WindowState.Normal;
        foreach(string id in new[]{"software","media","website","documents","everyday","secondary","all"}){
            string label=SectionLabel(id,SidebarStore.DefaultLabel(id));
            if(label=="Software Projects"||label=="Broadcast & Media"||label=="Development Documents"||label=="All Links")throw new Exception("Default navigation label is not sentence case.");
        }
        if(SectionLabel("software","Rob's Custom Alias")!="Rob's Custom Alias")throw new Exception("User alias was changed.");
        Select("software");CaptureShell(profile,"controls-dashboard.png",1280,860);
        Console.WriteLine("2.1.1 controls passed: one view toggle, repeated forward/back navigation, sentence-case defaults/custom-alias preservation, Refresh geometry/padding parity in both work tabs.");
    }

    void VerifyFixture()
    {
        verifying=true;
        VerifyReports();
        Select("work");string reportsSection=section;double reportsWidth=window.Width;
        VerifyDialog(ShowReports,"Agent updates","reports-window.png",dialog=>{
            if(dialog.Owner!=window||section!=reportsSection||window.Width!=reportsWidth)throw new Exception("Report window changed main navigation or owner.");
            if(!Visuals<TextBlock>(dialog).Any(t=>t.Text=="Source: project-local agent reports"))throw new Exception("Report window missing source.");
            var refresh=Visuals<Button>(dialog).Single(b=>Equals(b.Content,"Refresh"));var close=Visuals<Button>(dialog).Single(b=>Equals(b.Content,"Close"));
            var rp=refresh.TranslatePoint(new Point(),dialog);var cp=close.TranslatePoint(new Point(),dialog);
            if(Math.Abs(rp.Y-cp.Y)>1||rp.X>=cp.X)throw new Exception("Report footer is not one bottom row.");
            RenderReportsInto(reportsRows,true);if(reportsRows.Margin.Right<14)throw new Exception("Report scrollbar gutter missing.");
        });
        if(reportsWindow!=null||rows.Margin.Right<14)throw new Exception("Reports cleanup or main scrollbar gutter failed.");
        VerifyAssistantWork();
        VerifyViewControls();
        string catalog=Path.Combine(profile,"catalog.json");
        var before=File.ReadAllText(catalog);
        if(rows.Children.Count==0)throw new InvalidOperationException("Expected sample catalog rows.");
        var menu=Find<Menu>("AppMenu");
        if(menu.Style==null||menu.Background==null)throw new InvalidOperationException("Forge menu style did not load.");
        CaptureShell(profile,"directory.png",1280,860);
        VerifyDialog(EditCatalog,"Edit Dashboard","edit.png",dialog=>{
            var sectionPicker=Visuals<ComboBox>(dialog).Single();
            if((string)sectionPicker.SelectedValue!="software")throw new InvalidOperationException("Edit lost section context.");
            sectionPicker.SelectedValue="all";
            if(!Visuals<ListBox>(dialog).Any(list=>list.Items.Count==entries.Count))
                throw new InvalidOperationException("All entries did not load.");
        });
        var original=DashboardAccent.Selected;
        VerifyDialog(Settings,"Local Dashboard settings","settings.png",dialog=>{
            Click(dialog,"Window");var checks=Visuals<CheckBox>(dialog).ToList();
            if(checks.Count!=4||checks[0].IsChecked!=true||checks[2].IsChecked!=false)
                throw new InvalidOperationException("Settings checkboxes differ from the fixture defaults.");
            if(!checks[2].IsEnabled)throw new Exception("Preview startup checkbox is disabled.");
            checks[2].Focus();if(!checks[2].IsKeyboardFocused)throw new Exception("Startup checkbox cannot receive focus.");
            if(!checks[2].MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next))||!checks[3].IsKeyboardFocused)throw new Exception("Startup checkbox traps Tab focus.");
            checks[2].IsChecked=true;checks[2].IsChecked=false;
            Click(dialog,"Appearance");var input=Visuals<TextBox>(dialog).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color");
            var reset=Visuals<Button>(dialog).Single(b=>Equals(b.Content,"Reset"));
            var preview=Visuals<Button>(dialog).Single(b=>Equals(b.Content,"Save"));
            if(preview.Height!=reset.Height)throw new Exception("Save and Reset differ in height.");
            input.Text="#GG1010";
            if(preview.IsEnabled)throw new InvalidOperationException("Invalid preview enabled.");
            input.Text="#E8B8CD";
            WaitForAppearancePreview();
            if(DashboardAccent.Format(DashboardAccent.Selected)!="#E8B8CD")throw new InvalidOperationException("Preview failed.");
        });
        if(DashboardAccent.Selected!=original||File.Exists(StatePath("window-preferences.json")))
            throw new InvalidOperationException("Cancel persisted a preview.");
        if(File.ReadAllText(catalog)!=before)throw new InvalidOperationException("Read-only fixture unexpectedly changed.");
        VerifyDialog(EditCatalog,"Edit Dashboard","edit-save.png",dialog=>{
            var picker=Visuals<ComboBox>(dialog).Single();
            var input=Visuals<TextBox>(dialog).First(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Name");
            var entryList=Visuals<ListBox>(dialog).Single(list=>System.Windows.Automation.AutomationProperties.GetName(list)=="Dashboard entries");
            var editedEntry=entryList.SelectedItem;
            input.Text="Edited software fixture";
            picker.SelectedValue="workflows";
            input.Text="Edited workflow fixture";
            picker.SelectedValue="software";
            entryList.SelectedItem=editedEntry;
            if(input.Text!="Edited software fixture")throw new InvalidOperationException("Switching sections lost the draft.");
            Click(dialog,"Save");
        });
        var saved=File.ReadAllText(catalog);
        if(!saved.Contains("Edited software fixture")||!saved.Contains("Edited workflow fixture")||
           !saved.Contains("futureCatalogField"))
            throw new InvalidOperationException("Cross-section save or unknown-field preservation failed.");
        var initialLayout=sidebarStore.Read().Layout;
        string first=initialLayout.items[0].id;
        string firstEntry=(string)((Border)rows.Children[0]).Tag;
        VerifyDialog(EditCatalog,"Edit Dashboard","editor-reorder-cancel.png",dialog=>VerifyEditorReorder(dialog,first,firstEntry));
        if(sidebarStore.Read().Layout.items[0].id!=first||!Equals(((Border)rows.Children[0]).Tag,firstEntry))
            throw new InvalidOperationException("Cancel changed the Dashboard order.");
        VerifyDialog(EditCatalog,"Edit Dashboard","editor-reorder-save.png",dialog=>{
            VerifyEditorReorder(dialog,first,firstEntry);
            Click(dialog,"Save");
        });
        var savedLayout=sidebarStore.Read().Layout;
        if(savedLayout.items[1].id!=first||savedLayout.orders["software"][1]!=firstEntry)
            throw new InvalidOperationException("Save lost category or link order.");
        VerifyDialog(Settings,"Local Dashboard settings","settings-save.png",dialog=>{
            Visuals<TextBox>(dialog).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color").Text="#E8B8CD";
            WaitForAppearancePreview();
            Click(dialog,"Save");
        });
        preferenceStore.Reload();
        if(preferenceStore.Value.accentColor!="#E8B8CD"||DashboardAccent.Format(DashboardAccent.Selected)!="#E8B8CD")
            throw new InvalidOperationException("Save failed to retain the accent.");
        window.UpdateLayout();VerifyNativeIcon();
        Select("work");
        CaptureShell(profile,"compact.png",380,420);
        Select("reports");
        CaptureShell(profile,"reports.png",460,660);
        Exit();
    }

    void VerifyDialog(Action open,string title,string image,Action<Window> inspect)
    {
        Console.Error.WriteLine("Checking "+image);
        Exception failure=null;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(150)};
        timer.Tick+=(s,e)=>{
            var dialog=Application.Current.Windows.Cast<Window>().FirstOrDefault(w=>w.Title==title);
            if(dialog==null)return;
            timer.Stop();
            try
            {
                inspect(dialog);
                dialog.UpdateLayout();AuditScrollbars(dialog);
                if(Application.Current.Windows.Cast<Window>().Contains(dialog))Capture(dialog.Content as UIElement,Path.Combine(profile,image),
                    (int)dialog.Width-16,(int)dialog.Height-39);
            }
            catch(Exception error){failure=error;}
            finally{if(Application.Current.Windows.Cast<Window>().Contains(dialog))dialog.Close();}
        };
        timer.Start();
        try{open();}
        finally{timer.Stop();}
        if(failure!=null)throw failure;
    }

    static void Click(Window dialog,string name)
    {
        Visuals<Button>(dialog).Single(b=>Equals(b.Content,name)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    static IEnumerable<T> Visuals<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            var match=child as T;
            if(match!=null)yield return match;
            foreach(var descendant in Visuals<T>(child))yield return descendant;
        }
    }
}
