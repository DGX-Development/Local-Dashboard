using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal sealed partial class WpfDashboard
{

    readonly Window window;
    readonly string profile;
    readonly bool fixture;
    readonly bool refreshShell;
    readonly StackPanel rows;
    readonly TextBox search;
    readonly TextBlock heading;
    SidebarLayout layout;
    System.Collections.Generic.List<Entry> entries;
    string section="software", lastDashboardSection="software", expandedSection;

    WpfDashboard(string profile,bool fixture,bool refreshShell)
    {
        this.profile=profile;
        this.fixture=fixture;
        this.refreshShell=refreshShell;
        window=Load<Window>("Shell.xaml");
        window.Title="Local Dashboard";
        rows=Find<StackPanel>("Rows");
        search=Find<TextBox>("Search");
        heading=Find<TextBlock>("Heading");
        string iconPath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"local-dashboard.ico");
        if(!File.Exists(iconPath))iconPath=Path.Combine(AppData.Root,"dist","icons","local-dashboard.ico");
        var icon=new BitmapImage(new Uri(iconPath));
        window.Icon=Find<Image>("Brand").Source=icon;
        Find<MenuItem>("RefreshMenu").Click+=(s,e)=>Protect(()=>{Reload();});
        Find<MenuItem>("ExitMenu").Click+=(s,e)=>Exit();
        Find<MenuItem>("SettingsMenu").Click+=(s,e)=>Protect(Settings);
        Find<MenuItem>("BackupMenu").Click+=(s,e)=>Protect(BackupConfiguration);
        Find<MenuItem>("RestoreMenu").Click+=(s,e)=>Protect(RestoreConfiguration);
        Find<MenuItem>("HideMenu").Click+=(s,e)=>Hide();
        Find<MenuItem>("GuideMenu").Click+=(s,e)=>ShowHelp();
        Find<MenuItem>("SetupMenu").Click+=(s,e)=>Protect(ShowSetup);
        Find<MenuItem>("ReportIssueMenu").Click+=(s,e)=>ShowReportIssue();
        var topMenu=Find<MenuItem>("TopMenu");
        topMenu.Click+=(s,e)=>{window.Topmost=topMenu.IsChecked;UpdateHeaderActions();};
        window.PreviewKeyDown+=(s,e)=>{
            if(e.Key==System.Windows.Input.Key.F1){ShowHelp();e.Handled=true;}
            else if(System.Windows.Input.Keyboard.Modifiers==System.Windows.Input.ModifierKeys.Control)
            {
                if(e.Key==System.Windows.Input.Key.R){Protect(()=>{Reload();});e.Handled=true;}
                else if(e.Key==System.Windows.Input.Key.Q){Exit();e.Handled=true;}
            }
            else if(System.Windows.Input.Keyboard.Modifiers==(System.Windows.Input.ModifierKeys.Control|System.Windows.Input.ModifierKeys.Shift)&&e.Key==System.Windows.Input.Key.C)
            {Select("work");e.Handled=true;}
        };
        Find<Button>("OpenFolder").Click+=(s,e)=>Protect(()=>{
            var root=entries.FirstOrDefault(i=>i.id==(section=="software"?"software-folder":"websites-folder"));
            if(root!=null)AppData.Open(entries,root.id,root.targets.First(t=>t.kind=="folder").id,profile);
        });
        Find<MenuItem>("DirectoryMenu").Click+=(s,e)=>Select(DashboardSection());
        Find<MenuItem>("WorkMenu").Click+=(s,e)=>Select("work");
        Find<MenuItem>("ReportsMenu").Click+=(s,e)=>{agentUpdatesView=true;Select("work");};
        string[,] menuIcons={{"RefreshMenu","updates"},{"BackupMenu","config-backup"},{"RestoreMenu","config-restore"},{"SettingsMenu","settings"},{"HideMenu","hide"},{"ExitMenu","exit"},
            {"DirectoryMenu","dashboard"},{"CompactMenu","compact"},{"WorkMenu","checklist"},{"ReportsMenu","reports"},{"TopMenu","pin"},
            {"SetupMenu","setup"},{"GuideMenu","help"},{"ReportIssueMenu","report"},{"AboutMenu","info"}};
        for(int i=0;i<menuIcons.GetLength(0);i++)
            Find<MenuItem>(menuIcons[i,0]).Icon=GlyphImage(menuIcons[i,1]);
        Find<MenuItem>("AboutMenu").Click+=(s,e)=>ShowAbout();
        search.TextChanged+=(s,e)=>{if(search.Text.Length>0)section="all";Render();};
        Configure();
        var actions=Find<StackPanel>("FullWindowActions");
        var compactToggle=WindowIconButton("Compact view","compact",()=>SetCompact(!Compact));
        compactToggle.Tag="compact-toggle";actions.Children.Add(compactToggle);
        var top=WindowIconButton("On top","pin",()=>{
            window.Topmost=!window.Topmost;
            UpdateHeaderActions();
        },new OnTopButton());
        top.Tag="always-on-top";actions.Children.Add(top);
        Find<MenuItem>("CompactMenu").Click+=(s,e)=>SetCompact(!Compact);
        Find<ScrollViewer>("ContentScroll").ScrollChanged+=(s,e)=>AlignHeaderGutter();
        Find<ScrollViewer>("ContentScroll").SizeChanged+=(s,e)=>AlignHeaderGutter();
        Reload();
        UpdateHeaderActions();
    }

    T Find<T>(string name) where T:class {return (T)window.FindName(name);}
    static T Load<T>(string name)
    {
        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            return (T)XamlReader.Load(stream);
    }

    void Reload()
    {
        entries=AppData.ReadEntries(File.ReadAllText(Path.Combine(profile,"catalog.json")),profile);
        sidebarSnapshot=sidebarStore.Read();layout=sidebarSnapshot.Layout;
        foreach(string id in entries.Select(entry=>entry.section).Where(SidebarStore.Custom).Distinct())
            if(!layout.items.Any(item=>item.id==id))layout.items.Add(new SidebarItem{id=id,label=SidebarStore.DefaultLabel(id)});
        if(section!="work"&&section!="reports"&&!layout.items.Any(item=>item.id==section))section=layout.items.First(item=>SidebarStore.Known(item.id)).id;
        RefreshNavigation();
        Render();
    }

    void RefreshNavigation()
    {
        layout=sidebarSnapshot.Layout;
        var workspace=Find<StackPanel>("WorkspaceNavigation");
        workspace.Children.Clear();
        var dashboardButton=IconAction("Dashboard","dashboard",()=>{
            Select(DashboardSection());
            var selected=Find<StackPanel>("Navigation").Children.OfType<Button>().FirstOrDefault(button=>Equals(button.Tag,section));
            if(selected!=null&&selected.IsVisible)selected.Focus();
        });
        var tasksButton=IconAction("Tasks","checklist",()=>Select("work"));
        foreach(var button in new[]{dashboardButton,tasksButton})
        {
            button.Height=36;
            button.Margin=new Thickness(0,0,0,6);
            workspace.Children.Add(button);
        }
        var navigation=Find<StackPanel>("Navigation");
        navigation.Children.Clear();
        foreach(var item in layout.items.Where(i=>SidebarStore.Known(i.id)))
        {
            string id=item.id;
            var content=new DockPanel();
            string glyph=HasDashboardGlyph(item.icon)?item.icon:SidebarStore.DefaultIcon(id);
            var navIcon=String.IsNullOrEmpty(item.iconPath)?GlyphImage(glyph):new Image{Width=24,Height=24,Source=CategoryIcon(item)};
            navIcon.Margin=new Thickness(0,0,12,0);content.Children.Add(navIcon);
            content.Children.Add(new TextBlock{Text=SectionLabel(item.id,item.label),VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
            var button=new Button{Content=content,Tag=id,HorizontalContentAlignment=HorizontalAlignment.Stretch,
                Margin=new Thickness(0,0,0,6),Padding=new Thickness(10,0,10,0)};
            button.Click+=(s,e)=>Select(id);
            navigation.Children.Add(button);
        }
        var editActions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        editActions.Children.Add(SidebarAction(ActionButton("Edit",()=>Protect(EditCatalog))));
        navigation.Children.Add(editActions);
        var footer=Find<StackPanel>("SidebarFooter");
        footer.Children.Clear();
        footer.Children.Add(IconAction("Settings","settings",()=>Protect(Settings)));

    }

    bool TasksWorkspace {get{return section=="work"||section=="reports";}}
    string DashboardSection()
    {
        return layout.items.Any(item=>item.id==lastDashboardSection)?lastDashboardSection:layout.items.First(item=>SidebarStore.Known(item.id)).id;
    }

    void Select(string id)
    {
        if(Compact&&id!="work")SetCompact(false);
        search.Clear();section=id!="work"&&id!="reports"&&!layout.items.Any(item=>item.id==id)?layout.items.First(item=>SidebarStore.Known(item.id)).id:id;
        UpdateViewChrome();Render();
    }

    void UpdateViewChrome()
    {
        Find<Border>("Sidebar").Visibility=Compact?Visibility.Collapsed:Visibility.Visible;
        Find<ColumnDefinition>("SidebarColumn").Width=new GridLength(Compact?0:252);
        Find<DockPanel>("MenuBar").Visibility=Compact?Visibility.Collapsed:Visibility.Visible;
        var compactActions=Find<WrapPanel>("CompactToolbar");
        compactActions.Visibility=Compact?Visibility.Visible:Visibility.Collapsed;
        Panel fullActions=Find<StackPanel>("FullWindowActions");
        Panel destination=Compact?(Panel)compactActions:fullActions;
        Panel previous=Compact?fullActions:compactActions;
        while(previous.Children.Count>0)
        {
            var button=previous.Children[0];
            previous.Children.RemoveAt(0);
            destination.Children.Add(button);
        }
        Find<DockPanel>("MainContent").Margin=Compact?new Thickness(22):new Thickness(30,24,30,20);
        heading.FontSize=Compact?24:29;
        bool work=section=="work"||section=="reports";
        Find<DockPanel>("SearchBar").Visibility=work?Visibility.Collapsed:Visibility.Visible;
        search.Visibility=work?Visibility.Collapsed:Visibility.Visible;
        UpdateHeaderActions();
    }

    void SetCompact(bool enabled)
    {
        if(enabled==Compact)return;
        double centerX=window.Left+window.Width/2,centerY=window.Top+window.Height/2;
        if(window.WindowState==WindowState.Maximized){var rect=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;var source=PresentationSource.FromVisual(window);var transform=source==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;var center=transform.Transform(new Point(rect.Left+rect.Width/2,rect.Top+rect.Height/2));centerX=center.X;centerY=center.Y;}
        if(enabled)
        {
            fullState=window.WindowState;
            fullBounds=window.WindowState==WindowState.Normal?new Rect(window.Left,window.Top,window.Width,window.Height):window.RestoreBounds;
            SaveFullBounds();
            expandedSection=section;
            Select("work");window.WindowState=WindowState.Normal;compactView=true;
            UpdateViewChrome();
            window.MinWidth=CompactMinimumWidth();window.MinHeight=420;window.Width=Math.Max(window.MinWidth,compactSize.Width);window.Height=Math.Max(window.MinHeight,compactSize.Height);
            window.Left=compactBounds.HasValue?compactBounds.Value.Left:centerX-window.Width/2;window.Top=compactBounds.HasValue?compactBounds.Value.Top:centerY-window.Height/2;
            if(!verifying)FitToWorkArea();
        }
        else
        {
            SaveCompactBounds();compactSize=new Size(window.Width,window.Height);compactView=false;
            if(expandedSection!=null)
                section=expandedSection=="work"||expandedSection=="reports"||layout.items.Any(item=>item.id==expandedSection)?expandedSection:DashboardSection();
            expandedSection=null;
            window.MinWidth=760;window.MinHeight=540;window.Width=fullBounds.Width;window.Height=fullBounds.Height;
            window.Left=fullBounds.Left;window.Top=fullBounds.Top;
            if(!verifying)FitToWorkArea();window.WindowState=fullState;
        }
        UpdateViewChrome();Render();
    }

    void AlignHeaderGutter()
    {
        var scroll=Find<ScrollViewer>("ContentScroll");
        if(scroll.ActualWidth<=0||Double.IsNaN(scroll.ViewportWidth))return;
        double gutter=rows.Margin.Right+Math.Max(0,scroll.ActualWidth-scroll.ViewportWidth);
        var bar=Find<DockPanel>("SearchBar");bar.Margin=new Thickness(0,12,gutter,14);
        var toolbar=Find<WrapPanel>("CompactToolbar");toolbar.Margin=new Thickness(0,0,gutter,12);
    }

    void Render()
    {
        rows.Children.Clear();
        Find<StackPanel>("WorkNavigation").Visibility=section=="work"?Visibility.Visible:Visibility.Collapsed;
        string workspace=section=="software"?"software-folder":section=="website"?"websites-folder":null;
        Find<Button>("OpenFolder").Visibility=workspace!=null&&entries.Any(i=>i.id==workspace)?Visibility.Visible:Visibility.Collapsed;
        if(!TasksWorkspace)lastDashboardSection=section;
        var navigation=Find<StackPanel>("Navigation");
        navigation.Visibility=TasksWorkspace?Visibility.Collapsed:Visibility.Visible;
        navigation.IsEnabled=!TasksWorkspace;
        var workspaceButtons=Find<StackPanel>("WorkspaceNavigation");
        if(workspaceButtons.Children.Count==2)
        {
            SetNavigationSelected((Button)workspaceButtons.Children[1],TasksWorkspace);
        }
        var buttons=navigation.Children.OfType<Button>();
        foreach(var nav in buttons.Where(b=>b.Tag is string))
        {
            bool active=(string)nav.Tag==section;
            SetNavigationSelected(nav,active);
        }
        if(section=="work"||section=="reports")
        {
            heading.Margin=new Thickness(0,0,0,18);
            heading.Text="Agent updates";
            RenderCompact(true);
            return;
        }
        heading.Margin=new Thickness(0);
        heading.Text=SectionLabel(section,layout.items.First(i=>i.id==section).label);
        string query=search.Text.Trim();
        var candidates=entries.Where(e=>DashboardForm.EntryInSection(e,section)&&
            (query.Length==0||(e.name+" "+e.description+" "+String.Join(" ",e.targets.Select(t=>t.path??t.url))).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0));
        foreach(var entry in OrderedDashboardEntries(candidates,layout,section))
        {
            var row=new Border{Tag=entry.id,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,0,2),CornerRadius=new CornerRadius(6)};
            row.SetResourceReference(Border.BackgroundProperty,"Surface");
            var content=new DockPanel();
            var targets=new StackPanel{Orientation=Orientation.Horizontal};
            DockPanel.SetDock(targets,Dock.Right);
            content.Children.Add(targets);
            foreach(var target in entry.targets)
            {
                var button=new Button{Content=String.IsNullOrWhiteSpace(target.label)?target.kind:target.label,
                    ToolTip=target.path??target.url,Height=36,MinWidth=88,Padding=new Thickness(10,0,10,0),Margin=new Thickness(0,0,6,0),VerticalAlignment=VerticalAlignment.Center,
                    IsEnabled=target.kind=="web"||File.Exists(target.path)||Directory.Exists(target.path)};
                button.Click+=(s,e)=>Protect(()=>AppData.Open(entries,entry.id,target.id,profile));
                targets.Children.Add(button);
            }
            var favorite=ActionButton("",()=>Protect(()=>TogglePin(entry.id)));
            var star=FluentImage(pins.Contains(entry.id)?"favorite-filled":"favorite",24,24);
            favorite.Content=star;
            favorite.Width=36;favorite.Height=36;favorite.Padding=new Thickness(0);favorite.Margin=new Thickness(0);favorite.VerticalAlignment=VerticalAlignment.Center;
            favorite.ToolTip=pins.Contains(entry.id)?"Remove favorite":"Add favorite";
            System.Windows.Automation.AutomationProperties.SetName(favorite,(pins.Contains(entry.id)?"Remove favorite: ":"Add favorite: ")+entry.name);
            if(pins.Contains(entry.id))favorite.SetResourceReference(Control.ForegroundProperty,"DashboardAccent");
            targets.Children.Add(favorite);
            var image=EntryIcon(entry);
            if(image!=null)
            {
                var artwork=new Image{Source=image,Width=40,Height=40,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,12,0)};
                RenderOptions.SetBitmapScalingMode(artwork,BitmapScalingMode.HighQuality);
                content.Children.Add(artwork);
            }
            content.Children.Add(new TextBlock{Text=entry.name,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis});
            row.Child=content;
            rows.Children.Add(row);
        }
    }

    void CaptureShell(string profile,string file,int width,int height)
    {
        window.Width=width;window.Height=height;window.UpdateLayout();
        Capture((UIElement)window.Content,Path.Combine(profile,file),width,height);
    }

    static void Capture(UIElement surface,string path,int width,int height)
    {
        surface.Measure(new Size(width,height));
        surface.Arrange(new Rect(0,0,width,height));
        surface.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);
        bitmap.Render(surface);
        var encoder=new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(path))encoder.Save(stream);
    }

    [STAThread]
    static int Main(string[] args)
    {
        AppInstanceGate applicationGate=null;
        bool diagnostic=args.Any(a=>a=="--verify-closeout"||a=="--verify-icon-save"||a=="--verify-first-run"||a=="--verify-review-polish"||a=="--verify-task-lists"||a=="--verify-providers"||a=="--verify-review-links"||a=="--verify-sources"||a=="--verify-data"||a=="--verify-layout"||a=="--verify-icon-relaunch"||a=="--verify-fixture"||a=="--work-read"||a=="--work-apply");
        try
        {
            bool fixture=args.Length>=2&&(args[0]=="--fixture"||args[0]=="--verify-fixture");
            bool verify=fixture&&args[0]=="--verify-fixture";
            string profile=fixture?Path.GetFullPath(args[1]).TrimEnd(Path.DirectorySeparatorChar):AppData.Root;
            if(fixture)
            {
                string review=Path.Combine(AppData.Root,".dev","Review")+Path.DirectorySeparatorChar;
                if(!profile.StartsWith(review,StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("This launcher requires a profile under .dev/Review.");
                for(var directory=new DirectoryInfo(profile);directory!=null;directory=directory.Parent)
                    if((directory.Attributes&FileAttributes.ReparsePoint)!=0)
                        throw new IOException("The fixture path must not contain a junction or symbolic link.");
                args=args.Skip(2).ToArray();
            }
            else
            {
#if WPF_CANONICAL
                string executable=System.Windows.Forms.Application.ExecutablePath;
                if(!String.Equals(executable,Path.Combine(AppData.Root,"LocalDashboard.exe"),StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Run this app through the canonical Dashboard shortcut.");
#else
                throw new ArgumentException("Supply --fixture and an isolated profile under .dev/Review.");
#endif
            }
            var catalog=AppData.ReadEntries(File.ReadAllText(Path.Combine(profile,"catalog.json")),profile);
            string local=Path.Combine(profile,".local");
            var checklist=new ChecklistStore(ChecklistLocation.PathFor(profile),catalog);
            if(args.Length==1&&args[0]=="--work-read")
            {
                Console.WriteLine(AppData.Json.Serialize(checklist.Read()));
                return 0;
            }
            if(args.Length==2&&args[0]=="--work-apply")
            {
                string patch=Path.GetFullPath(args[1]);
                if(!patch.StartsWith(local+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||
                    !Path.GetExtension(patch).Equals(".json",StringComparison.OrdinalIgnoreCase)||new FileInfo(patch).Length>16384)
                    throw new IOException("Checklist patch must be a small JSON file in this profile.");
                Console.WriteLine(AppData.Json.Serialize(checklist.Upsert(AppData.Json.Deserialize<WorkPatch>(File.ReadAllText(patch)))));
                return 0;
            }
            if(fixture&&args.Length==1&&args[0]=="--verify-providers"){ProviderVerification.Run(profile);return 0;}
            if(fixture&&args.Length==1&&args[0]=="--verify-review-links"){ReviewLinkVerification.Run(profile);return 0;}
            if(fixture&&args.Length==1&&args[0]=="--verify-sources"){WorkSourceVerification.Run(profile);return 0;}
            if(fixture&&args.Length==1&&args[0]=="--verify-task-lists"){TaskListVerification.Run(profile);return 0;}
            if(fixture&&args.Length==1&&args[0]=="--verify-data"){DataVerification.Run(profile);return 0;}
            bool closeoutReview=fixture&&args.Length==1&&args[0]=="--verify-closeout";bool firstRunReview=fixture&&args.Length==1&&args[0]=="--verify-first-run";bool reviewPolish=fixture&&args.Length==1&&args[0]=="--verify-review-polish";
            bool layoutOnly=closeoutReview||firstRunReview||reviewPolish||(fixture&&args.Length==1&&args[0]=="--verify-layout");if(layoutOnly)args=new string[0];
            bool verifyIcon=args.Length==1&&args[0]=="--verify-icon-relaunch";if(verifyIcon)args=new string[0];
            bool verifyIconSave=fixture&&args.Length==1&&args[0]=="--verify-icon-save";if(verifyIconSave)args=new string[0];
            bool currentWork=args.Length==1&&args[0]=="--current-work";
            if(args.Length>1||verify&&args.Length!=0)throw new ArgumentException("Unsupported dashboard command.");
            if(args.Length==1&&!currentWork&&args[0]!="--startup")
            {
                var destination=AppData.Parse(args[0],catalog);
                if(destination!=null)
                {
                    AppData.Open(catalog,destination.Item1,destination.Item2,profile);
                    return 0;
                }
            }
            if(!diagnostic){
                applicationGate=new AppInstanceGate();
                if(!applicationGate.Acquire()){
                    applicationGate.RequestActivation();
                    string running=AppInstanceGate.OtherRunningVersion();
                    if(running!=null&&running!=typeof(WpfDashboard).Assembly.GetName().Version.ToString(4))
                        MessageBox.Show("Local Dashboard "+running+" is already running. Exit it before opening this version.","Local Dashboard",MessageBoxButton.OK,MessageBoxImage.Information);
                    return 0;
                }
                string older=AppInstanceGate.OtherRunningVersion(true);
                if(older!=null)throw new IOException("Local Dashboard "+older+" is already running. Use File > Exit in that app before opening this version.");
            }
            string identity;
            using(var sha=SHA256.Create())
                identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(profile.ToUpperInvariant()))).Replace("-","");
            string key=@"Local\LocalDashboard.Profile."+identity;
            using(var signal=new EventWaitHandle(false,EventResetMode.AutoReset,key+".Show"))
            using(var workSignal=new EventWaitHandle(false,EventResetMode.AutoReset,key+".CurrentWork"))
            {
                bool first;
                using(var instance=new Mutex(true,key,out first))
                {
                    if(!first){(currentWork?workSignal:signal).Set();return 0;}
                    if(!verify&&!layoutOnly&&!verifyIcon)DashboardData.Recover(profile);
                    if(!layoutOnly)ShellTaskbarBrand.InitializeProcess(profile,fixture);
                    var app=new Application{ShutdownMode=ShutdownMode.OnMainWindowClose};
                    EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler(ThemeWindow));
                    app.Resources.MergedDictionaries.Add(Load<ResourceDictionary>("ForgePresentation.xaml"));
                    bool firstSetup=!verify&&!layoutOnly&&!verifyIcon&&!verifyIconSave&&NeedsFirstSetup(profile);
                    var dashboard=new WpfDashboard(profile,fixture,!diagnostic||verifyIconSave);
                    if(layoutOnly){if(closeoutReview)dashboard.VerifyCloseout();else if(firstRunReview)dashboard.VerifyFirstRun();else if(reviewPolish)dashboard.VerifyReviewPolish();else dashboard.VerifyLayoutOnly();return 0;}
                    if(verifyIcon){dashboard.window.ShowActivated=false;dashboard.window.ShowInTaskbar=false;dashboard.window.Left=-32000;dashboard.window.Top=-32000;dashboard.window.Show();dashboard.VerifyNativeIcon();dashboard.Exit();return 0;}
                    if(verify)
                    {
                        dashboard.window.WindowStartupLocation=WindowStartupLocation.Manual;
                        dashboard.window.Left=-32000;
                        dashboard.window.Top=-32000;
                        dashboard.window.ShowActivated=false;
                        dashboard.window.ShowInTaskbar=false;
                        dashboard.window.Show();
                        dashboard.VerifyFixture();
                        return 0;
                    }
                    var registration=ThreadPool.RegisterWaitForSingleObject(signal,(state,timedOut)=>{
                        dashboard.window.Dispatcher.BeginInvoke(new Action(()=>{
                            dashboard.Restore();
                        }));
                    },null,Timeout.Infinite,false);
                    var workRegistration=ThreadPool.RegisterWaitForSingleObject(workSignal,(state,timedOut)=>{
                        dashboard.window.Dispatcher.BeginInvoke(new Action(()=>{dashboard.Select("work");dashboard.Restore();}));
                    },null,Timeout.Infinite,false);
                    var appRegistration=applicationGate==null?null:ThreadPool.RegisterWaitForSingleObject(applicationGate.Activate,(state,timedOut)=>{
                        dashboard.window.Dispatcher.BeginInvoke(new Action(dashboard.Restore));
                    },null,Timeout.Infinite,false);
                    if(currentWork)dashboard.Select("work");
                    if(verifyIconSave){EventHandler rendered=null;rendered=(sender,eventArgs)=>{dashboard.window.ContentRendered-=rendered;dashboard.window.Dispatcher.BeginInvoke(new Action(dashboard.VerifyVisibleIconSave));};dashboard.window.ContentRendered+=rendered;}
                    if(firstSetup){EventHandler setup=null;setup=(sender,eventArgs)=>{dashboard.window.ContentRendered-=setup;dashboard.window.Dispatcher.BeginInvoke(new Action(()=>dashboard.Protect(()=>dashboard.ShowSetup(true))));};dashboard.window.ContentRendered+=setup;}
                    dashboard.refreshTimer.Start();
                    try
                    {
                        app.MainWindow=dashboard.window;
                        if(dashboard.preferenceStore.Value.startMinimized)dashboard.tray.Visible=true;
                        else dashboard.window.Show();
                        return app.Run();
                    }
                    finally{registration.Unregister(null);workRegistration.Unregister(null);if(appRegistration!=null)appRegistration.Unregister(null);}
                }
            }
        }
        catch(Exception error)
        {
            if(diagnostic)Console.Error.WriteLine(error);
            else MessageBox.Show(error.Message,"Local Dashboard",MessageBoxButton.OK,MessageBoxImage.Warning);
            return 1;
        }
        finally{if(applicationGate!=null)applicationGate.Dispose();}
    }
}
