using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal sealed partial class WpfDashboard
{
    bool nonWindowedVerification;
    void VerifyLayoutOnly()
    {
        verifying=nonWindowedVerification=true;window.Left=100;window.Top=100;
        Action ensureHidden=()=>{
            if(window.IsVisible||new System.Windows.Interop.WindowInteropHelper(window).Handle!=IntPtr.Zero)
                throw new Exception("Non-windowed verification unexpectedly created a native window.");
        };
        Action<string,int,int> shell=(name,width,height)=>{
            CaptureShell(profile,name,width,height);AlignHeaderGutter();
            CaptureShell(profile,name,width,height);ensureHidden();
        };
        try
        {
            var folder=entries.SelectMany(e=>e.targets).First(t=>t.kind=="folder");
            entries.Add(new Entry{id="software-folder",name="Sample software folder",section="roots",targets=new System.Collections.Generic.List<LinkTarget>{new LinkTarget{id="folder",kind="folder",path=folder.path}}});
            entries.Add(new Entry{id="websites-folder",name="Sample website folder",section="roots",targets=new System.Collections.Generic.List<LinkTarget>{new LinkTarget{id="folder",kind="folder",path=folder.path}}});
            foreach(string id in new[]{"software","media","website","documents","everyday","workflows","secondary","all"})
            {
                Select(id);shell("layout-"+id+".png",1280,860);AuditHeader();AuditScrollbars(window.Content as DependencyObject);
                var header=Find<DockPanel>("SearchBar");
                double headerRight=header.TranslatePoint(new Point(header.ActualWidth,0),window.Content as UIElement).X;
                double rowsRight=rows.TranslatePoint(new Point(rows.ActualWidth,0),window.Content as UIElement).X;
                if(Math.Abs(headerRight-rowsRight)>1)throw new Exception("Header/list right edges differ in "+id+": "+headerRight+" / "+rowsRight);
            }
            Select("software");shell("layout-directory.png",1280,860);
            Panel toolbar=Find<StackPanel>("FullWindowActions");
            var bounds=toolbar.Children.OfType<Button>().Where(b=>Equals(b.Tag,"view-toggle")||Equals(b.Tag,"compact-toggle")).Select(b=>new Rect(b.TranslatePoint(new Point(),toolbar),new Size(b.ActualWidth,b.ActualHeight))).ToArray();
            var full=new Rect(window.Left,window.Top,window.Width,window.Height);
            Select("work");shell("layout-work.png",1280,860);
            if(new Rect(window.Left,window.Top,window.Width,window.Height)!=full)throw new Exception("Normal work navigation changed full bounds.");
            var after=toolbar.Children.OfType<Button>().Where(b=>Equals(b.Tag,"view-toggle")||Equals(b.Tag,"compact-toggle")).Select(b=>new Rect(b.TranslatePoint(new Point(),toolbar),new Size(b.ActualWidth,b.ActualHeight))).ToArray();
            if(!bounds.SequenceEqual(after))throw new Exception("Header actions jumped between Dashboard and Current Work.");
            SetCompact(true);toolbar=Find<WrapPanel>("CompactToolbar");shell("layout-compact-minimum.png",(int)window.MinWidth,760);AuditHeader();
            var previous=toolbar.Children.OfType<Button>().First(b=>b.Visibility==Visibility.Visible);var expand=toolbar.Children.OfType<Button>().Single(b=>Equals(b.Tag,"compact-toggle"));
            var ep=expand.TranslatePoint(new Point(),toolbar);var pp=previous.TranslatePoint(new Point(),toolbar);
            if(ep.Y>pp.Y&&ep.Y-(pp.Y+previous.ActualHeight)<7)throw new Exception("Wrapped Expand touches the first toolbar row.");
            SetCompact(false);if(new Rect(window.Left,window.Top,window.Width,window.Height)!=full)throw new Exception("Expand lost the saved full-window bounds.");
            Select("software");
            presentDialog=dialog=>{
                Capture(dialog.Content as UIElement,Path.Combine(profile,"layout-dialog-"+SafeName(dialog.Title)+".png"),(int)dialog.Width-16,(int)dialog.Height-39);
                if(dialog.IsVisible||new System.Windows.Interop.WindowInteropHelper(dialog).Handle!=IntPtr.Zero)throw new Exception("Dialog created a native window.");
                if(dialog.Title=="Edit Dashboard")
                {
                    var destinations=Visuals<ListBox>(dialog.Content as DependencyObject).Single(l=>System.Windows.Automation.AutomationProperties.GetName(l)=="Destination");
                    if(destinations.Items.Count==2){var scroll=Visuals<ScrollViewer>(destinations).First();if(scroll.ExtentHeight>scroll.ViewportHeight+.5)throw new Exception("Two target rows have an unnecessary scrollbar.");}
                }
                if(dialog.Title=="Local Dashboard settings")
                {
                    var input=Visuals<TextBox>(dialog.Content as DependencyObject).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color");
                    input.Text="#FF6600";WaitForAppearancePreview();
                    if(DashboardAccent.Format(DashboardAccent.Selected)!="#FF6600")throw new Exception("Live preview changed the exact color.");
                    var save=Visuals<Button>(dialog.Content as DependencyObject).Single(b=>Equals(b.Content,"Save"));
                    input.Text="#GG6600";if(save.IsEnabled)throw new Exception("Invalid hex enables Save.");
                    input.Text="#9AD9C1";WaitForAppearancePreview();
                    foreach(string page in new[]{"Window","Tasks","Appearance"}){
                        ClickIn(dialog.Content as DependencyObject,page);
                        Capture(dialog.Content as UIElement,Path.Combine(profile,"layout-settings-"+SafeName(page)+".png"),(int)dialog.Width-16,(int)dialog.Height-39);
                        AuditControlSizes(dialog.Content as DependencyObject);
                        if(page=="Tasks"){
                            if(!Visuals<Button>(dialog.Content as DependencyObject).Any(action=>Equals(action.Content,"Use default checklist")))throw new Exception("Checklist storage controls are missing.");
                        }
                    }
                }
                if(dialog.Title=="Online connections: coming soon"&&Visuals<Button>(dialog.Content as DependencyObject).Any(b=>Convert.ToString(b.Content).Contains("Sign in")))throw new Exception("Disabled provider surface exposes sign-in.");
                AuditControlSizes(dialog.Content as DependencyObject);
                dialog.Close();return false;
            };
            EditCatalog();Settings("Appearance");ShowHelp();ShowAbout();ShowReportIssue();ShowReports();ShowProviderSetup();ReviewDestination();ShowDataFolder();DataConfirmation("Back up Dashboard","One file for your Dashboard","Settings, catalog references, favorites, layout, Agent Work and My checklist are included. Linked content and status reports stay separate.","Continue");ensureHidden();
            DataConfirmation("Restore Dashboard","Replace Dashboard configuration?","This replaces saved settings, catalog, favorites, source selection, legacy managed Agent Work and My checklist with the backup. External work lists, cached snapshots, linked files, status reports and Windows startup registration are not changed. Back up your current configuration first if you need it.","Restore");
            DataConfirmation("Backup saved","Dashboard backup saved","Your linked files remain in their folders.","Close");ensureHidden();
            Select("work");taskListView=-1;RenderCompact(true);shell("layout-checklist.png",1280,860);
            if(Visuals<Button>(rows).Any(action=>Equals(action.Content,"Edit")||Equals(action.Content,"Add checklist item")))
                throw new Exception("Checklist browsing exposes editing controls.");
            if(Find<MenuItem>("CompactMenu").Icon==null)throw new Exception("Compact view menu icon missing.");
            System.Drawing.Color parsed;
            foreach(var hex in new[]{"#FF6600","#000000","#101010","#FFFFFF"})if(!DashboardAccent.TryParse(hex,out parsed)||DashboardAccent.Format(parsed)!=hex)throw new Exception("Valid custom hex rejected.");
            AuditControlSizes(window.Content as DependencyObject);
            Console.WriteLine("Layout checks passed: accent preview/validation, aligned fields, Settings pages, unified provider choices, button sizes, read-only checklist layout and Compact menu icon.");
            var proof=new StackPanel{Background=(Brush)window.FindResource("Background"),Margin=new Thickness(24),Width=420};
            proof.Resources["DashboardAccent"]=window.Resources["DashboardAccent"];
            var list=new ListBox{ItemsSource=new[]{"Selected item","Keyboard focus item"},SelectedIndex=0};proof.Children.Add(list);
            var field=new TextBox{Text="Display name or destination",Margin=new Thickness(0,12,0,12)};proof.Children.Add(field);
            var disclosure=new Expander{Header="Full status and next step",Content=new TextBlock{Text="Sample detail"}};proof.Children.Add(disclosure);
            var button=new Button{Content="Standard action"};proof.Children.Add(button);
            Capture(proof,Path.Combine(profile,"focus-treatment-base.png"),460,320);
            field.ApplyTemplate();((Border)field.Template.FindName("InputFocus",field)).Visibility=Visibility.Visible;
            var disclosureHeader=(System.Windows.Controls.Primitives.ToggleButton)disclosure.Template.FindName("HeaderSite",disclosure);disclosureHeader.ApplyTemplate();
            var chevron=disclosureHeader.Template.FindName("DisclosureChevron",disclosureHeader) as System.Windows.Shapes.Path;
            if(chevron==null)throw new Exception("Disclosure focus proof requires a chevron in the header template.");
            disclosureHeader.SetResourceReference(Control.ForegroundProperty,"DashboardAccent");
            chevron.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty,"DashboardAccent");
            var selected=(ListBoxItem)list.ItemContainerGenerator.ContainerFromIndex(0);selected.ApplyTemplate();
            if(selected.Foreground!=window.FindResource("DashboardAccent"))throw new Exception("Selected item does not use the accent text brush.");
            ((Border)selected.Template.FindName("ItemFocus",selected)).Visibility=Visibility.Visible;
            foreach(int scale in new[]{100,125,150,200})
            {
                proof.Measure(new Size(460,320));proof.Arrange(new Rect(0,0,460,320));proof.UpdateLayout();
                var image=new RenderTargetBitmap(460*scale/100,320*scale/100,96.0*scale/100,96.0*scale/100,PixelFormats.Pbgra32);image.Render(proof);
                var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));
                using(var stream=File.Create(Path.Combine(profile,"focus-treatment-proof-"+scale+".png")))encoder.Save(stream);
            }
            string reviewFile=Path.Combine(profile,"review-layout.txt");File.WriteAllText(reviewFile,"Isolated review layout fixture.");
            var reviewPanel=new StackPanel{Margin=new Thickness(24)};AddReviewAction(reviewPanel,new StatusTask{id="layout-review",projectPath=profile,state="Waiting for review",review=new StatusReview{title="Layout review",target=reviewFile,verifiedAt=DateTimeOffset.UtcNow.ToString("o")}});Capture(reviewPanel,Path.Combine(profile,"layout-review-action.png"),650,420);
            Select("software");pins.Add("demo-project");Render();CaptureShell(profile,"layout-star-selected-dark.png",1000,760);
            DashboardTheme.Apply("Light");ApplyAccent(DashboardAccent.Selected);RefreshGlyphs();CaptureShell(profile,"layout-theme-light.png",1000,760);
            if(DashboardTheme.Dark||((SolidColorBrush)Application.Current.Resources["ThemeF2F2F2"]).Color.R!=32)throw new Exception("Light theme did not apply.");
            pins.Remove("demo-project");Render();CaptureShell(profile,"layout-star-light.png",1000,760);
            DashboardTheme.Apply("Dark");ApplyAccent(DashboardAccent.Selected);RefreshGlyphs();ensureHidden();
            taskListView=0;VerifyAssistantWork();ensureHidden();VerifySourceSettings();VerifyPolishBehavior();VerifyClarityBehavior();VerifyNextBehavior();VerifyTaskListBehavior();VerifyPersonalEditorBehavior();ensureHidden();
            Console.WriteLine("Non-windowed layout checks passed: category header/list edges, view/button geometry, compact center, editor overflow, Settings, Help/About/Reports and selection resources.");
            Console.WriteLine("Focus proofs simulate the current template focus cues for visual inspection at 100/125/150/200% render density; these do not certify physical keyboard focus, Windows scaling or taskbar appearance.");
            Console.WriteLine("No native Dashboard or dialog window was created. Running app/profile and desktop were not used.");
        }
        finally{refreshTimer.Stop();tray.Dispose();trayIcon.Dispose();}
    }
    static string SafeName(string value){return new string(value.Select(c=>Char.IsLetterOrDigit(c)?c:'-').ToArray());}
    void VerifySourceSettings(){
        var original=WorkSources.Read(profile);
        try{
            Action save;var settings=BuildWorkSource(out save);string before=WorkSources.Token(profile);save();
            if(WorkSources.Token(profile)!=before)throw new Exception("Unchanged Tasks Settings rewrote the source.");
            if(Visuals<ComboBox>(settings).Any())throw new Exception("Tasks Settings still exposes redundant source categories.");
            if(Visuals<TextBox>(settings).Count(t=>System.Windows.Automation.AutomationProperties.GetName(t).EndsWith("file location"))!=3)throw new Exception("Tasks Settings does not show all three file locations.");
            foreach(var button in Visuals<Button>(settings))if(button.Height!=36)throw new Exception("Tasks Settings has a nonstandard button.");
            WorkSources.Save(profile,new WorkSourceChoice{schemaVersion=1,kind="google-docs",path="LegacyDoc"},WorkSources.Token(profile));
            settings=BuildWorkSource(out save);before=WorkSources.Token(profile);save();
            if(WorkSources.Token(profile)!=before)throw new Exception("Legacy online selection changed without choosing a replacement.");
            if(!Visuals<TextBlock>(settings).Any(t=>t.Text.Contains("An online source is selected.")))throw new Exception("Preserved online selection is not explained.");
            Console.WriteLine("Task settings passed: three locations, consistent buttons, combined local/synced/network file choice and unchanged online-source preservation.");
        }finally{WorkSources.Save(profile,original,WorkSources.Token(profile));}
    }
    static void AuditControlSizes(DependencyObject root){
        foreach(var button in Visuals<Button>(root).Where(b=>b.Visibility==Visibility.Visible&&b.ActualHeight>0))
            if(Math.Abs(button.ActualHeight-(Equals(button.Tag,"editor-grip")?28:Equals(button.Tag,"sidebar-small-action")?30:36))>.5)throw new Exception("Unexpected button height: "+button.Content+" / "+button.ActualHeight);
        foreach(var field in Visuals<TextBox>(root).Where(t=>t.Visibility==Visibility.Visible&&!t.AcceptsReturn&&t.ActualHeight>0))
            if(Math.Abs(field.ActualHeight-36)>.5)throw new Exception("Unexpected input height: "+System.Windows.Automation.AutomationProperties.GetName(field));
    }
}
