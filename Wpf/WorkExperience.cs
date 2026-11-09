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
    // Same native caption colors used by BroadcastForge WindowTheme (Dialogs.cs).
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
        System.Windows.Controls.Primitives.Selector.SetIsSelected(button,selected);
        button.SetResourceReference(Control.BackgroundProperty,selected?"Theme454545":"Theme383838");
        button.SetResourceReference(Control.ForegroundProperty,selected?"DashboardAccent":"ThemeF2F2F2");
    }
    static Border WorkRule(){var line=new Border{Height=1,Margin=new Thickness(0,5,0,10)};line.SetResourceReference(Border.BackgroundProperty,"Theme444444");return line;}
    Button HeaderAction(string text,string glyph,Action action){var button=ActionButton(text,action);var group=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};var image=GlyphImage(glyph);image.Margin=new Thickness(0,0,7,0);image.VerticalAlignment=VerticalAlignment.Center;group.Children.Add(image);group.Children.Add(new TextBlock{Text=text,VerticalAlignment=VerticalAlignment.Center});button.Content=group;button.HorizontalAlignment=HorizontalAlignment.Left;button.VerticalContentAlignment=VerticalAlignment.Center;button.HorizontalContentAlignment=HorizontalAlignment.Center;button.Padding=new Thickness(11,0,11,0);button.Height=36;button.Margin=new Thickness(0,0,7,8);System.Windows.Automation.AutomationProperties.SetName(button,text);return button;}
    void FitToWorkArea(){
        var handle=new System.Windows.Interop.WindowInteropHelper(window).Handle;
        var screen=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
        var presentation=PresentationSource.FromVisual(window);var transform=presentation==null?Matrix.Identity:presentation.CompositionTarget.TransformFromDevice;
        var start=transform.Transform(new Point(screen.Left,screen.Top));var end=transform.Transform(new Point(screen.Right,screen.Bottom));
        window.Width=Math.Max(window.MinWidth,Math.Min(window.Width,end.X-start.X));window.Height=Math.Max(window.MinHeight,Math.Min(window.Height,end.Y-start.Y));
        window.Left=Math.Max(start.X,Math.Min(Double.IsNaN(window.Left)?start.X:window.Left,end.X-window.Width));window.Top=Math.Max(start.Y,Math.Min(Double.IsNaN(window.Top)?start.Y:window.Top,end.Y-window.Height));
    }
    static string SectionLabel(string id,string label){return label;}
    void UpdateHeaderActions(){
        var panel=Find<WrapPanel>("CompactToolbar");
        foreach(Button button in panel.Children){
            if(Equals(button.Tag,"work-refresh"))button.Visibility=section=="work"?Visibility.Visible:Visibility.Collapsed;
            if(Equals(button.Tag,"view-toggle")){
                button.Visibility=Compact?Visibility.Collapsed:Visibility.Visible;
                string label=section=="work"?"Dashboard":"Tasks";
                System.Windows.Automation.AutomationProperties.SetName(button,label);
                var group=(StackPanel)button.Content;group.Children.OfType<TextBlock>().Single().Text=label;
                var icon=group.Children.OfType<Image>().Single();icon.Tag=section=="work"?"dashboard":"checklist";
            }
            if(Equals(button.Tag,"compact-toggle")){
                string label=Compact?"Expand":"Compact view";
                System.Windows.Automation.AutomationProperties.SetName(button,label);
                ((StackPanel)button.Content).Children.OfType<TextBlock>().Single().Text=label;
                ((StackPanel)button.Content).Children.OfType<Image>().Single().Tag=Compact?"expand":"compact";
            }
            bool selected=System.Windows.Automation.AutomationProperties.GetName(button)=="On top"&&window.Topmost;
            button.SetResourceReference(Control.ForegroundProperty,selected?"DashboardAccent":"ThemeF2F2F2");
            if(Equals(button.Tag,"always-on-top")){button.SetResourceReference(Control.BackgroundProperty,selected?"Theme454545":"Theme383838");button.ToolTip=selected?"Always on top is on. Click to turn off.":"Keep Dashboard above other windows.";}
        }
    }
    static Button WorkRefresh(Action action){var button=CompactAction(ActionButton("Refresh",action));button.Margin=new Thickness(0,0,0,8);return button;}
    void RenderQuinnWork(){RenderSavedTaskList(false);}
    void RenderChecklist(){
        var store=new ChecklistStore(ChecklistLocation.PathFor(profile),entries);
        WorkDocument document;
        try{ChecklistLocation.RequireAvailable(profile);document=store.Read();}catch(Exception error){rows.Children.Add(WorkText("Checklist unavailable. "+error.Message));return;}
        var tabs=new WrapPanel();
        foreach(bool done in new[]{false,true}){bool selected=done;var tab=SourceAction((done?"Done":"Current")+" ("+document.items.Count(i=>(i.state=="done")==done)+")",()=>{doneView=selected;RenderCompact(true);});SetNavigationSelected(tab,done==doneView);tabs.Children.Add(tab);}
        rows.Children.Add(tabs);
        if(!doneView){var entry=new DockPanel();var title=new TextBox{MaxLength=240,Margin=new Thickness(0,0,8,8),ToolTip="New checklist item"};System.Windows.Automation.AutomationProperties.SetName(title,"New checklist item");
            var error=WorkText("");error.Tag="checklist-error";error.Visibility=Visibility.Collapsed;
            Action add=()=>{try{store.Upsert(new WorkPatch{expectedRevision=document.revision,id="personal-"+Guid.NewGuid().ToString("N"),title=title.Text.Trim()});RenderCompact(true);}catch(Exception failure){error.Text=failure.Message;error.Visibility=Visibility.Visible;}};
            var button=CompactAction(ActionButton("Add",add));button.Height=36;button.Margin=new Thickness(0,0,0,8);title.Height=36;title.Padding=new Thickness(10,0,10,0);title.VerticalContentAlignment=VerticalAlignment.Center;DockPanel.SetDock(button,Dock.Right);entry.Children.Add(button);entry.Children.Add(title);rows.Children.Add(entry);rows.Children.Add(error);
            title.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){add();e.Handled=true;}};
        }
        var selectedItems=document.items.Where(i=>(i.state=="done")==doneView).ToList();
        if(selectedItems.Count==0)rows.Children.Add(WorkText(doneView?"Completed items stay here. Uncheck one to restore it.":"Add your first checklist item above."));
        foreach(var item in selectedItems){var row=new StackPanel{Margin=new Thickness(0,6,0,4),Tag=item.id};
            var line=new DockPanel();var error=WorkText("");
            var edit=CompactAction(ActionButton("Edit",()=>EditChecklistItem(row,store,document,item)));edit.Margin=new Thickness(8,0,0,4);DockPanel.SetDock(edit,Dock.Right);line.Children.Add(edit);
            var check=new CheckBox{Content=WorkText(item.title,true),IsChecked=doneView,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,0,4)};check.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");System.Windows.Automation.AutomationProperties.SetName(check,(doneView?"Restore ":"Complete ")+item.title);
            RoutedEventHandler complete=(s,e)=>{try{store.Complete(document.revision,item.id,check.IsChecked==true);RenderCompact(true);}catch(Exception failure){RenderCompact(true);rows.Children.Add(WorkText(failure.Message));}};
            check.Checked+=complete;check.Unchecked+=complete;line.Children.Add(check);row.Children.Add(line);
            if(!String.IsNullOrWhiteSpace(item.details))row.Children.Add(WorkText(item.details));rows.Children.Add(row);rows.Children.Add(WorkRule());
        }
    }
    void EditChecklistItem(StackPanel row,ChecklistStore store,WorkDocument document,WorkItem item){
        row.Children.Clear();row.Children.Add(WorkText("Edit checklist item",true));var title=new TextBox{Text=item.title,MaxLength=240};System.Windows.Automation.AutomationProperties.SetName(title,"Checklist title");row.Children.Add(title);
        row.Children.Add(WorkText("Details (optional)"));var details=new TextBox{Text=item.details??"",AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MaxLength=2000,MinHeight=65,MaxHeight=160,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};System.Windows.Automation.AutomationProperties.SetName(details,"Checklist details");row.Children.Add(details);
        var error=WorkText("");row.Children.Add(error);var actions=new WrapPanel();
        Action save=()=>{try{store.Upsert(new WorkPatch{expectedRevision=document.revision,id=item.id,title=title.Text.Trim(),details=details.Text,projectPath=item.projectPath});RenderCompact(true);}catch(Exception failure){error.Text=failure.Message;}};
        actions.Children.Add(SourceAction("Save",save));actions.Children.Add(SourceAction("Cancel",()=>RenderCompact(true)));row.Children.Add(actions);title.Focus();title.SelectAll();
        title.KeyDown+=(s,e)=>{if(e.Key==Key.Enter){save();e.Handled=true;}else if(e.Key==Key.Escape){RenderCompact(true);e.Handled=true;}};
    }
}
