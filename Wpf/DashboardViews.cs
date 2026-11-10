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
    bool exitRequested,doneView; bool assistantWorkView=true;
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
        shellBrand=new ShellTaskbarBrand(profile,fixture,System.Windows.Forms.Application.ExecutablePath);
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
            if(section=="work")RefreshDisplayedTasks(false);
        };
        DashboardTheme.Apply(preferenceStore.Value.theme);RefreshGlyphs();
        ApplyAccent(SavedAccent());
        if(refreshShell)UpdateTaskbarSafely(()=>shellBrand.RefreshShortcuts());
        tray=new System.Windows.Forms.NotifyIcon{Text="Local Dashboard",Icon=trayIcon};
        tray.DoubleClick+=(s,e)=>window.Dispatcher.BeginInvoke(new Action(Restore));
        var menu=new System.Windows.Forms.ContextMenuStrip{BackColor=System.Drawing.Color.FromArgb(43,43,43),ForeColor=System.Drawing.Color.FromArgb(242,242,242),Font=new System.Drawing.Font("Segoe UI",10f),Padding=new System.Windows.Forms.Padding(6),Renderer=new DashboardMenuRenderer()};
        menu.Items.Add("Show",null,(s,e)=>window.Dispatcher.BeginInvoke(new Action(Restore)));
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
    void Restore(){window.Show();window.WindowState=WindowState.Normal;tray.Visible=false;window.Activate();}
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
        catch(Exception error){if(verifying)throw;MessageBox.Show(window,error.Message,"Local Dashboard",MessageBoxButton.OK,MessageBoxImage.Warning);}
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
        rows.Children.Clear();WorkTabs();
        heading.Text=agentUpdatesView?"Agent updates":"Tasks";
        if(agentUpdatesView){var reports=new StackPanel();rows.Children.Add(reports);RenderReportsInto(reports,true);}else if(personalTasksView)RenderSavedTaskList(true);else if(assistantWorkView)RenderQuinnWork();else RenderChecklist();
    }
    void RefreshDisplayedTasks(bool manual)
    {
        if(!agentUpdatesView&&(assistantWorkView||personalTasksView))RefreshWorkSource(personalTasksView,manual);
        else RenderCompact(true);
    }
    void WorkTabs(){
        var header=Find<StackPanel>("WorkNavigation");header.Children.Clear();header.Visibility=Visibility.Visible;
        var modes=new WrapPanel{Margin=new Thickness(0,8,0,8)};
        for(int i=0;i<3;i++){int mode=i;string label=i==1?PersonalTaskLabel():TaskLabel(i==0);var button=SourceAction(label,()=>{agentUpdatesView=false;assistantWorkView=mode==0;personalTasksView=mode==1;RenderCompact(true);if(mode<2)RefreshWorkSource(personalTasksView,false);Find<ScrollViewer>("ContentScroll").ScrollToHome();});
            bool selected=!agentUpdatesView&&i==(personalTasksView?1:assistantWorkView?0:2);SetNavigationSelected(button,selected);modes.Children.Add(button);
        }
        modes.Children.Add(HeaderAction("Refresh","updates",()=>RefreshDisplayedTasks(true)));
        header.Children.Add(modes);
        int attention=AttentionCount();Find<MenuItem>("ReportsMenu").Visibility=attention>0?Visibility.Visible:Visibility.Collapsed;
        if(attention>0){var notice=SourceAction("Needs your attention ("+attention+")",()=>{agentUpdatesView=true;RenderCompact(true);});SetNavigationSelected(notice,agentUpdatesView);rows.Children.Add(notice);}
        header.Children.Add(WorkText(agentUpdatesView?"Progress saved by participating agents, including work ready for review or needing attention.":personalTasksView?"Your personal tasks. Edit them here.":assistantWorkView?"Work your agents maintain.":"Quick things to add and check off."));
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
        dialog.SourceInitialized+=(s,e)=>{CenterDialog(dialog);dialog.Icon=DialogTitleIcon(dialog);};
        dialog.DpiChanged+=(s,e)=>dialog.Icon=DialogTitleIcon(dialog);
        return dialog;
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
        button.Width=Double.NaN;button.MinWidth=72;
        button.Height=36;
        button.VerticalAlignment=VerticalAlignment.Center;
        button.Padding=new Thickness(12,0,12,0);
        button.Margin=new Thickness(8,0,0,0);
        return button;
    }

    static Button SidebarAction(Button button){
        CompactAction(button);button.Height=30;button.Padding=new Thickness(8,3,8,3);button.Tag="sidebar-small-action";return button;
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

    Func<Window,bool?> presentDialog=dialog=>dialog.ShowDialog();

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
        var copyPrompt=StandardAction(ActionButton("Copy setup prompt",()=>Protect(()=>Clipboard.SetText(DashboardHelp.AiSetupPrompt))));
        actions.Children.Add(copyPrompt);actions.Children.Add(close);
        var topics=new ListBox{Width=260,Margin=new Thickness(0,0,18,0),
            ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse("<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding Title}' TextWrapping='Wrap'/></DataTemplate>")};
        DockPanel.SetDock(topics,Dock.Left);panel.Children.Add(topics);
        var article=new TextBlock{TextWrapping=TextWrapping.Wrap};
        panel.Children.Add(new ScrollViewer{Content=article,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        topics.SelectionChanged+=(s,e)=>{
            var topic=topics.SelectedItem as DashboardHelp.Topic;
            article.Text=topic==null?"":topic.Title+"\n\n"+topic.Body;
            copyPrompt.Visibility=topic!=null&&topic.Title==DashboardHelp.AiSetupTitle?Visibility.Visible:Visibility.Collapsed;
        };
        Action refresh=()=>{
            topics.ItemsSource=DashboardHelp.Topics.Where(t=>(t.Title+" "+t.Body).IndexOf(filter.Text,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();
            if(topics.Items.Count>0)topics.SelectedIndex=0;
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
            image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.UriSource=new Uri(entry.iconPath);image.EndInit();image.Freeze();
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
