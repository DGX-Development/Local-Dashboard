using System.Linq;
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
        if(!DataConfirmation("Back up Dashboard","Configuration and task-list copies","Includes your settings, dashboard links, favorites, layout and custom icons, plus copies of Agent tasks, Personal tasks and Checklist when available. The result tells you if a list could not be copied. Linked project files, online-only content and progress reports are not included.","Continue"))return;
        var picker=new SaveFileDialog{Title="Back up Dashboard configuration",FileName="local-dashboard-"+DateTime.Now.ToString("yyyy-MM-dd")+".json",Filter="Dashboard configuration|*.json",AddExtension=true,DefaultExt=".json"};
        if(picker.ShowDialog(window)!=true)return;
        var saved=DashboardData.ExportWithLists(profile,picker.FileName);
        DataConfirmation("Backup saved","Dashboard backup saved",DashboardData.TaskCopySummary(saved)+"\r\n"+DashboardData.BackupIconSummary(saved)+"\r\n\r\nYour original lists and linked files were not changed.","Close");
    }
    void RestoreConfiguration(){
        var picker=new OpenFileDialog{Title="Restore Dashboard configuration",Filter="Dashboard backups|*.json"};
        if(picker.ShowDialog(window)!=true)return;
        var catalog=DashboardData.CatalogImportData(picker.FileName);
        if(catalog!=null){
            string currentHash=DashboardData.Hash(DashboardData.ReadBounded(Path.Combine(profile,"catalog.json")));
            int count=DashboardData.CatalogCount(catalog,profile);
            if(!DataConfirmation("Import dashboard links","This file contains dashboard links only",count+" links will replace your current dashboard links. This does not restore settings, category labels or favorites. Your task-list selections and checklist stay unchanged. A copy of the current catalog will be saved before import.\r\n\r\nLinked files and folders are not copied or deleted. Check that their destinations are available on this PC.","Import links"))return;
            string retained=DashboardData.ImportCatalog(profile,catalog,currentHash);Reload();
            DataConfirmation("Dashboard links imported",count+" dashboard links imported","Previous catalog retained at:\r\n"+retained+"\r\n\r\nSettings and task lists were not changed. All links shows every imported link.","Close");return;
        }
        var backup=DashboardData.Load(picker.FileName);
        DashboardData.CheckBackup(profile,backup);
        var content=new DockPanel{Margin=new Thickness(28)};var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);content.Children.Add(actions);
        var body=new StackPanel();
        body.Children.Add(WorkText("Restore Dashboard configuration",true));
        body.Children.Add(WorkText("Restore replaces your Dashboard links, categories, favorites and settings. Dashboard backs up your current configuration first. Restored copies are saved as new files. Your existing files are kept."));
        body.Children.Add(WorkText(DashboardData.BackupIconSummary(backup)));
        var copies=DashboardData.TaskCopies(backup);
        var choices=new System.Collections.Generic.Dictionary<string,CheckBox>();
        foreach(string role in new[]{"agent","personal","checklist"})
        {
            string label=role=="agent"?"Agent tasks":role=="personal"?"Personal tasks":"Checklists";
            body.Children.Add(WorkText(label,true));
            if(copies[role].status!="included")
            {
                body.Children.Add(WorkText("No copy in this backup. Dashboard keeps your current selection."));
                continue;
            }
            var current=DashboardData.CurrentRestoreList(profile,role,entries);
            bool hasCurrent=current==RestoreListStatus.Available;
            choices[role]=Option(body,"Use restored copy",!hasCurrent);
            string fileLabel=role=="checklist"?"checklist":label;
            string explanation;
            if(hasCurrent)
                explanation="Dashboard will keep using your current file unless you select the restored copy.";
            else if(current==RestoreListStatus.Unset)
                explanation="No "+fileLabel+" file is set up. Dashboard will use the restored copy if this box stays checked.";
            else
                explanation="Dashboard can't open your current "+fileLabel+" file. Dashboard will use the restored copy if this box stays checked.";
            body.Children.Add(WorkText(explanation));
        }
        content.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var dialog=Dialog("Restore configuration",720,650,content);
        actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        actions.Children.Add(StandardAction(ActionButton("Restore",()=>dialog.DialogResult=true)));
        if(presentDialog(dialog)!=true)return;

        string recovery=DashboardData.CreateRestoreRecoveryBackup(profile);
        var selected=choices.Where(choice=>choice.Value.IsChecked==true).Select(choice=>choice.Key).ToArray();
        var restored=DashboardData.RestoreCopies(profile,backup,selected);
        preferenceStore.Reload();
        pins.Clear();string pinFile=StatePath("pins.json");if(File.Exists(pinFile))pins.UnionWith(AppData.Json.Deserialize<string[]>(File.ReadAllText(pinFile)));
        DashboardTheme.Apply(preferenceStore.Value.theme);Reload();ApplyAccent(SavedAccent());RefreshGlyphs();ThemeWindow(window,null);
        if(selected.Contains("agent"))RefreshWorkSource(false);
        if(selected.Contains("personal"))RefreshWorkSource(true);
        int missing=entries.SelectMany(entry=>entry.targets).Count(target=>
            target.kind=="folder"&&!Directory.Exists(target.path)||target.kind=="document"&&!File.Exists(target.path));
        int missingIcons=entries.Select(entry=>entry.iconPath).Concat(layout.items.Select(item=>item.iconPath))
            .Where(path=>!String.IsNullOrWhiteSpace(path)&&!File.Exists(path)).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        string lists=String.Join("\r\n",new[]{"agent","personal","checklist"}.Select(role=>{
            string label=role=="agent"?"Agent tasks":role=="personal"?"Personal tasks":"Checklists";
            return label+": "+(selected.Contains(role)?"using the restored copy.":"previous selection kept.");
        }));
        DataConfirmation("Configuration restored","Dashboard configuration restored",lists+
            (restored.Folder==null?"":"\r\n\r\nRestored copies: "+restored.Folder)+
            "\r\n\r\nBackup made before restore: "+recovery+
            (missing==0?"":"\r\n\r\n"+missing+" linked file or folder destinations are unavailable on this PC. Use Edit Dashboard to reconnect them.")+
            (missingIcons==0?"":"\r\n\r\n"+missingIcons+" custom icon files are unavailable. Use Edit Dashboard to choose replacements."),"Close");
    }
}
