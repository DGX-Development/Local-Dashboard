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
        VerifyNativeIcon();
        var sequences=new[]{new[]{"Amber","Blue","Amber"},new[]{"Violet","Rose","Mint"},new[]{"Blue","Violet","Amber"}};
        foreach(var sequence in sequences)foreach(string preset in sequence){
            string selected=null;
            VerifyDialog(Settings,"Local Dashboard settings","visible-settings-save.png",dialog=>{
                var button=Visuals<Button>(dialog).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==preset+" accent");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                SettleTaskbarPreview();
                selected=Visuals<TextBox>(dialog).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color").Text;
                if(DashboardAccent.Format(DashboardAccent.Selected)!=selected)throw new Exception(preset+" preview failed.");
                VerifyNativeIcon();Click(dialog,"Save");
            });
            SettleTaskbarPreview();preferenceStore.Reload();
            if(preferenceStore.Value.accentColor!=selected)throw new Exception(preset+" save failed.");
            VerifyNativeIcon();Console.WriteLine("Repeated Save diagnostic passed: "+preset);
            VerifyDialog(Settings,"Local Dashboard settings","visible-settings-cancel.png",dialog=>{
                var button=Visuals<Button>(dialog).Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==(preset=="Rose"?"Mint":"Rose")+" accent");
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));SettleTaskbarPreview();VerifyNativeIcon();Click(dialog,"Cancel");
            });
            SettleTaskbarPreview();
            if(DashboardAccent.Format(DashboardAccent.Selected)!=selected)throw new Exception("Cancel did not restore "+preset);
            VerifyNativeIcon();Console.WriteLine("Cancel diagnostic passed: "+preset);
        }
        Console.WriteLine("Three paced multi-save passes completed. Explorer pixels NOT verified by this diagnostic.");Exit();
    }
    static void SettleTaskbarPreview(){
        var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(3)};
        timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    void VerifyNativeIcon(){
        var expected=DashboardAccent.Selected;var hwnd=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if(ShellTaskbarBrand.ProcessIdentity()!=shellBrand.AppId)throw new Exception("Process and window taskbar identities differ.");
        if(shellBrand.Read(hwnd,2)!=shellBrand.RelaunchCommand)throw new Exception("Shell relaunch command differs from the selected profile command.");
        if(shellBrand.Read(hwnd,5)!=shellBrand.AppId||shellBrand.Read(hwnd,3)!=shellBrand.IconResource)throw new Exception("Shell taskbar identity or icon resource differs.");
        var iconPath=shellBrand.IconResource.Substring(0,shellBrand.IconResource.Length-2);if(!File.Exists(iconPath)||iconPath!=shellBrand.RefreshKey||DashboardData.Hash(File.ReadAllBytes(iconPath))!=DashboardData.Hash(DashboardBrand.IconBytes(expected)))throw new Exception("The relaunch icon does not identify the current accent.");
        Console.WriteLine("Shell taskbar properties read back: "+shellBrand.AppId+" / "+shellBrand.IconResource+". Actual taskbar appearance still requires visual verification.");
        foreach(int kind in new[]{0,1}){
            var handle=SendMessage(hwnd,0x007F,new IntPtr(kind),IntPtr.Zero);if(handle==IntPtr.Zero)throw new Exception("Missing native window icon kind "+kind);
            using(var icon=System.Drawing.Icon.FromHandle(handle))
            using(var image=icon.ToBitmap())
            using(var generated=new System.Drawing.Icon(iconPath,image.Width,image.Height))
            using(var reference=System.Drawing.Icon.FromHandle(generated.Handle))
            using(var expectedFrame=reference.ToBitmap())
            {
                if(expectedFrame.Size!=image.Size)throw new Exception("Native icon dimensions do not match the generated frame.");
                for(int y=0;y<image.Height;y++)
                    for(int x=0;x<image.Width;x++)
                        if(image.GetPixel(x,y).ToArgb()!=expectedFrame.GetPixel(x,y).ToArgb())
                            throw new Exception("Native icon pixels differ from the generated "+image.Width+" x "+image.Height+" frame.");
                Console.WriteLine("Native "+(kind==0?"small":"large")+" window icon matches the generated "+image.Width+" x "+image.Height+" frame.");
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
        if(Visuals<TextBox>(rows).Any(t=>!t.IsReadOnly)||!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Twitch alerts"))throw new Exception("Task list 1 did not render separate items.");
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
        if(Find<DockPanel>("SearchBar").Margin.Bottom<14)throw new Exception("Work search spacing is still cramped.");
        Console.WriteLine("2.1.2 disclosure passed: custom rounded chrome, tab/focus properties, preserved accessibility ExpandCollapse provider, expand/collapse, and heading/control spacing.");
        File.WriteAllText(source,sourceBefore.Replace("Twitch alerts","Refreshed alerts"));RefreshWorkAndWait();
        if(!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Refreshed alerts"))throw new Exception("Refresh did not reread source.");
        ClickWork("Checklists");
        var draft=store.Upsert(new WorkPatch{expectedRevision=store.Read().revision,id="checkoff-fixture",title="Checklist checkoff fixture"});
        RenderTaskItems();
        var itemRow=rows.Children.OfType<StackPanel>().Single(row=>Equals(row.Tag,"checkoff-fixture"));
        Visuals<CheckBox>(itemRow).Single().IsChecked=true;
        if(store.Read().items.Single(i=>i.id=="checkoff-fixture").state!="done")throw new Exception("Completion failed.");
        Visuals<Button>(rows).Single(x=>x.Content is string&&((string)x.Content).StartsWith("Done (")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        itemRow=rows.Children.OfType<StackPanel>().Single(row=>Equals(row.Tag,"checkoff-fixture"));
        Visuals<CheckBox>(itemRow).Single().IsChecked=false;
        if(store.Read().items.Single(i=>i.id=="checkoff-fixture").state!="current")throw new Exception("Restore failed.");
        doneView=false;RenderTaskItems();CaptureShell(profile,"my-checklist.png",460,760);
        ClickWork("Task list 1");RefreshWorkAndWait();File.Delete(source);RefreshWorkAndWait();if(!Visuals<TextBlock>(rows).Any(t=>t.Text.StartsWith("Could not load the selected file.")))throw new Exception("Missing source not reported.");
        if(!Visuals<TextBlock>(rows).Any(t=>t.Text=="1. Refreshed alerts"))throw new Exception("Offline refresh discarded cached work.");
        ClickWork("Checklists");if(!Visuals<CheckBox>(rows).Any())throw new Exception("Source failure blocked checklist.");
        File.WriteAllText(source,sourceBefore);ClickWork("Task list 1");
        foreach(var size in new[]{new Size(520,420),new Size(580,760),new Size(700,800)}){window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();AuditHeader();AuditScrollbars(window);Capture(window.Content as UIElement,Path.Combine(profile,"work-"+size.Width+".png"),(int)size.Width-16,(int)size.Height-39);}
        window.Width=580;window.Height=760;window.UpdateLayout();
        foreach(int scale in new[]{150,200}){var target=window.Content as FrameworkElement;var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)(target.ActualWidth*scale/100),(int)(target.ActualHeight*scale/100),96.0*scale/100,96.0*scale/100,PixelFormats.Pbgra32);image.Render(target);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));using(var stream=File.Create(Path.Combine(profile,"work-render-"+scale+".png")))encoder.Save(stream);}
        var dpi=VisualTreeHelper.GetDpi(window);Console.WriteLine("Actual window DPI: "+dpi.PixelsPerInchX+" x "+dpi.PixelsPerInchY);
        if(File.ReadAllText(source)!=sourceBefore)throw new Exception("Rendering changed authoritative source.");
        Select("software");foreach(var size in new[]{new Size(760,540),new Size(1280,860)}){window.Width=size.Width;window.Height=size.Height;window.UpdateLayout();AuditHeader();AuditScrollbars(window);}
        Console.WriteLine("Experience checks passed: structured task list, refresh/missing source, checklist completion and restore, toolbar geometry and all discovered scrollbar templates at compact/full sizes.");
    }
    void RefreshWorkAndWait(){
        RefreshDisplayedTasks(true);
        var frame=new DispatcherFrame();var until=DateTime.UtcNow.AddSeconds(10);
        var timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(s,e)=>{if((taskReads[0]==null||taskReads[0].IsCompleted)&&!taskReadStatus[0].StartsWith("Loading the selected file")||DateTime.UtcNow>=until)frame.Continue=false;};
        timer.Start();try{Dispatcher.PushFrame(frame);}finally{timer.Stop();}
        if((taskReads[0]!=null&&!taskReads[0].IsCompleted)||taskReadStatus[0].StartsWith("Loading the selected file"))throw new Exception("Refresh did not finish within the fixture deadline.");
        window.UpdateLayout();
    }
    void ClickWork(string label){
        var button=TaskNavigationPanel().Children.OfType<Button>()
            .FirstOrDefault(item=>System.Windows.Automation.AutomationProperties.GetName(item)==label);
        if(button!=null)button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        else ClickIn(rows,label);
    }
    static void ClickIn(DependencyObject root,string label){Visuals<Button>(root).Single(b=>Equals(b.Content,label)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    void AuditHeader(){
        Panel panel=Compact?(Panel)Find<WrapPanel>("CompactToolbar"):Find<StackPanel>("FullWindowActions");
        var actions=panel.Children.OfType<Button>().ToArray();
        if(actions.Length!=2||!actions.Any(button=>Equals(button.Tag,"compact-toggle"))||!actions.Any(button=>Equals(button.Tag,"always-on-top")))
            throw new Exception("Expected the view and pin actions in the active header.");
        foreach(var button in actions)
        {
            if(button.ActualHeight!=36||button.ActualWidth!=36||String.IsNullOrEmpty(System.Windows.Automation.AutomationProperties.GetName(button)))
                throw new Exception("A header action has an unexpected size or missing name.");
            if(button.TranslatePoint(new Point(button.ActualWidth,0),panel).X>panel.ActualWidth+1)
                throw new Exception("A header action overflows.");
        }
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
            var refresh=Find<DockPanel>("SearchBar").Children.OfType<Button>().Single(button=>Equals(button.Tag,"task-refresh"));
            if(System.Windows.Automation.AutomationProperties.GetName(refresh)!="Refresh"||refresh.Height!=36)
                throw new Exception("Refresh is missing from the task selector row.");
            ClickWork("Checklists");window.UpdateLayout();
            if(refresh.Visibility!=Visibility.Visible)throw new Exception("Checklists cannot refresh.");
            ClickWork("Task list 1");toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));window.UpdateLayout();
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
        if(reportsWindow!=null||rows.Margin.Right<7)throw new Exception("Reports cleanup or main scrollbar gutter failed.");
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
