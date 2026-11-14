using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;

internal sealed class QuinnSummary {
    internal string Title,Status,Next,Group;
    internal static List<QuinnSummary> Parse(string text){
        var result=new List<QuinnSummary>();QuinnSummary item=null;string group="",field="";
        foreach(string raw in text.Replace("\r","").Split('\n')){
            string line=raw.Trim();var title=Regex.Match(line,@"^\d+\.\s+(.+)$");
            if(title.Success){item=new QuinnSummary{Title=title.Groups[1].Value,Group=group,Status="",Next=""};result.Add(item);field="";continue;}
            if(raw.Length>0&&!Char.IsWhiteSpace(raw[0])&&line.Length>0&&line==line.ToUpperInvariant()&&line.Any(Char.IsLetter)) {group=line;item=null;field="";continue;}
            if(item==null)continue;
            if(line.StartsWith("Status:",StringComparison.OrdinalIgnoreCase)){item.Status=line.Substring(7).Trim();field="status";}
            else if(line.StartsWith("Next:",StringComparison.OrdinalIgnoreCase)){item.Next=line.Substring(5).Trim();field="next";}
            else if(Regex.IsMatch(line,@"^[\w /-]+:"))field="";
            else if(raw.StartsWith(" ")&&line.Length>0){if(field=="status")item.Status+=" "+line;else if(field=="next")item.Next+=" "+line;}
        }return result;
    }
    internal static string Short(string value,int limit){if(String.IsNullOrWhiteSpace(value))return "Not supplied in the work list.";if(value.Length<=limit)return value;int cut=value.LastIndexOf(' ',limit);return value.Substring(0,cut>limit/2?cut:limit).TrimEnd()+"â€¦";}
}

