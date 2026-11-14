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
    System.Drawing.Color SavedAccent(){var p=preferenceStore.Value;return DashboardAccent.Read((DashboardTheme.Dark?p.darkAccent:p.lightAccent)??p.accentColor);}
    string TaskLabel(bool agent){
        if(agent)return TaskListLabel(0);
        string label=preferenceStore.Value.checklistLabel;
        return String.IsNullOrWhiteSpace(label)||new[]{"My Checklist","Checklist","Checklists"}.Any(value=>String.Equals(label,value,StringComparison.OrdinalIgnoreCase))?"Checklists":label;
    }
    string PersonalTaskLabel(){return TaskListLabel(1);}
    string TaskListLabel(int list){
        var preferences=preferenceStore.Value;
        string label=new[]{preferences.agentTasksLabel,preferences.personalTasksLabel,preferences.thirdTaskListLabel}[list];
        return String.IsNullOrWhiteSpace(label)?"Task list "+(list+1):label;
    }
    static Button StandardAction(Button button){button.Height=36;button.MinWidth=88;button.Width=Double.NaN;button.Padding=new Thickness(12,0,12,0);button.Margin=new Thickness(8,0,0,0);button.HorizontalContentAlignment=HorizontalAlignment.Center;button.VerticalContentAlignment=VerticalAlignment.Center;return button;}
    static TextBox NamedField(string name,string text,int limit){var box=new TextBox{Text=text,MaxLength=limit};System.Windows.Automation.AutomationProperties.SetName(box,name);return box;}
    static string ValidTaskLabel(string value){value=(value??"").Trim();if(value.Length==0||value.Length>40||value.Any(Char.IsControl))throw new IOException("Use a task label of 1 to 40 characters.");return value;}
    void Settings(){Settings("Appearance");}
    void Settings(string initialPage,int taskList=-1){
        preferenceStore.Reload();var current=preferenceStore.Value;var original=DashboardAccent.Selected;
        var content=new DockPanel{Margin=new Thickness(28)};
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(footer,Dock.Bottom);content.Children.Add(footer);
        var navigation=new WrapPanel{Margin=new Thickness(0,0,0,16)};DockPanel.SetDock(navigation,Dock.Top);content.Children.Add(navigation);
        var pages=new Grid();var scroll=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Content=pages};content.Children.Add(scroll);
        var dialog=Dialog("Local Dashboard settings",780,700,content);
        var appearance=new StackPanel{Margin=new Thickness(0,0,14,0)};
        appearance.Children.Add(new TextBlock{Text="Appearance",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        appearance.Children.Add(WorkText("Changes preview immediately. Save keeps them; Cancel restores your saved appearance."));
        var theme=new ComboBox{ItemsSource=new[]{"System","Light","Dark"},SelectedItem=current.theme??"Dark",Width=240,HorizontalAlignment=HorizontalAlignment.Left};System.Windows.Automation.AutomationProperties.SetName(theme,"Appearance theme");appearance.Children.Add(theme);
        var previewCard=new StackPanel{Margin=new Thickness(18,12,18,12)};
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
        var showAllLinks=Option(appearance,"Show All links in the sidebar",current.showAllLinks!=false);
        var customColor=new Expander{Header="Custom color",Content=custom};
        customColor.Expanded+=(s,e)=>{
            dialog.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>{
                if(customColor.IsExpanded&&appearance.Visibility==Visibility.Visible)scroll.ScrollToBottom();
            }));
        };
        appearance.Children.Add(customColor);
        var agentLabel=NamedField("Task list 1 label",TaskListLabel(0),40);
        var personalLabel=NamedField("Task list 2 label",TaskListLabel(1),40);
        var thirdLabel=NamedField("Task list 3 label",TaskListLabel(2),40);
        var checklistLabel=NamedField("Checklist button label",TaskLabel(false),40);
        var windowOptions=new StackPanel{Margin=new Thickness(0,0,14,0)};windowOptions.Children.Add(new TextBlock{Text="Window & startup",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        windowOptions.Children.Add(WorkText("Window buttons",true));
        var close=Option(windowOptions,"Close to the system tray",current.closeToTray);
        var minimize=Option(windowOptions,"Minimize to the system tray",current.minimizeToTray);
        windowOptions.Children.Add(WorkText("Use File > Exit to quit Dashboard."));
        windowOptions.Children.Add(WorkRule());
        windowOptions.Children.Add(WorkText("Startup",true));
        var startup=Option(windowOptions,"Start when I sign in to Windows",current.startWithWindows);
        if(fixture)windowOptions.Children.Add(WorkText("This review profile does not change Windows sign-in."));
        var startMin=Option(windowOptions,"Start minimized in the system tray",current.startMinimized);
        windowOptions.Children.Add(WorkRule());
        windowOptions.Children.Add(WorkText("Window sizes",true));
        bool resetWindowSizes=false;
        var resetWindowNote=WorkText("");
        resetWindowNote.Visibility=Visibility.Collapsed;
        var resetSizes=StandardAction(ActionButton("Reset window sizes",()=>{}));
        resetSizes.HorizontalAlignment=HorizontalAlignment.Left;
        resetSizes.Margin=new Thickness(0,8,0,0);
        resetSizes.Click+=(s,e)=>{
            resetWindowSizes=true;
            resetWindowNote.Text="Click Save to reset both window sizes.";
            resetWindowNote.Visibility=Visibility.Visible;
            resetWindowNote.BringIntoView();
        };
        windowOptions.Children.Add(resetSizes);
        windowOptions.Children.Add(resetWindowNote);
        appearance.Children.Remove(card);DockPanel.SetDock(card,Dock.Top);content.Children.Insert(2,card);

        Action saveSource;var tasks=BuildWorkSource(out saveSource,false,new[]{agentLabel,personalLabel,thirdLabel,checklistLabel},taskList);
        tasks.Children.Add(WorkRule());
        var agentPrompts=Option(tasks,"Show agent edit prompts in Tasks",current.showAgentPrompts==true);
        var pageNames=new[]{"Appearance","Window","Tasks"};var pageContents=new FrameworkElement[]{appearance,windowOptions,tasks};foreach(var page in pageContents)pages.Children.Add(page);
        bool updating=false;string dark=current.darkAccent,light=current.lightAccent;bool currentDark=DashboardTheme.Dark; if(currentDark)dark=DashboardAccent.Format(original);else light=DashboardAccent.Format(original);
        if(String.IsNullOrEmpty(dark))dark="#9AD9C1";if(String.IsNullOrEmpty(light))light="#237352";
        var applyTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(120)};
        Action apply=()=>{System.Drawing.Color color;if(!DashboardAccent.TryParse(hex.Text,out color))return;ApplyAccent(color);dialog.Resources["DashboardAccent"]=window.Resources["DashboardAccent"];dialog.Icon=DialogTitleIcon(dialog);sampleIcon.Source=BrandVector(color);ThemeWindow(dialog,null);};
        applyTimer.Tick+=(s,e)=>{applyTimer.Stop();try{apply();}catch(Exception error){feedback.Text="Could not preview appearance: "+error.Message;}};
        bool saved=false;
        var save=StandardAction(ActionButton("Save",()=>Protect(()=>{
            System.Drawing.Color color;if(!DashboardAccent.TryParse(hex.Text,out color))throw new IOException("Enter # followed by six hexadecimal digits.");
            string a=ValidTaskLabel(agentLabel.Text),c=ValidTaskLabel(checklistLabel.Text),p=ValidTaskLabel(personalLabel.Text),third=ValidTaskLabel(thirdLabel.Text);if(new[]{a,c,p,third}.Distinct(StringComparer.CurrentCultureIgnoreCase).Count()!=4)throw new IOException("Give the task sections different labels.");
            applyTimer.Stop();apply();saveSource();QueueTaskbarRefresh(true);
            preferenceStore.Save(new WindowPreferences{closeToTray=close.IsChecked==true,minimizeToTray=minimize.IsChecked==true,startWithWindows=startup.IsChecked==true,startMinimized=startMin.IsChecked==true,theme=theme.SelectedItem as string,accentColor=DashboardAccent.Format(color),darkAccent=dark,lightAccent=light,agentTasksLabel=a,checklistLabel=c,personalTasksLabel=p,thirdTaskListLabel=third,showAllLinks=showAllLinks.IsChecked==true,showAgentPrompts=agentPrompts.IsChecked==true});
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
        if(saved&&current.showAllLinks!=preferenceStore.Value.showAllLinks)Reload();
        if(saved&&resetWindowSizes)ResetWindowSizes();
    }
    static double HsvSaturation(System.Drawing.Color c){double max=Math.Max(c.R,Math.Max(c.G,c.B)),min=Math.Min(c.R,Math.Min(c.G,c.B));return max==0?0:(max-min)/max;}
    static System.Drawing.Color FromHsv(double h,double s,double v){double c=v*s,x=c*(1-Math.Abs(h/60%2-1)),m=v-c;double r=0,g=0,b=0;if(h<60){r=c;g=x;}else if(h<120){r=x;g=c;}else if(h<180){g=c;b=x;}else if(h<240){g=x;b=c;}else if(h<300){r=x;b=c;}else{r=c;b=x;}return System.Drawing.Color.FromArgb((int)Math.Round((r+m)*255),(int)Math.Round((g+m)*255),(int)Math.Round((b+m)*255));}
    static void AddLinkFocus(Hyperlink link){link.GotKeyboardFocus+=(s,e)=>link.SetResourceReference(TextElement.BackgroundProperty,"Theme454545");link.LostKeyboardFocus+=(s,e)=>link.ClearValue(TextElement.BackgroundProperty);}
    TextBlock WebsiteLink(string label,string address,bool underline=true){var link=new Hyperlink(new Run(label)){NavigateUri=new Uri(address)};if(!underline)link.TextDecorations=new TextDecorationCollection();AddLinkFocus(link);link.SetResourceReference(TextElement.ForegroundProperty,"DashboardAccent");link.RequestNavigate+=(s,e)=>{e.Handled=true;Protect(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(address){UseShellExecute=true}));};System.Windows.Automation.AutomationProperties.SetName(link,"Open "+label);return new TextBlock(link){Margin=new Thickness(0,0,0,12)};}
    void ShowAbout(){
        var root=new DockPanel{Margin=new Thickness(28)};var close=StandardAction(new Button{Content="Close",IsCancel=true,IsDefault=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);
        var body=new StackPanel();root.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var header=new DockPanel{Margin=new Thickness(0,0,0,24)};header.Children.Add(new Image{Source=window.Icon,Width=64,Height=64,Margin=new Thickness(0,0,18,0)});var identity=new StackPanel{VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(new TextBlock{Text="Local Dashboard",FontSize=25,FontWeight=FontWeights.SemiBold});identity.Children.Add(WorkText("Version "+DisplayVersion()));header.Children.Add(identity);body.Children.Add(header);
        var card=new StackPanel{Margin=new Thickness(18)};card.Children.Add(WorkText("Your projects and tasks, in one place. Open folders, documents and websites, and keep track of your tasks."));card.Children.Add(new TextBlock{Text="Developed by",Margin=new Thickness(0,16,0,4),FontWeight=FontWeights.SemiBold});card.Children.Add(PublisherLink());card.Children.Add(WebsiteLink("dgx.digitalguy.me",PublisherWebsite,false));var frame=new Border{CornerRadius=new CornerRadius(9),Child=card};frame.SetResourceReference(Border.BackgroundProperty,"Surface");body.Children.Add(frame);
        var credit=WorkText("Project and website icons belong to their respective owners. Interface icons: Microsoft Fluent System Icons (MIT).");
        credit.Margin=new Thickness(0,8,0,4);body.Children.Add(credit);
        var license=SourceAction("View icon license",ShowIconLicense);license.Margin=new Thickness(0,8,0,0);body.Children.Add(license);
        var dialog=Dialog("About Local Dashboard",560,520,root);
        ((UIElement)dialog.Content).Measure(new Size(544,Double.PositiveInfinity));
        dialog.Height=Math.Ceiling(((UIElement)dialog.Content).DesiredSize.Height)+52;
        presentDialog(dialog);
    }
    static string DisplayVersion(){
        var version=typeof(WpfDashboard).Assembly.GetName().Version;
        return version.ToString(version.Revision==0?3:4);
    }
    async void CheckForUpdates(object sender,RoutedEventArgs e){
        var command=(MenuItem)sender;
        command.IsEnabled=false;
        const string releases="https://github.com/DGX-Development/Local-Dashboard/releases";
        try{
            using(var client=new System.Net.Http.HttpClient{Timeout=TimeSpan.FromSeconds(10)}){
                client.DefaultRequestHeaders.UserAgent.ParseAdd("LocalDashboard/"+DisplayVersion());
                string json=await client.GetStringAsync("https://api.github.com/repos/DGX-Development/Local-Dashboard/releases/latest");
                var release=AppData.Json.DeserializeObject(json) as Dictionary<string,object>;
                object value;
                string tag=release!=null&&release.TryGetValue("tag_name",out value)?value as string:null;
                Version latest;
                if(String.IsNullOrEmpty(tag)||!System.Text.RegularExpressions.Regex.IsMatch(tag,@"^v?[0-9]+\.[0-9]+\.[0-9]+$")||
                    !Version.TryParse(tag.TrimStart('v'),out latest))throw new IOException("Unexpected release version.");
                var installed=typeof(WpfDashboard).Assembly.GetName().Version;
                var stableBase=new Version(installed.Major,installed.Minor,installed.Build);
                if(latest>stableBase){
                    if(DataConfirmation("Check for updates","Update available","Local Dashboard "+latest+" is available.","View release"))
                        Protect(()=>System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(releases+"/tag/"+Uri.EscapeDataString(tag)){UseShellExecute=true}));
                }else if(stableBase>latest)DataConfirmation("Check for updates","You're running a newer version",
                    "Installed: "+DisplayVersion()+".\nLatest published stable: "+latest+".\nNo update is needed.","Close");
                else DataConfirmation("Check for updates","You're up to date","Latest stable version: "+latest+".","Close");
            }
        }catch(Exception){
            DataConfirmation("Check for updates","Couldn't check for updates","GitHub's release information is unavailable. Try again in a moment.","Close");
        }finally{command.IsEnabled=true;}
    }
    const string IssueWebsite="https://github.com/DGX-Development/Local-Dashboard/issues/new";
    void ShowReportIssue(Action<string> openPage=null){
        if(ShowNotice(window,"Report an issue",
            "Open Local Dashboard's GitHub issue page in your browser. A GitHub account is required to submit an issue. Local Dashboard will stay open.","Open GitHub"))
            Protect(()=>{
                if(openPage!=null)openPage(IssueWebsite);
                else System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(IssueWebsite){UseShellExecute=true});
            });
    }
    static void WaitForAppearancePreview(){var frame=new DispatcherFrame();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(180)};timer.Tick+=(s,e)=>{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    void VerifyPolishBehavior(){
        string path=StatePath("window-preferences.json"),before=File.Exists(path)?File.ReadAllText(path):null;var original=DashboardAccent.Selected;string originalTheme=DashboardTheme.Preference;var presenter=presentDialog;
        try{
            presentDialog=dialog=>{
                var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-settings-behavior.png"),764,661);var theme=Visuals<ComboBox>(root).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Appearance theme");theme.SelectedItem="Light";if(DashboardTheme.Dark)throw new Exception("Theme did not preview immediately.");
                var input=Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color");input.Text="#123ABC";WaitForAppearancePreview();
                if(DashboardAccent.Format(DashboardAccent.Selected)!="#123ABC"||dialog.Icon==null||DashboardData.Hash(File.ReadAllBytes(shellBrand.IconResource.Substring(0,shellBrand.IconResource.Length-2)))!=DashboardData.Hash(DashboardBrand.IconBytes(DashboardAccent.Selected)))throw new Exception("Live appearance or icon resource did not follow custom color.");
                var custom=Visuals<Expander>(root).Single(x=>Equals(x.Header,"Custom color"));custom.IsExpanded=true;var scrolling=Visuals<ScrollViewer>(root).First(v=>v.Content is Grid);scrolling.ScrollToVerticalOffset(280);Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-custom-color.png"),764,661);var hue=Visuals<Slider>(root).Single(x=>System.Windows.Automation.AutomationProperties.GetName(x)=="Hue");hue.Value=70;WaitForAppearancePreview();if(DashboardAccent.Format(DashboardAccent.Selected)=="#123ABC")throw new Exception("Hue slider did not update the live color.");custom.IsExpanded=false;
                foreach(string page in new[]{"Appearance","Window","Tasks"}){ClickIn(root,page);Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-light-"+page+".png"),764,661);foreach(var b in Visuals<Button>(root).Where(b=>Equals(b.Tag,"settings-category"))){var cp=Visuals<ContentPresenter>(b).First();var p=cp.TranslatePoint(new Point(),b);if(Math.Abs(p.X+cp.ActualWidth/2-b.ActualWidth/2)>1)throw new Exception("Settings category label drift: "+b.Content);}}
                dialog.Close();return false;
            };
            Settings("Appearance");if((File.Exists(path)?File.ReadAllText(path):null)!=before||DashboardAccent.Selected!=original||DashboardTheme.Preference!=originalTheme)throw new Exception("Cancel failed to restore saved appearance.");
            presentDialog=dialog=>{var root=dialog.Content as DependencyObject;Capture(dialog.Content as UIElement,Path.Combine(profile,"polish-settings-behavior.png"),764,661);ClickIn(root,"Tasks");Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Task list 1 label").Text="Team tasks";Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Checklist button label").Text="Personal list";Visuals<ComboBox>(root).Single(c=>System.Windows.Automation.AutomationProperties.GetName(c)=="Appearance theme").SelectedItem="Light";Visuals<TextBox>(root).Single(t=>System.Windows.Automation.AutomationProperties.GetName(t)=="Accent hex color").Text="#345678";ClickIn(root,"Save");dialog.Close();return true;};
            Settings();var reopened=new WindowPreferenceStore(path,new MemoryStartupEntry());if(reopened.Value.agentTasksLabel!="Team tasks"||reopened.Value.checklistLabel!="Personal list"||reopened.Value.lightAccent!="#345678"||reopened.Value.theme!="Light")throw new Exception("Appearance or label Save did not persist.");
            Select("work");RenderCompact(true);if(!TaskNavigationPanel().Children.OfType<Button>().Any(b=>System.Windows.Automation.AutomationProperties.GetName(b)=="Team tasks"))throw new Exception("Task navigation did not use the saved list label.");ClickWork("Personal list");if(taskListView!=-1)throw new Exception("Custom checklist label routes incorrectly.");
            var top=Find<StackPanel>("FullWindowActions").Children.OfType<Button>().Single(b=>Equals(b.Tag,"always-on-top"));bool oldTop=window.Topmost;top.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));if(window.Topmost==oldTop)throw new Exception("Pin did not toggle.");top.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(!(GlyphSource("settings") is DrawingImage))throw new Exception("Command glyph is still a bitmap.");
            var activate=activateFolder;var launch=launchFolder;try{int launched=0;activateFolder=p=>false;launchFolder=p=>launched++;RequestDataFolder();RequestDataFolder();if(launched!=1)throw new Exception("Repeated folder request launched duplicates.");activateFolder=p=>true;if(!RequestDataFolder()||launched!=1)throw new Exception("Existing Explorer window was not reused.");}finally{activateFolder=activate;launchFolder=launch;lastFolderRequest=DateTime.MinValue;}
            Console.WriteLine("Polish behavior passed: immediate theme/color/icon-resource preview, Cancel rollback, saved labels and theme accents, category centering, task navigation, pin toggle, vector glyphs and duplicate-folder request prevention. Explorer activation and Windows taskbar appearance still require native review.");
        }finally{if(before==null){if(File.Exists(path))File.Delete(path);}else DashboardData.Atomic(path,System.Text.Encoding.UTF8.GetBytes(before));preferenceStore.Reload();DashboardTheme.Apply(originalTheme);ApplyAccent(original);RefreshGlyphs();presentDialog=presenter;taskListView=0;}
    }
}
