using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Interop;

internal sealed partial class WpfDashboard
{
    readonly HashSet<string> pins=new HashSet<string>(StringComparer.Ordinal);
    SidebarStore sidebarStore;
    SidebarSnapshot sidebarSnapshot;

    WindowPreferenceStore preferenceStore;
    System.Windows.Forms.NotifyIcon tray;
    System.Drawing.Icon trayIcon;
    ShellTaskbarBrand shellBrand;
    string lastReportView;
    bool exitRequested,doneView; int taskListView;
    bool compactView; Rect fullBounds; WindowState fullState;
    TaskbarRefreshQueue taskbarUpdates;
    Size compactSize=new Size(580,760);
    System.Windows.Threading.DispatcherTimer refreshTimer;


    string StatePath(string name){return Path.Combine(profile,".local",name);}
    void Configure()
    {
        sidebarStore=new SidebarStore(StatePath("sidebar-layout.json"));
        preferenceStore=new WindowPreferenceStore(StatePath("window-preferences.json"),fixture?(IStartupEntry)new MemoryStartupEntry():new DashboardStartup());
        LoadCompactBounds();
        LoadFullBounds();
        string pinFile=StatePath("pins.json");
        if(File.Exists(pinFile))pins.UnionWith(AppData.Json.Deserialize<string[]>(File.ReadAllText(pinFile)));
        shellBrand=new ShellTaskbarBrand(profile,fixture,System.Windows.Forms.Application.ExecutablePath,desktopReview);
        taskbarUpdates=new TaskbarRefreshQueue(
            action=>{window.Dispatcher.BeginInvoke(action,System.Windows.Threading.DispatcherPriority.ContextIdle);},
            ()=>refreshShell&&window.IsVisible&&window.ShowInTaskbar,
            ()=>shellBrand.RefreshKey,
            ()=>UpdateTaskbarSafely(()=>shellBrand.RefreshTaskbar(new WindowInteropHelper(window).Handle)));
        window.SourceInitialized+=(s,e)=>UpdateTaskbarSafely(()=>shellBrand.Apply(new WindowInteropHelper(window).Handle));
        window.Loaded+=(s,e)=>{if(!verifying){FitToWorkArea();if(loadedFullPlacement&&!Compact)window.WindowState=fullState;}QueueTaskbarRefresh();};
        Microsoft.Win32.UserPreferenceChangedEventHandler themeChanged=(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{DashboardTheme.Apply(appearancePreviewTheme??preferenceStore.Value.theme);ApplyAccent(appearancePreviewTheme==null?SavedAccent():DashboardAccent.Selected);RefreshGlyphs();foreach(Window open in Application.Current.Windows)ThemeWindow(open,null);}));Microsoft.Win32.SystemEvents.UserPreferenceChanged+=themeChanged;window.Closed+=(s,e)=>Microsoft.Win32.SystemEvents.UserPreferenceChanged-=themeChanged;
        window.Activated+=(s,e)=>{
            QueueTaskbarRefresh();
            if(section=="work"&&activeTaskPrompt==null)RefreshDisplayedTasks(false);
        };
        DashboardTheme.Apply(preferenceStore.Value.theme);RefreshGlyphs();
        ApplyAccent(SavedAccent());
        if(refreshShell)UpdateTaskbarSafely(()=>shellBrand.RefreshShortcuts());
        tray=new System.Windows.Forms.NotifyIcon{Text="Local Dashboard",Icon=trayIcon};
        tray.DoubleClick+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(Restore));
        Func<string,System.Drawing.Color> trayColor=key=>{
            var color=DashboardTheme.Resolve("#"+key);
            return System.Drawing.Color.FromArgb(color.A,color.R,color.G,color.B);
        };
        var menu=new System.Windows.Forms.ContextMenuStrip{ShowImageMargin=false,Font=new System.Drawing.Font("Segoe UI",10f),
            Padding=new System.Windows.Forms.Padding(6),Renderer=new DashboardMenuRenderer(trayColor)};
        menu.Opening+=(s,e)=>{
            menu.BackColor=trayColor("2B2B2B");
            menu.ForeColor=trayColor("F2F2F2");
        };
        menu.Items.Add("Open Dashboard",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{
            Select(DashboardSection());
            Restore();
        })));
        menu.Items.Add("Open Tasks",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(()=>{
            Select("work");
            Restore();
        })));
        menu.Items.Add("Exit",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(Exit)));
        tray.ContextMenuStrip=menu;
        window.Closing+=(s,e)=>{
            if(!exitRequested&&preferenceStore.Value.closeToTray){e.Cancel=true;Hide();}
            if(!e.Cancel)SaveWindowBounds();
            if(!e.Cancel&&shellBrand!=null)UpdateTaskbarSafely(()=>shellBrand.Clear(new WindowInteropHelper(window).Handle));
        };
        window.StateChanged+=(s,e)=>{if(window.WindowState==WindowState.Minimized&&preferenceStore.Value.minimizeToTray)Hide();};
        window.Closed+=(s,e)=>{refreshTimer.Stop();tray.Dispose();menu.Dispose();trayIcon.Dispose();shellBrand.Dispose();};
        refreshTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};
        refreshTimer.Tick+=(s,e)=>{if(reportsWindow!=null&&reportsWindow.IsVisible)RenderReportsInto(reportsRows,false);else if(window.IsVisible&&section=="reports")RenderCompact(false);};
    }

    bool Compact {get{return compactView;}}
    void Restore(){
        if(activeTaskPrompt!=null&&activeTaskPrompt.IsVisible){activeTaskPrompt.Activate();return;}
        window.Show();window.WindowState=WindowState.Normal;tray.Visible=false;window.Activate();
    }
    void Hide(){tray.Visible=true;window.Hide();}
    void Exit(){exitRequested=true;try{window.Close();}finally{exitRequested=false;}}
    void ApplyAccent(System.Drawing.Color color)
    {
        DashboardAccent.Selected=color;
        var textColor=DashboardTheme.AccentText(color); window.Resources["DashboardAccent"]=new SolidColorBrush(Color.FromRgb(textColor.R,textColor.G,textColor.B));
        var old=trayIcon;
        using(var image=DashboardBrand.Render(color,256))
        {
            using(var stream=new MemoryStream())
            {
                image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                stream.Position=0;
                var brand=new BitmapImage();
                brand.BeginInit();brand.CacheOption=BitmapCacheOption.OnLoad;brand.StreamSource=stream;brand.EndInit();brand.Freeze();
                window.Icon=brand;Find<Image>("Brand").Source=BrandVector(color);
            }
            using(var frame=DashboardBrand.Render(color,32))
            {
                IntPtr handle=frame.GetHicon();
                try{using(var native=System.Drawing.Icon.FromHandle(handle))trayIcon=(System.Drawing.Icon)native.Clone();}
                finally{DestroyIcon(handle);}
            }
        }
        shellBrand.Prepare(null,color);
        UpdateTaskbarSafely(()=>shellBrand.Apply(new WindowInteropHelper(window).Handle));
        QueueTaskbarRefresh();
        if(tray!=null)tray.Icon=trayIcon;
        if(old!=null)old.Dispose();
        foreach(Window owned in Application.Current.Windows.Cast<Window>().Where(w=>w.Owner==window)){owned.Icon=DialogTitleIcon(owned);owned.Resources["DashboardAccent"]=window.Resources["DashboardAccent"];ThemeWindow(owned,null);}
        if(entries!=null&&section!="work"&&section!="reports")Render();
    }
    void QueueTaskbarRefresh(bool force=false)
    {
        taskbarUpdates.Request(force);
    }
    bool UpdateTaskbarSafely(Action update){
        try{update();return true;}
        catch(Exception error){
            // A shell appearance failure must not prevent opening or closing the Dashboard.
            System.Diagnostics.Trace.TraceError("Taskbar update failed: "+error);
            try{File.WriteAllText(StatePath("taskbar-error.txt"),DateTimeOffset.UtcNow.ToString("o")+"\r\n"+error);}catch{}
            return false;
        }
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr handle);

    void Protect(Action action)
    {
        try{action();}
        catch(Exception error){if(verifying)throw;ShowNotice(window,"Local Dashboard",error.Message);}
    }

    static void NameControl(FrameworkElement control,string name,string id)
    {
        System.Windows.Automation.AutomationProperties.SetName(control,name);
        System.Windows.Automation.AutomationProperties.SetAutomationId(control,id);
    }

    static Button ActionButton(string name,Action action)
    {
        var button=new Button{Content=name};
        button.Click+=(s,e)=>action();
        return button;
    }

    void RenderCompact(bool force){
        if(!force&&section!="reports")return;
        if(section=="reports"){RenderReportsInto(rows,force);return;}
        var visible=VisibleTaskTabs();
        if(!visible.Contains(taskListView)){taskListView=visible[0];ClearSearch();}
        WorkTabs(visible);
        heading.Margin=new Thickness(0);
        heading.Text=taskListView<0?TaskLabel(false):TaskListLabel(taskListView);
        RenderTaskItems();
    }
    void RenderTaskItems(){
        rows.Children.Clear();
        if(taskListView>=0)RenderSavedTaskList(taskListView);else RenderChecklist();
    }
    static Border DashboardCard(UIElement content,string tag=null){
        var card=new Border{Tag=tag,Child=content,Padding=new Thickness(12,8,12,8),
            Margin=new Thickness(0,0,0,2),CornerRadius=new CornerRadius(6),MinHeight=56};
        card.SetResourceReference(Border.BackgroundProperty,"Surface");
        return card;
    }
    void ClearSearch(){
        clearingSearch=true;
        try{search.Clear();}finally{clearingSearch=false;}
    }
    static bool MatchesTaskSearch(string query,params string[] fields){
        return query.Length==0||fields.Any(field=>field!=null&&field.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0);
    }
    void RefreshDisplayedTasks(bool manual)
    {
        if(taskListView>=0)RefreshWorkSource(taskListView,manual);
        else RenderCompact(true);
    }
    bool HasTaskListFile(int list)
    {
        try{WorkSources.Read(profile,list);return true;}
        catch(TaskSourceNotSelectedException){return false;}
        catch(IOException){return true;}
        catch(UnauthorizedAccessException){return true;}
        catch(ArgumentException){return true;}
        catch(InvalidOperationException){return true;}
    }
    Panel TaskNavigationPanel(){return Compact?(Panel)Find<WrapPanel>("WorkNavigation"):Find<StackPanel>("TaskNavigation");}
    void WorkTabs(int[] visible){
        var modes=TaskNavigationPanel();
        Panel inactive=Compact?(Panel)Find<StackPanel>("TaskNavigation"):Find<WrapPanel>("WorkNavigation");
        inactive.Children.Clear();
        var existing=modes.Children.OfType<Button>().Where(button=>button.Tag is int).ToArray();
        if(existing.Select(button=>(int)button.Tag).SequenceEqual(visible)&&existing.All(button=>
            System.Windows.Automation.AutomationProperties.GetName(button)==((int)button.Tag<0?TaskLabel(false):TaskListLabel((int)button.Tag)))){
            foreach(var button in existing)SetNavigationSelected(button,(int)button.Tag==taskListView);
            return;
        }
        modes.Children.Clear();
        foreach(int list in visible)
        {
            string label=list<0?TaskLabel(false):TaskListLabel(list);
            Action select=()=>{
                taskListView=list;
                ClearSearch();
                RenderCompact(true);
                if(list>=0)RefreshWorkSource(list,false);
                Find<ScrollViewer>("ContentScroll").ScrollToHome();
            };
            var button=Compact?SourceAction(label,select):IconAction(label,list<0?"checklist":"document",select);
            if(!Compact){
                button.Margin=new Thickness(0,0,0,6);
                ((TextBlock)((DockPanel)button.Content).Children[1]).TextTrimming=TextTrimming.CharacterEllipsis;
            }
            button.Tag=list;
            NameControl(button,label,list<0?"checklists-tab":"task-list-"+(list+1)+"-tab");
            SetNavigationSelected(button,taskListView==list);
            modes.Children.Add(button);
        }
        var editActions=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
        editActions.Children.Add(SidebarAction(ActionButton("Edit",()=>Protect(EditTaskWorkspace))));
        modes.Children.Add(editActions);
    }

    static DockPanel NoticeContent(string title,string message,out WrapPanel actions)
    {
        var body=new DockPanel{Margin=new Thickness(24)};
        var heading=new TextBlock{Text=title,FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(heading,Dock.Top);body.Children.Add(heading);
        actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);
        body.Children.Add(new ScrollViewer{Content=WorkText(message),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        return body;
    }

    bool ShowNotice(Window owner,string title,string message,string confirm=null)
    {
        WrapPanel actions;
        var body=NoticeContent(title,message,out actions);
        var dialog=Dialog(title,500,250,body);
        if(!nonWindowedVerification)dialog.Owner=owner;
        bool accepted=false;
        if(confirm!=null)actions.Children.Add(StandardAction(ActionButton(confirm,()=>{
            accepted=true;
            if(!nonWindowedVerification)dialog.DialogResult=true;
        })));
        var dismiss=StandardAction(new Button{Content=confirm==null?"OK":"Cancel",IsDefault=true,IsCancel=true});
        actions.Children.Add(dismiss);
        dialog.Loaded+=(s,e)=>dismiss.Focus();
        presentDialog(dialog);
        return accepted;
    }

    static void ShowStartupNotice(string message)
    {
        var app=Application.Current;
        if(app==null){
            app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};
            app.Resources.MergedDictionaries.Add(Load<ResourceDictionary>("ForgePresentation.xaml"));
            DashboardTheme.Apply("System");
        }
        WrapPanel actions;
        var body=NoticeContent("Local Dashboard",message,out actions);
        var dialog=new Window{Title="Local Dashboard",Width=500,Height=250,MinWidth=380,MinHeight=220,
            WindowStartupLocation=WindowStartupLocation.CenterScreen,ShowInTaskbar=true,UseLayoutRounding=true,
            FontFamily=new FontFamily("Segoe UI"),FontSize=14,Content=body};
        dialog.SetResourceReference(Window.BackgroundProperty,"Background");
        dialog.SetResourceReference(Window.ForegroundProperty,"ThemeF2F2F2");
        var dismiss=StandardAction(new Button{Content="OK",IsDefault=true,IsCancel=true});
        actions.Children.Add(dismiss);
        dialog.SourceInitialized+=(s,e)=>{RemoveDialogMinimize(dialog);dialog.Icon=DialogTitleIcon(dialog);ThemeWindow(dialog,null);};
        dialog.Loaded+=(s,e)=>dismiss.Focus();
        ShowOwnedDialog(dialog);
    }

    Window Dialog(string title,double width,double height,UIElement content)
    {
        var dialog=new Window{Owner=nonWindowedVerification?null:window,Title=title,Width=width,Height=height,
            MinWidth=Math.Min(width,520),MinHeight=Math.Min(height,380),WindowStartupLocation=WindowStartupLocation.CenterOwner,
            ShowInTaskbar=false,UseLayoutRounding=true,FontFamily=window.FontFamily,FontSize=window.FontSize,Icon=DialogTitleIcon(window)};
        dialog.Resources["DashboardAccent"]=window.Resources["DashboardAccent"];
        dialog.SetResourceReference(Window.BackgroundProperty,"Background");
        var surface=new Border{Child=content};
        surface.SetResourceReference(Border.BackgroundProperty,"Background");
        dialog.Content=surface;
        dialog.SetResourceReference(Window.ForegroundProperty,"ThemeF2F2F2");
        dialog.SourceInitialized+=(s,e)=>{RemoveDialogMinimize(dialog);CenterDialog(dialog);dialog.Icon=DialogTitleIcon(dialog);};
        dialog.DpiChanged+=(s,e)=>dialog.Icon=DialogTitleIcon(dialog);
        return dialog;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongW")]
    static extern int DialogWindowStyle(IntPtr handle,int index);
    [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongW")]
    static extern int SetDialogWindowStyle(IntPtr handle,int index,int style);
    static void RemoveDialogMinimize(Window dialog)
    {
        const int styleIndex=-16, minimizeBox=0x00020000;
        var handle=new WindowInteropHelper(dialog).Handle;
        SetDialogWindowStyle(handle,styleIndex,DialogWindowStyle(handle,styleIndex)&~minimizeBox);
    }

    static ImageSource DialogTitleIcon(Window owner)
    {
        int pixels=(int)Math.Round(16*VisualTreeHelper.GetDpi(owner).DpiScaleX);
        using(var frame=DashboardBrand.Render(DashboardAccent.Selected,pixels))
            return BitmapSourceFor(frame);
    }

    void CenterDialog(Window dialog)
    {
        var handle=new WindowInteropHelper(window).Handle;
        var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var source=PresentationSource.FromVisual(window);
        var transform=source==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;
        var start=transform.Transform(new Point(area.Left,area.Top));var end=transform.Transform(new Point(area.Right,area.Bottom));
        dialog.MinWidth=Math.Min(dialog.MinWidth,end.X-start.X);dialog.MinHeight=Math.Min(dialog.MinHeight,end.Y-start.Y);
        dialog.Width=Math.Min(dialog.Width,end.X-start.X);dialog.Height=Math.Min(dialog.Height,end.Y-start.Y);
        var bounds=window.WindowState==WindowState.Maximized?new Rect(start,end):new Rect(window.Left,window.Top,window.ActualWidth,window.ActualHeight);
        dialog.WindowStartupLocation=WindowStartupLocation.Manual;
        dialog.Left=Math.Max(start.X,Math.Min(bounds.Left+(bounds.Width-dialog.Width)/2,end.X-dialog.Width));
        dialog.Top=Math.Max(start.Y,Math.Min(bounds.Top+(bounds.Height-dialog.Height)/2,end.Y-dialog.Height));
        ThemeWindow(dialog,null);
    }

    static CheckBox Option(Panel panel,string label,bool value)
    {
        var checkbox=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,8,0,10)};
        checkbox.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");
        panel.Children.Add(checkbox);
        return checkbox;
    }

    static Button CompactAction(Button button)
    {
        button.HorizontalContentAlignment=HorizontalAlignment.Center;
        button.VerticalContentAlignment=VerticalAlignment.Center;
        button.Width=Double.NaN;button.MinWidth=88;
        button.Height=36;
        button.VerticalAlignment=VerticalAlignment.Center;
        button.Padding=new Thickness(12,0,12,0);
        button.Margin=new Thickness(8,0,0,0);
        return button;
    }

    static Button SidebarAction(Button button){
        CompactAction(button);button.Height=30;button.MinWidth=72;button.Padding=new Thickness(8,3,8,3);button.Tag="sidebar-small-action";return button;
    }
    static readonly List<WeakReference> glyphImages=new List<WeakReference>();
    static readonly Dictionary<string,ImageSource> glyphSources=new Dictionary<string,ImageSource>();
    static ImageSource GlyphSource(string glyph){
        var brush=Application.Current.TryFindResource("ThemeF2F2F2") as SolidColorBrush;
        Color tint=brush==null?(DashboardTheme.Dark?Color.FromRgb(242,242,242):Color.FromRgb(32,32,32)):brush.Color;
        string key=tint.ToString()+":"+glyph;ImageSource source;if(glyphSources.TryGetValue(key,out source))return source;
        source=VectorGlyph(glyph,tint);glyphSources[key]=source;return source;
    }
    static void RefreshGlyphs(){foreach(var reference in glyphImages.ToArray()){var item=reference.Target as Image;if(item==null){glyphImages.Remove(reference);continue;}var binding=System.Windows.Data.BindingOperations.GetMultiBindingExpression(item,Image.SourceProperty);if(binding!=null)binding.UpdateTarget();}}
    static Image GlyphImage(string glyph){var item=FluentImage(glyph,24,24);glyphImages.Add(new WeakReference(item));return item;}
    static Button IconAction(string label,string glyph,Action action)
    {
        var button=ActionButton(label,action);
        var content=new DockPanel();
        var icon=GlyphImage(glyph);
        icon.Margin=new Thickness(0,0,12,0);
        content.Children.Add(icon);
        content.Children.Add(new TextBlock{Text=label,VerticalAlignment=VerticalAlignment.Center});
        button.Content=content;
        button.HorizontalContentAlignment=HorizontalAlignment.Stretch;
        button.Padding=new Thickness(10,0,10,0);
        System.Windows.Automation.AutomationProperties.SetName(button,label);
        return button;
    }

    Func<Window,bool?> presentDialog=ShowOwnedDialog;
    static bool? ShowOwnedDialog(Window dialog)
    {
        var owner=dialog.Owner;
        var previous=Keyboard.FocusedElement as UIElement;
        try{return dialog.ShowDialog();}
        finally
        {
            if(owner!=null&&owner.IsActive&&previous!=null&&previous.IsVisible&&previous.IsEnabled)
                Keyboard.Focus(previous);
        }
    }

    void ShowHelp(){ShowHelp(null,null);}

    void ShowHelp(string initialTopic,Window owner)
    {
        var panel=new DockPanel{Margin=new Thickness(28)};
        var filter=new TextBox();
        System.Windows.Automation.AutomationProperties.SetName(filter,"Search help");
        var label=new TextBlock{Text="Search help"};
        DockPanel.SetDock(label,Dock.Top);panel.Children.Add(label);
        DockPanel.SetDock(filter,Dock.Top);panel.Children.Add(filter);
        var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(actions,Dock.Bottom);panel.Children.Add(actions);
        actions.Children.Add(close);
        var topics=new ListBox{Width=260,Margin=new Thickness(0,0,18,0),
            ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Title}' TextWrapping='Wrap'/></DataTemplate>")};
        DockPanel.SetDock(topics,Dock.Left);panel.Children.Add(topics);
        var article=new TextBlock{TextWrapping=TextWrapping.Wrap};
        panel.Children.Add(new ScrollViewer{Content=article,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        topics.SelectionChanged+=(s,e)=>{
            var topic=topics.SelectedItem as DashboardHelp.Topic;
            article.Text=topic==null?"No help topics match your search.":topic.Title+"\n\n"+topic.Body;
        };
        Action refresh=()=>{
            topics.ItemsSource=DashboardHelp.Topics.Where(t=>(t.Title+" "+t.Body).IndexOf(filter.Text,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
            if(topics.Items.Count>0)topics.SelectedIndex=0;else article.Text="No help topics match your search.";
        };
        filter.TextChanged+=(s,e)=>refresh();
        refresh();
        if(initialTopic!=null)
            topics.SelectedItem=topics.Items.Cast<DashboardHelp.Topic>().FirstOrDefault(topic=>topic.Title==initialTopic);
        var dialog=Dialog("Help & user guide",900,620,panel);
        if(owner!=null&&!nonWindowedVerification)dialog.Owner=owner;
        presentDialog(dialog);
    }

    void TogglePin(string id)
    {
        var next=new HashSet<string>(pins,StringComparer.Ordinal);
        if(!next.Add(id))next.Remove(id);
        string file=StatePath("pins.json");
        Directory.CreateDirectory(Path.GetDirectoryName(file));
        string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            File.WriteAllText(temporary,AppData.Json.Serialize(next.ToArray()));
            if(File.Exists(file))File.Replace(temporary,file,null);
            else File.Move(temporary,file);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
        pins.Clear();
        pins.UnionWith(next);
        Render();
    }

    static BitmapImage BitmapSourceFor(System.Drawing.Image image)
    {
        using(var stream=new MemoryStream())
        {
            image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);stream.Position=0;
            var result=new BitmapImage();
            result.BeginInit();result.CacheOption=BitmapCacheOption.OnLoad;result.StreamSource=stream;result.EndInit();result.Freeze();
            return result;
        }
    }

    static ImageSource EntryIcon(Entry entry,string iconProfile=null)
    {
        if(HasDashboardGlyph(entry.iconGlyph))return GlyphSource(entry.iconGlyph);
        if(String.IsNullOrEmpty(entry.iconPath)&&entry.iconAsset=="local-dashboard.svg")
            using(var brand=DashboardBrand.Render(DashboardAccent.Selected,32))return BitmapSourceFor(brand);
        var origin=WebsiteIcons.Origin(entry);
        if(origin!=null)
        {
            var cached=iconProfile==null?null:WebsiteIcons.Cached(iconProfile,origin);
            return cached??GlyphSource("globe");
        }
        if(String.IsNullOrEmpty(entry.iconPath)||!File.Exists(entry.iconPath))
            return GlyphSource(entry.section=="documents"?"dev-guide":entry.section=="workflows"?"workflow":entry.section=="everyday"?"globe":"copy");
        try
        {
            var pixooLogo=Path.Combine("Pixoo-Weather","assets","logo.png");
            var image=new BitmapImage();
            if(Path.GetExtension(entry.iconPath).Equals(".ico",StringComparison.OrdinalIgnoreCase))
                using(var bitmap=DashboardForm.ReadIcon(entry.iconPath))return BitmapSourceFor(bitmap);
            using(var stream=File.OpenRead(entry.iconPath))
            {
                image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;
                image.StreamSource=stream;image.EndInit();image.Freeze();
            }
            // The wide Pixoo wordmark includes a standalone cloud at the left.
            if(entry.iconPath.EndsWith(Path.DirectorySeparatorChar+pixooLogo,StringComparison.OrdinalIgnoreCase)&&image.PixelWidth==1774&&image.PixelHeight==887)
            {
                var cloud=new CroppedBitmap(image,new Int32Rect(105,239,456,424));
                cloud.Freeze();
                return cloud;
            }
            return image;
        }
        catch(NotSupportedException){return null;}
        catch(IOException){return null;}
        catch(ArgumentException){return null;}
    }
}