internal sealed partial class WpfDashboard {
    // All application windows share the same native caption treatment.
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
    static void ThemeWindow(object sender,RoutedEventArgs args){
        var target=sender as Window;if(target==null||SystemParameters.HighContrast)return;
        var handle=new System.Windows.Interop.WindowInteropHelper(target).Handle;
        if(handle==IntPtr.Zero)return;
        try{int dark=DashboardTheme.Dark?1:0,border=DashboardTheme.Dark?0x383838:0xE0E0E0,caption=DashboardTheme.Dark?0x202020:0xF5F5F5,text=DashboardTheme.Dark?0xF2F2F2:0x202020;
            DwmSetWindowAttribute(handle,20,ref dark,4);DwmSetWindowAttribute(handle,34,ref border,4);
            DwmSetWindowAttribute(handle,35,ref caption,4);DwmSetWindowAttribute(handle,36,ref text,4);
        }catch(DllNotFoundException){}catch(EntryPointNotFoundException){}
    }
    static TextBlock WorkText(string text,bool title=false){var block=new TextBlock{Text=text,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,5),FontSize=title?16:14,FontWeight=title?FontWeights.SemiBold:FontWeights.Normal};block.SetResourceReference(TextBlock.ForegroundProperty,title?"ThemeF2F2F2":"ThemeB9B9B9");return block;}
    static void SetNavigationSelected(Button button,bool selected){
        System.Windows.Automation.AutomationProperties.SetItemStatus(button,selected?"Selected":"");
        System.Windows.Controls.Primitives.Selector.SetIsSelected(button,selected);
        button.SetResourceReference(Control.BackgroundProperty,selected?"Theme454545":"Theme383838");
        button.SetResourceReference(Control.ForegroundProperty,selected?"DashboardAccent":"ThemeF2F2F2");
    }
    static void StyleWorkspaceButton(Button button,bool active){
        button.SetResourceReference(Control.BackgroundProperty,active?"Theme454545":"Theme383838");
        ((TextBlock)((DockPanel)button.Content).Children[1]).FontWeight=active?FontWeights.SemiBold:FontWeights.Normal;
        System.Windows.Automation.AutomationProperties.SetItemStatus(button,active?"Current workspace":"");
    }
    static Border WorkRule(){var line=new Border{Height=1,Margin=new Thickness(0,5,0,10)};line.SetResourceReference(Border.BackgroundProperty,"Theme444444");return line;}
    Button HeaderAction(string text,string glyph,Action action,Button supplied=null){var button=supplied??new Button();button.Click+=(s,e)=>action();var group=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var image=GlyphImage(glyph);image.Margin=new Thickness(0,0,7,0);image.VerticalAlignment=VerticalAlignment.Center;group.Children.Add(image);group.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});button.Content=group;button.HorizontalAlignment=HorizontalAlignment.Left;button.VerticalContentAlignment=VerticalAlignment.Center;button.HorizontalContentAlignment=HorizontalAlignment.Center;button.Padding=new Thickness(11,0,11,0);button.Height=36;button.Margin=new Thickness(0,0,7,8);System.Windows.Automation.AutomationProperties.SetName(button,text);return button;}
    void FitToWorkArea(){
        var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var screen=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var presentation=PresentationSource.FromVisual(window);var transform=presentation==null?Matrix.Identity:presentation.CompositionTarget.TransformFromDevice;
        var start=transform.Transform(new Point(screen.Left,screen.Top));var end=transform.Transform(new Point(screen.Right,screen.Bottom));
        window.Width=Math.Max(window.MinWidth,Math.Min(window.Width,end.X-start.X));window.Height=Math.Max(window.MinHeight,Math.Min(window.Height,end.Y-start.Y));
        window.Left=Math.Max(start.X,Math.Min(Double.IsNaN(window.Left)?start.X:window.Left,end.X-window.Width));window.Top=Math.Max(start.Y,Math.Min(Double.IsNaN(window.Top)?start.Y:window.Top,end.Y-window.Height));
    }
    static string SectionLabel(string id,string label){return label;}
    Button WindowIconButton(string label,string glyph,Action action,Button supplied=null)
    {
        var button=supplied??new Button();
        button.SetResourceReference(FrameworkElement.StyleProperty,typeof(Button));
        button.Content=GlyphImage(glyph);
        button.Width=button.Height=36;
        button.Margin=new Thickness(6,0,0,0);
        button.Padding=new Thickness(0);
        button.ToolTip=label;
        System.Windows.Automation.AutomationProperties.SetName(button,label);
        button.Click+=(s,e)=>action();
        return button;
    }

    void UpdateHeaderActions(){
        var topMenu=Find<MenuItem>("TopMenu");
        topMenu.IsChecked=window.Topmost;
        ((Image)topMenu.Icon).Tag=window.Topmost?"pin-active":"pin";
        Panel panel=Compact?(Panel)Find<WrapPanel>("CompactToolbar"):Find<StackPanel>("FullWindowActions");
        foreach(Button button in panel.Children){
            if(Equals(button.Tag,"compact-toggle")){
                string label=Compact?"Expand":"Compact view";
                System.Windows.Automation.AutomationProperties.SetName(button,label);
                button.ToolTip=label;
                ((Image)button.Content).Tag=Compact?"expand":"compact";
            }
            bool selected=System.Windows.Automation.AutomationProperties.GetName(button)=="On top"&&window.Topmost;
            button.SetResourceReference(Control.ForegroundProperty,selected?"DashboardAccent":"ThemeF2F2F2");
            var pin=button as OnTopButton;
            if(pin!=null)
            {
                pin.Pressed=selected;
                ((Image)pin.Content).Tag=selected?"pin-active":"pin";
                pin.SetResourceReference(Control.BackgroundProperty,selected?"Theme454545":"Theme383838");
                pin.ToolTip=selected?"Always on top is on. Click to turn off.":"Always on top is off. Click to keep Dashboard above other windows.";
            }
        }
    }
    sealed class OnTopButton:Button
    {
        bool pressed;
        internal bool Pressed
        {
            get{return pressed;}
            set
            {
                if(pressed==value)return;
                var previous=pressed;
                pressed=value;
                var peer=System.Windows.Automation.Peers.UIElementAutomationPeer.FromElement(this);
                if(peer!=null)peer.RaisePropertyChangedEvent(System.Windows.Automation.TogglePatternIdentifiers.ToggleStateProperty,
                    previous?System.Windows.Automation.ToggleState.On:System.Windows.Automation.ToggleState.Off,
                    pressed?System.Windows.Automation.ToggleState.On:System.Windows.Automation.ToggleState.Off);
            }
        }

        protected override System.Windows.Automation.Peers.AutomationPeer OnCreateAutomationPeer(){return new OnTopPeer(this);}
        sealed class OnTopPeer:System.Windows.Automation.Peers.ButtonAutomationPeer,System.Windows.Automation.Provider.IToggleProvider
        {
            readonly OnTopButton button;
            internal OnTopPeer(OnTopButton owner):base(owner){button=owner;}
            public override object GetPattern(System.Windows.Automation.Peers.PatternInterface pattern)
            {
                return pattern==System.Windows.Automation.Peers.PatternInterface.Toggle?this:base.GetPattern(pattern);
            }
            public System.Windows.Automation.ToggleState ToggleState
            {
                get{return button.Pressed?System.Windows.Automation.ToggleState.On:System.Windows.Automation.ToggleState.Off;}
            }
            public void Toggle()
            {
                if(!IsEnabled())throw new System.Windows.Automation.ElementNotEnabledException();
                button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
        }
    }
    static Button WorkRefresh(Action action){var button=CompactAction(ActionButton("Refresh",action));button.Margin=new Thickness(0,0,0,8);return button;}
    void RenderChecklist(){
        ChecklistStore store;
        WorkDocument document;
        try{ChecklistLocation.RequireAvailable(profile);store=new ChecklistStore(ChecklistLocation.PathFor(profile),entries);document=store.Read();}catch(Exception error){UpdateTaskPromptButton(null,null);rows.Children.Add(WorkText("Checklist unavailable. "+error.Message));return;}
        string query=search.Text.Trim();
        var matchingItems=document.items.Where(i=>MatchesTaskSearch(query,i.title,i.details)).ToList();
        var tabs=new WrapPanel();
        foreach(bool done in new[]{false,true}){bool selected=done;var tab=SourceAction((done?"Done":"Current")+" ("+matchingItems.Count(i=>(i.state=="done")==done)+")",()=>{doneView=selected;RenderCompact(true);});SetNavigationSelected(tab,done==doneView);tabs.Children.Add(tab);}
        UpdateTaskPromptButton(-1,null);
        rows.Children.Add(tabs);
        var selectedItems=matchingItems.Where(i=>(i.state=="done")==doneView).ToList();
        if(selectedItems.Count==0){
            string message=query.Length>0?"No checklist items match your search.":
                doneView?"No completed checklist items.":
                document.items.Count==0?"Use Edit to add your first checklist item.":"No current checklist items. Completed items are under Done.";
            rows.Children.Add(WorkText(message));
        }
        foreach(var item in selectedItems){var row=new StackPanel{Tag=item.id};
            var content=new StackPanel{VerticalAlignment=VerticalAlignment.Center};
            var line=new DockPanel();var error=WorkText("");
            var check=new CheckBox{Content=WorkText(item.title,true),IsChecked=doneView,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,0,4)};check.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");System.Windows.Automation.AutomationProperties.SetName(check,(doneView?"Restore ":"Complete ")+item.title);
            RoutedEventHandler complete=(s,e)=>{try{store.Complete(document.revision,item.id,check.IsChecked==true);RenderCompact(true);}catch(Exception failure){RenderCompact(true);rows.Children.Add(WorkText(failure.Message));}};
            check.Checked+=complete;check.Unchecked+=complete;line.Children.Add(check);content.Children.Add(line);
            if(!String.IsNullOrWhiteSpace(item.details))content.Children.Add(WorkText(item.details));
            row.Children.Add(DashboardCard(content));rows.Children.Add(row);
        }
    }
}
