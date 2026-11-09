using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Diagnostics;
using Microsoft.Win32;

internal sealed partial class WpfDashboard {
    void ShowDataFolder(){
        var panel=new DockPanel{Margin=new Thickness(28)};var content=new StackPanel();
        content.Children.Add(new TextBlock{Text="Your Dashboard data",FontSize=24});
        content.Children.Add(WorkText("Your catalog and .local folder hold settings, the default checklist and task-list snapshots. Your authoritative work list stays at its selected source. Linked documents and project files remain in their own folders. Participating status reports are separate and are not restored from configuration backups."));
        var path=new TextBox{Text=Path.GetFullPath(profile),IsReadOnly=true,Height=36,VerticalContentAlignment=VerticalAlignment.Center,Padding=new Thickness(10,0,10,0)};content.Children.Add(path);
        try{content.Children.Add(WorkText("Selected work-list source: "+WorkSources.Read(profile).path));}catch(Exception error){content.Children.Add(WorkText("Source selection unavailable: "+error.Message));}
        var actions=new WrapPanel();
        actions.Children.Add(ActionButton("Open in File Explorer",()=>Protect(OpenDataFolder)));
        actions.Children.Add(ActionButton("Copy path",()=>Clipboard.SetText(path.Text)));
        content.Children.Add(actions);
        var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);panel.Children.Add(close);
        panel.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        presentDialog(Dialog("Dashboard data folder",700,460,panel));
    }
    bool DataConfirmation(string title,string heading,string detail,string action){
        var content=new DockPanel{Margin=new Thickness(28)};
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        DockPanel.SetDock(actions,Dock.Bottom);content.Children.Add(actions);
        var body=new StackPanel();body.Children.Add(new TextBlock{Text=heading,FontSize=24,TextWrapping=TextWrapping.Wrap});body.Children.Add(WorkText(detail));
        content.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=body});
        var dialog=Dialog(title,680,360,content);
        if(action=="Close")actions.Children.Add(StandardAction(new Button{Content="Close",IsCancel=true,IsDefault=true}));
        else{
            actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
            var confirm=StandardAction(ActionButton(action,()=>dialog.DialogResult=true));confirm.Width=Double.NaN;confirm.MinWidth=72;actions.Children.Add(confirm);
        }
        return presentDialog(dialog)==true;
    }
    void BackupConfiguration(){
        if(Moving)throw new IOException("Save or cancel the current move first.");
        if(!DataConfirmation("Back up Dashboard","Configuration and task-list copies","Includes your settings, dashboard links, favorites and layout, plus copies of Agent tasks, Personal tasks and Checklist when available. The result tells you if a list could not be copied. Linked project files, online-only content and progress reports are not included.","Continue"))return;
        var picker=new SaveFileDialog{Title="Back up Dashboard configuration",FileName="local-dashboard-"+DateTime.Now.ToString("yyyy-MM-dd")+".json",Filter="Dashboard configuration|*.json",AddExtension=true,DefaultExt=".json"};
        if(picker.ShowDialog(window)!=true)return;
        var saved=DashboardData.ExportWithLists(profile,picker.FileName);
        DataConfirmation("Backup saved","Dashboard backup saved",DashboardData.TaskCopySummary(saved)+"\r\n\r\nYour original lists and linked files were not changed.","Close");
    }
    void RestoreConfiguration(){
        if(Moving)throw new IOException("Save or cancel the current move first.");
        var picker=new OpenFileDialog{Title="Restore Dashboard configuration",Filter="Dashboard backups|*.json"};
        if(picker.ShowDialog(window)!=true)return;
        var backup=DashboardData.Load(picker.FileName);
        DashboardData.CheckBackup(profile,backup);
        var content=new DockPanel{Margin=new Thickness(28)};var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);content.Children.Add(actions);
        var body=new StackPanel();body.Children.Add(WorkText("Restore configuration and separate list copies",true));body.Children.Add(WorkText("This replaces your Dashboard settings, links, favorites and layout. Back up the current configuration first if you need it. Any task lists in this backup are saved as new copies; your current lists are kept."));body.Children.Add(WorkText(DashboardData.TaskCopySummary(backup)));
        var useCopies=Option(body,"Use the restored task-list copies in Dashboard",false);body.Children.Add(WorkText("Leave this off to keep using your current lists. You can choose the restored copies later in Settings > Tasks. Missing copies never replace a current selection."));content.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var dialog=Dialog("Restore configuration",700,550,content);actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));actions.Children.Add(StandardAction(ActionButton("Restore",()=>dialog.DialogResult=true)));if(presentDialog(dialog)!=true)return;
        var restored=DashboardData.RestoreCopies(profile,backup,useCopies.IsChecked==true);preferenceStore.Reload();
        pins.Clear();string pinFile=StatePath("pins.json");if(File.Exists(pinFile))pins.UnionWith(AppData.Json.Deserialize<string[]>(File.ReadAllText(pinFile)));
        DashboardTheme.Apply(preferenceStore.Value.theme);Reload();ApplyAccent(SavedAccent());RefreshGlyphs();ThemeWindow(window,null);
        DataConfirmation("Configuration restored","Dashboard configuration restored",(restored.Folder==null?"No task-list copies were available.":"Restored task-list copies:\r\n"+restored.Folder)+(useCopies.IsChecked==true?"\r\nAvailable copies are now selected. Use Refresh to read Agent Tasks.":"\r\nDashboard still uses your previous task-list selections.")+"\r\n\r\nYour original lists were not changed. Verify dashboard link destinations on this PC.","Close");
    }
}
