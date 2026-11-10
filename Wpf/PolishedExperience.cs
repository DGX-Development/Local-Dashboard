using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

internal sealed partial class WpfDashboard {
    const string PublisherWebsite="https://dgx.digitalguy.me/";
    const string SupportWebsite="https://dgx.digitalguy.me/contact.html";
    string lastSettingsPage="Appearance";
    string appearancePreviewTheme;
    bool agentUpdatesView;
    System.Drawing.Color SavedAccent(){var p=preferenceStore.Value;return DashboardAccent.Read((DashboardTheme.Dark?p.darkAccent:p.lightAccent)??p.accentColor);}
    string TaskLabel(bool agent){var p=preferenceStore.Value;var label=agent?p.agentTasksLabel:p.checklistLabel;return String.IsNullOrWhiteSpace(label)||(agent?String.Equals(label,"Agent Tasks",StringComparison.OrdinalIgnoreCase):new[]{"My Checklist","Checklist","Checklists"}.Any(x=>String.Equals(label,x,StringComparison.OrdinalIgnoreCase)))?(agent?"Agent tasks":"Checklists"):label;}
    string PersonalTaskLabel(){var label=preferenceStore.Value.personalTasksLabel;return String.IsNullOrWhiteSpace(label)?"Personal tasks":label;}
    static Button StandardAction(Button button){button.Height=36;button.MinWidth=88;button.Width=Double.NaN;button.Padding=new Thickness(12,0,12,0);button.Margin=new Thickness(8,0,0,0);button.HorizontalContentAlignment=HorizontalAlignment.Center;button.VerticalContentAlignment=VerticalAlignment.Center;return button;}
    static TextBox NamedField(string name,string text,int limit){var box=new TextBox{Text=text,MaxLength=limit};System.Windows.Automation.AutomationProperties.SetName(box,name);return box;}
    static string ValidTaskLabel(string value){value=(value??"").Trim();if(value.Length==0||value.Length>40||value.Any(Char.IsControl))throw new IOException("Use a task label of 1 to 40 characters.");return value;}
    void Settings(){Settings("Appearance");}
    void Settings(string initialPage){
        preferenceStore.Reload();var current=preferenceStore.Value;var original=DashboardAccent.Selected;
        var content=new DockPanel{Margin=new Thickness(28)};
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(footer,Dock.Bottom);content.Children.Add(footer);
        var navigation=new WrapPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(navigation,Dock.Top);content.Children.Add(navigation);
        var pages=new Grid();var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=pages};content.Children.Add(scroll);
        var dialog=Dialog("Local Dashboard settings",780,700,content);
        var appearance=new StackPanel{Margin=new Thickness(0,0,14,0)};
        appearance.Children.Add(new TextBlock{Text="Appearance",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        appearance.Children.Add(WorkText("Changes preview immediately. Save keeps them; Cancel restores your saved appearance."));
        var theme=new ComboBox{ItemsSource=new[]{"System","Light","Dark"},SelectedItem=current.theme??"Dark"};System.Windows.Automation.AutomationProperties.SetName(theme,"Appearance theme");appearance.Children.Add(theme);
        var previewCard=new StackPanel{Margin=new Thickness(18)};
        previewCard.Children.Add(WorkText("Appearance preview",true));
        var sample=new DockPanel{LastChildFill=false};var sampleIcon=new Image{Source=BrandVector(original),Width=32,Height=32,Margin=new Thickness(0,0,12,0)};sample.Children.Add(sampleIcon);
        var sampleButton=StandardAction(new Button{Content="Sample button",IsHitTestVisible=false,Focusable=false});sampleButton.SetResourceReference(Control.ForegroundProperty,"DashboardAccent");sample.Children.Add(sampleButton);previewCard.Children.Add(sample);
        var sampleText=WorkText("Selected items and focus use a readable shade of your accent.");sampleText.SetResourceReference(TextBlock.ForegroundProperty,"DashboardAccent");previewCard.Children.Add(sampleText);
        var card=new Border{CornerRadius=new CornerRadius(9),Child=previewCard,Margin=new Thickness(0,8,0,16)};card.SetResourceReference(Border.BackgroundProperty,"Surface");appearance.Children.Add(card);
        appearance.Children.Add(WorkText("Accent color",true));var presets=new WrapPanel();appearance.Children.Add(presets);
        var colorRow=new WrapPanel();var hex=NamedField("Accent hex color",DashboardAccent.Format(original),7);hex.Width=112;hex.FontFamily=new FontFamily("Consolas");hex.Margin=new Thickness(0,0,8,8);colorRow.Children.Add(hex);
        var reset=StandardAction(ActionButton("Reset",()=>{}));reset.Margin=new Thickness(0,0,0,8);colorRow.Children.Add(reset);appearance.Children.Add(colorRow);
        var feedback=WorkText("");appearance.Children.Add(feedback);
        var custom=new StackPanel();var hue=new Slider{Minimum=0,Maximum=359,Value=original.GetHue(),TickFrequency=1};var saturation=new Slider{Minimum=0,Maximum=100,Value=HsvSaturation(original)*100};var brightness=new Slider{Minimum=0,Maximum=100,Value=Math.Max(original.R,Math.Max(original.G,original.B))*100.0/255};
        var sliders=new[]{hue,saturation,brightness};var names=new[]{"Hue","Saturation","Brightness"};
        for(int i=0;i<sliders.Length;i++){custom.Children.Add(WorkText(names[i]));sliders[i].Margin=new Thickness(0,0,0,8);System.Windows.Automation.AutomationProperties.SetName(sliders[i],names[i]);custom.Children.Add(sliders[i]);}
        var spectrum=new LinearGradientBrush();string[] spectrumColors={"#FF0000","#FFFF00","#00FF00","#00FFFF","#0000FF","#FF00FF","#FF0000"};for(int i=0;i<spectrumColors.Length;i++)spectrum.GradientStops.Add(new GradientStop((Color)ColorConverter.ConvertFromString(spectrumColors[i]),i/6.0));custom.Children.Insert(0,new Border{Height=12,CornerRadius=new CornerRadius(6),Background=spectrum,Margin=new Thickness(0,8,0,8)});
        appearance.Children.Add(new Expander{Header="Custom color",Content=custom,Margin=new Thickness(0,0,0,16)});
        var agentLabel=NamedField("Agent task button label",TaskLabel(true),40);
        var personalLabel=NamedField("Personal task button label",PersonalTaskLabel(),40);
        var checklistLabel=NamedField("Checklist button label",TaskLabel(false),40);
        var windowOptions=new StackPanel{Margin=new Thickness(0,0,14,0)};windowOptions.Children.Add(new TextBlock{Text="Window & startup",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        var close=Option(windowOptions,"X button: keep running in the system tray",current.closeToTray);windowOptions.Children.Add(WorkText("On: X hides the window and keeps Dashboard running near the Windows clock. Off: X quits Dashboard."));
        var minimize=Option(windowOptions,"Minimize button: hide in the system tray",current.minimizeToTray);windowOptions.Children.Add(WorkText("On: minimize hides Dashboard near the Windows clock. Off: minimize keeps its button on the taskbar."));
        windowOptions.Children.Add(WorkText("Double-click the tray icon to return. To quit, choose File > Exit or right-click the tray icon and choose Exit."));
        var startup=Option(windowOptions,"Start Local Dashboard when I sign in to Windows",current.startWithWindows);if(fixture)windowOptions.Children.Add(WorkText("This review profile simulates startup settings; Windows sign-in is unchanged."));
        var startMin=Option(windowOptions,"Start minimized in the system tray",current.startMinimized);windowOptions.Children.Add(WorkText("Start minimized applies when you open Dashboard and when Windows starts it."));
        bool resetWindowSizes=false;var resetWindowNote=WorkText("Dashboard and Compact view remember separate positions and sizes. Other dialogs open at their standard sizes.");
        var resetSizes=StandardAction(ActionButton("Reset window sizes",()=>{}));
        resetSizes.HorizontalAlignment=HorizontalAlignment.Left;
        resetSizes.Click+=(s,e)=>{
            resetWindowSizes=true;
            resetWindowNote.Text="Click Save to reset both window sizes.";
            resetWindowNote.BringIntoView();
        };
        windowOptions.Children.Add(resetSizes);
        windowOptions.Children.Add(resetWindowNote);
        appearance.Children.Remove(card);DockPanel.SetDock(card,Dock.Top);content.Children.Insert(2,card);
        Action saveSource;var tasks=BuildWorkSource(out saveSource,false,new[]{agentLabel,personalLabel,checklistLabel});
        var pageNames=new[]{"Appearance","Window","Tasks"};var pageContents=new FrameworkElement[]{appearance,windowOptions,tasks};foreach(var page in pageContents)pages.Children.Add(page);
        bool updating=false;string dark=current.darkAccent,light=current.lightAccent;bool currentDark=DashboardTheme.Dark; if(currentDark)dark=DashboardAccent.Format(original);else light=DashboardAccent.Format(original);
        if(String.IsNullOrEmpty(dark))dark="#9AD9C1";if(String.IsNullOrEmpty(light))light="#237352";
        var applyTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
        Action apply=()=>{System.Drawing.Color color;if(!DashboardAccent.TryParse(hex.Text,out color))return;ApplyAccent(color);dialog.Resources["DashboardAccent"]=window.Resources["DashboardAccent"];dialog.Icon=DialogTitleIcon(dialog);sampleIcon.Source=BrandVector(color);ThemeWindow(dialog,null);};
        applyTimer.Tick+=(s,e)=>{applyTimer.Stop();try{apply();}catch(Exception error){feedback.Text="Could not preview appearance: "+error.Message;}};
        bool saved=false;
        var save=StandardAction(ActionButton("Save",()=>Protect(()=>{
            System.Drawing.Color color;if(!DashboardAccent.TryParse(hex.Text,out color))throw new IOException("Enter # followed by six hexadecimal digits.");
            string a=ValidTaskLabel(agentLabel.Text),c=ValidTaskLabel(checklistLabel.Text),p=ValidTaskLabel(personalLabel.Text);if(new[]{a,c,p}.Distinct(StringComparer.CurrentCultureIgnoreCase).Count()!=3)throw new IOException("Give the task sections different labels.");
            applyTimer.Stop();apply();saveSource();QueueTaskbarRefresh(true);
            preferenceStore.Save(new WindowPreferences{closeToTray=close.IsChecked==true,minimizeToTray=minimize.IsChecked==true,startWithWindows=startup.IsChecked==true,startMinimized=startMin.IsChecked==true,theme=theme.SelectedItem as string,accentColor=DashboardAccent.Format(color),darkAccent=dark,lightAccent=light,agentTasksLabel=a,checklistLabel=c,personalTasksLabel=p});
            saved=true;
            if(!nonWindowedVerification)dialog.DialogResult=true;
        })));footer.Children.Add(save);footer.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        Action refreshPalette=()=>{presets.Children.Clear();string[] labels={"Mint","Blue","Amber","Violet","Rose"};string[] colors=DashboardTheme.Dark?new[]{"#9AD9C1","#A8CEFF","#F5CC89","#DBB9FF","#F3A8BE"}:new[]{"#237352","#245FAD","#8A570C","#7040A0","#A33255"};for(int i=0;i<labels.Length;i++){string value=colors[i];var b=StandardAction(ActionButton(labels[i],()=>hex.Text=value));b.Margin=new Thickness(0,0,8,8);var group=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center};group.Children.Add(new Border{Tag="accent-dot",Width=14,Height=14,VerticalAlignment=VerticalAlignment.Center,CornerRadius=new CornerRadius(7),Background=new SolidColorBrush((Color)ColorConverter.ConvertFromString(value)),Margin=new Thickness(0,0,8,0)});group.Children.Add(new TextBlock{Text=labels[i],VerticalAlignment=VerticalAlignment.Center});b.Content=group;System.Windows.Automation.AutomationProperties.SetName(b,labels[i]+" accent");presets.Children.Add(b);}};
        hex.TextChanged+=(s,e)=>{System.Drawing.Color color;bool valid=DashboardAccent.TryParse(hex.Text,out color);save.IsEnabled=valid;feedback.Text=valid?"Window, tray and taskbar icons preview this exact color.":"Enter # followed by six hexadecimal digits.";if(!valid)return;if(currentDark)dark=hex.Text;else light=hex.Text;if(!updating){updating=true;hue.Value=color.GetHue();saturation.Value=HsvSaturation(color)*100;brightness.Value=Math.Max(color.R,Math.Max(color.G,color.B))*100.0/255;updating=false;}applyTimer.Stop();applyTimer.Start();};
        foreach(var slider in sliders)slider.ValueChanged+=(s,e)=>{if(updating)return;updating=true;hex.Text=DashboardAccent.Format(FromHsv(hue.Value,saturation.Value/100,brightness.Value/100));updating=false;};
        theme.SelectionChanged+=(s,e)=>{appearancePreviewTheme=theme.SelectedItem as string;DashboardTheme.Apply(appearancePreviewTheme);currentDark=DashboardTheme.Dark;hex.Text=currentDark?dark:light;refreshPalette();RefreshGlyphs();applyTimer.Stop();apply();ThemeWindow(window,null);};
        reset.Click+=(s,e)=>hex.Text=DashboardTheme.Dark?"#9AD9C1":"#237352";
        Action<int> selectPage=index=>{card.Visibility=index==0?Visibility.Visible:Visibility.Collapsed;lastSettingsPage=pageNames[index];for(int i=0;i<pageContents.Length;i++){pageContents[i].Visibility=i==index?Visibility.Visible:Visibility.Collapsed;var b=(Button)navigation.Children[i];SetNavigationSelected(b,i==index);}scroll.ScrollToHome();};
        for(int i=0;i<pageNames.Length;i++){int page=i;var b=StandardAction(ActionButton(pageNames[i],()=>selectPage(page)));b.Margin=new Thickness(0,0,8,8);b.Tag="settings-category";navigation.Children.Add(b);}
        if(initialPage=="Work lists & connections"||initialPage=="Connections"||initialPage=="Data")initialPage="Tasks";selectPage(Math.Max(0,Array.IndexOf(pageNames,initialPage)));refreshPalette();
        try{presentDialog(dialog);}finally{
            applyTimer.Stop();appearancePreviewTheme=null;DashboardTheme.Apply(preferenceStore.Value.theme);ApplyAccent(SavedAccent());RefreshGlyphs();ThemeWindow(window,null);if(section=="work")RenderCompact(true);
            // Ordinary Settings always opens Appearance; contextual links select their own page.
        }
        if(saved&&resetWindowSizes)ResetWindowSizes();
    }
    static double HsvSaturation(System.Drawing.Color c){double max=Math.Max(c.R,Math.Max(c.G,c.B)),min=Math.Min(c.R,Math.Min(c.G,c.B));return max==0?0:(max-min)/max;}
    static System.Drawing.Color FromHsv(double h,double s,double v){double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;double r=0,g=0,b=0;if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}return System.Drawing.Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));}
    static void AddLinkFocus(Hyperlink link){link.GotKeyboardFocus+=(s,e)=>link.SetResourceReference(TextElement.BackgroundProperty,"Theme454545");link.LostKeyboardFocus+=(s,e)=>link.ClearValue(TextElement.BackgroundProperty);}
    TextBlock WebsiteLink(string label,string address,bool underline=true){var link=new Hyperlink(new Run(label)){NavigateUri=new Uri(address)};if(!underline)link.TextDecorations=new TextDecorationCollection();AddLinkFocus(link);link.SetResourceReference(TextElement.ForegroundProperty,"DashboardAccent");link.RequestNavigate+=(s,e)=>{e.Handled=true;Protect(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address){UseShellExecute=true}));};System.Windows.Automation.AutomationProperties.SetName(link,"Open "+label);return new TextBlock(link){Margin=new Thickness(0,0,0,12)};}
    void ShowAbout(){
        var root=new DockPanel{Margin=new Thickness(28)};var close=StandardAction(new Button{Content="Close",IsCancel=true,IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
        var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var header=new DockPanel{Margin=new Thickness(0,0,0,24)};header.Children.Add(new Image{Source=window.Icon,Width=64,Height=64,Margin=new Thickness(0,0,18,0)});var identity=new StackPanel{VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(new TextBlock{Text="Local Dashboard",FontSize=25,FontWeight=FontWeights.SemiBold});identity.Children.Add(WorkText("Version "+typeof(WpfDashboard).Assembly.GetName().Version.ToString(3)));header.Children.Add(identity);body.Children.Add(header);
        var card=new StackPanel{Margin=new Thickness(18)};card.Children.Add(WorkText("Your projects and tasks, in one place. Open folders, documents and websites, and keep track of your tasks."));card.Children.Add(new TextBlock{Text="Developed by",Margin=new Thickness(0,16,0,4),FontWeight=FontWeights.SemiBold});card.Children.Add(PublisherLink());card.Children.Add(WebsiteLink("dgx.digitalguy.me",PublisherWebsite,false));var frame=new Border{CornerRadius=new CornerRadius(9),Child=card};frame.SetResourceReference(Border.BackgroundProperty,"Surface");body.Children.Add(frame);
        body.Children.Add(WorkText("Project and website icons belong to their respective owners. Interface icons: Microsoft Fluent System Icons (MIT)."));body.Children.Add(SourceAction("View icon license",ShowIconLicense));presentDialog(Dialog("About Local Dashboard",560,650,root));
    }
    static string ComposeIssueReport(string version,string doing,string expected,string happened){
        return "Local Dashboard "+version+"\n\nWhat were you doing?\n"+doing+"\n\nWhat did you expect to happen?\n"+expected+"\n\nWhat happened instead?\n"+happened;
    }
    void ShowReportIssue(){
        var root=new DockPanel{Margin=new Thickness(28)};
        var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
        var body=new StackPanel();body.Children.Add(WorkText("Report an issue",true));
        body.Children.Add(WorkText("Describe the problem, then copy your report into a message on the DGX Development contact page. Nothing is sent automatically."));
        string version=typeof(WpfDashboard).Assembly.GetName().Version.ToString(3);
        body.Children.Add(WorkText("Local Dashboard version: "+version));
        var answers=new List<TextBox>();
        foreach(string prompt in new[]{"What were you doing?","What did you expect to happen?","What happened instead?"}){
            var field=new TextBox{AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,Height=76,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,12)};
            System.Windows.Automation.AutomationProperties.SetName(field,prompt);
            var label=new Label{Content=prompt,Target=field,Padding=new Thickness(0,0,0,5)};label.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");body.Children.Add(label);body.Children.Add(field);answers.Add(field);
        }
        body.Children.Add(SourceAction("Copy report",()=>Protect(()=>Clipboard.SetText(ComposeIssueReport(version,answers[0].Text,answers[1].Text,answers[2].Text)))));
        body.Children.Add(WebsiteLink("Open DGX Development contact page",SupportWebsite));
        root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});presentDialog(Dialog("Report an issue",640,700,root));
    }
    static void WaitForAppearancePreview(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    void VerifyPolishBehavior(){
        string path=StatePath("window-preferences.json"),before=File.Exists(path)?File.ReadAllText(path):null;var original=DashboardAccent.Selected;string originalTheme=DashboardTheme.Preference;var presenter=presentDialog;
        try{
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-settings-behavior.png"),764,661);var theme=Visuals<ComboBox>(root).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Appearance theme");theme.SelectedItem="Light";if(DashboardTheme.Dark)throw new Exception("Theme did not preview immediately.");
                var input=Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color");input.Text="#123ABC";WaitForAppearancePreview();
                if(DashboardAccent.Format(DashboardAccent.Selected)!="#123ABC"||!shellBrand.RefreshKey.Contains("123ABC")||dialog.Icon!=window.Icon)throw new Exception("Live appearance or icon resource did not follow custom color.");
                var custom=Visuals<Expander>(root).Single(x=>Equals(x.Header,"Custom color"));custom.IsExpanded=true;var scrolling=Visuals<ScrollViewer>(root).First(v=>v.Content is Grid);scrolling.ScrollToVerticalOffset(280);Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-custom-color.png"),764,661);var hue=Visuals<Slider>(root).Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Hue");hue.Value=70;WaitForAppearancePreview();if(DashboardAccent.Format(DashboardAccent.Selected)=="#123ABC")throw new Exception("Hue slider did not update the live color.");custom.IsExpanded=false;
                foreach(string page in new[]{"Appearance","Window","Tasks"}){ClickIn(root,page);Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-light-"+page+".png"),764,661);foreach(var b in Visuals<Button>(root).Where(b=>Equals(b.Tag,"settings-category"))){var cp=Visuals<ContentPresenter>(b).First();var p=cp.TranslatePoint(new Point(),b);if(Math.Abs(p.X+cp.ActualWidth/2-b.ActualWidth/2)>1)throw new Exception("Settings category label drift: "+b.Content);}}
                dialog.Close();return false;
            };
            Settings("Appearance");if((File.Exists(path)?File.ReadAllText(path):null)!=before||DashboardAccent.Selected!=original||DashboardTheme.Preference!=originalTheme)throw new Exception("Cancel failed to restore saved appearance.");
            presentDialog=dialog=>{var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-settings-behavior.png"),764,661);ClickIn(root,"Tasks");Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Agent task button label").Text="Team tasks";Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist button label").Text="Personal list";Visuals<ComboBox>(root).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Appearance theme").SelectedItem="Light";Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color").Text="#345678";ClickIn(root,"Save");dialog.Close();return true;};
            Settings();var reopened=new WindowPreferenceStore(path,new MemoryStartupEntry());if(reopened.Value.agentTasksLabel!="Team tasks"||reopened.Value.checklistLabel!="Personal list"||reopened.Value.lightAccent!="#345678"||reopened.Value.theme!="Light")throw new Exception("Appearance or label Save did not persist.");
            Select("work");agentUpdatesView=true;RenderCompact(true);if(!Visuals<Button>(Find<StackPanel>("WorkNavigation")).Any(b=>Equals(b.Content,"Team tasks")))throw new Exception("Report view removed task navigation.");ClickIn(Find<StackPanel>("WorkNavigation"),"Personal list");if(agentUpdatesView||assistantWorkView)throw new Exception("Custom checklist label routes incorrectly.");
            var top=Find<StackPanel>("FullWindowActions").Children.OfType<Button>().Single(b=>Equals(b.Tag,"always-on-top"));bool oldTop=window.Topmost;top.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(window.Topmost==oldTop)throw new Exception("Pin did not toggle.");top.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(!(GlyphSource("settings") is DrawingImage))throw new Exception("Command glyph is still a bitmap.");
            var activate=activateFolder;var launch=launchFolder;try{int launched=0;activateFolder=p=>false;launchFolder=p=>launched++;RequestDataFolder();RequestDataFolder();if(launched!=1)throw new Exception("Repeated folder request launched duplicates.");activateFolder=p=>true;if(!RequestDataFolder()||launched!=1)throw new Exception("Existing Explorer window was not reused.");}finally{activateFolder=activate;launchFolder=launch;lastFolderRequest=DateTime.MinValue;}
            Console.WriteLine("Polish behavior passed: immediate theme/color/icon-resource preview, Cancel rollback, saved labels and theme accents, category centering, report navigation, pin toggle, vector glyphs and duplicate-folder request prevention. Explorer activation and Windows taskbar appearance still require native review.");
        }finally{if(before==null){if(File.Exists(path))File.Delete(path);}else DashboardData.Atomic(path,System.Text.Encoding.UTF8.GetBytes(before));preferenceStore.Reload();DashboardTheme.Apply(originalTheme);ApplyAccent(original);RefreshGlyphs();presentDialog=presenter;agentUpdatesView=false;assistantWorkView=true;}
    }
}
