using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

internal sealed partial class WpfDashboard {
    Task<WorkSourceCache> workRead;
    static Button SourceAction(string label,Action action){var b=CompactAction(ActionButton(label,action));b.Width=Double.NaN;b.MinWidth=72;b.Margin=new Thickness(0,0,8,8);b.HorizontalAlignment=HorizontalAlignment.Left;return b;}
    string workReadStatus="";
    async void RefreshWorkSource(){
        bool personal=personalTasksView;
        if(workRead!=null&&!workRead.IsCompleted){SetReadStatus(personal,"A source read is still pending. Your cached copy is retained.");RenderCompact(true);return;}
        try{
            var choice=WorkSources.Read(profile,personal);
            if(CloudSources.IsCloud(choice.kind)){SetReadStatus(personal,"Direct online connections are coming soon. The saved source and cache are preserved. Choose a local file in Settings > Tasks.");RenderCompact(true);return;}
            string token=WorkSources.Token(profile,personal);SetReadStatus(personal,"Reading selected source...");RenderCompact(true);
            workRead=Task.Run(()=>WorkSources.Fetch(choice,profile));
            if(!await WorkSources.CompletesWithin(workRead,8000)){
                ObserveFailedRead(workRead);SetReadStatus(personal,"Source read timed out. Cached content may be out of date. Retry after the pending read finishes.");
            }else{
                var cache=await workRead;WorkSources.Store(profile,choice,token,cache,personal);
                SetReadStatus(personal,"Read succeeded. The source file was not changed.");
            }
        }catch(Exception error){SetReadStatus(personal,"Refresh failed; cached content may be out of date. "+error.Message);}
        if(section=="work"&&!agentUpdatesView&&((personal&&personalTasksView)||(!personal&&assistantWorkView)))RenderCompact(true);
    }
    static void ObserveFailedRead(Task<WorkSourceCache> task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
    void ShowWorkSource(){Settings("Tasks");}
    StackPanel BuildWorkSource(out Action saveSource){
        string expected=WorkSources.Token(profile);WorkSourceChoice selected;
        try{selected=WorkSources.Read(profile);}catch{selected=new WorkSourceChoice{kind="unavailable",path=""};}
        var body=new StackPanel{Margin=new Thickness(0,0,14,0)};
        body.Children.Add(new TextBlock{Text="Your tasks and checklist",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,14)});
        body.Children.Add(WorkText(TaskLabel(true),true));
        body.Children.Add(WorkText("Things your agents manage. Dashboard displays the saved list; it does not send instructions to agents."));
        body.Children.Add(WorkText("List location"));
        bool direct=CloudSources.IsCloud(selected.kind);
        var path=new TextBox{Text=direct?"":selected.path,MaxLength=2048,IsReadOnly=true,Margin=new Thickness(0,0,8,0)};
        System.Windows.Automation.AutomationProperties.SetName(path,"Agent tasks list location");
        var row=new DockPanel{Margin=new Thickness(0,0,0,8)};var choose=StandardAction(ActionButton("Choose…",()=>{
            var picker=new OpenFileDialog{Title="Choose Agent tasks file",Filter="Text and Markdown|*.txt;*.md",CheckFileExists=true};
            if(picker.ShowDialog(Window.GetWindow(body)??window)==true)path.Text=picker.FileName;
        }));DockPanel.SetDock(choose,Dock.Right);row.Children.Add(choose);row.Children.Add(path);body.Children.Add(row);
        body.Children.Add(WorkText(direct?"Your saved online source is retained. Direct sign-in is coming soon; choose a local file only if you want to replace that selection.":"Choose a text or Markdown file, including one in a synced folder or network share. Keep synced files available offline."));
        Action validatePersonal,savePersonal;AddPersonalSource(body,out validatePersonal,out savePersonal);
        body.Children.Add(WorkRule());body.Children.Add(WorkText(TaskLabel(false),true));
        body.Children.Add(WorkText("Things you manage. Add, edit and check off tasks in Dashboard."));body.Children.Add(WorkText("List location"));
        string current=ChecklistLocation.PathFor(profile),configToken=ChecklistLocation.Token(ChecklistLocation.Config(profile)),currentToken=ChecklistLocation.Token(current),destinationToken=currentToken;
        var personal=new TextBox{Text=current,IsReadOnly=true,Margin=new Thickness(0,0,8,0)};System.Windows.Automation.AutomationProperties.SetName(personal,"Checklist list location");
        var feedback=WorkText("Changes to your checklist save automatically in this file.");
        var personalRow=new DockPanel{Margin=new Thickness(0,0,0,8)};var personalChoose=StandardAction(ActionButton("Choose…",()=>Protect(()=>{
            var picker=new SaveFileDialog{Title="Choose Checklist file",Filter="Dashboard checklist|*.json",DefaultExt=".json",AddExtension=true,OverwritePrompt=false,FileName="My Checklist.json"};
            if(picker.ShowDialog(Window.GetWindow(body)??window)!=true)return;
            string chosen=ChecklistLocation.Validate(profile,picker.FileName);new ChecklistStore(chosen,entries).Read();
            destinationToken=ChecklistLocation.Token(chosen);personal.Text=chosen;
            feedback.Text=File.Exists(chosen)?"Save opens this existing checklist. Your previous file is kept; the lists are not merged.":"Save copies your checklist here and uses the copy. Your original file is kept.";
        })));DockPanel.SetDock(personalChoose,Dock.Right);personalRow.Children.Add(personalChoose);personalRow.Children.Add(personal);body.Children.Add(personalRow);body.Children.Add(feedback);
        body.Children.Add(WorkRule());body.Children.Add(WorkText("Google Drive, Dropbox and OneDrive direct sign-in is coming soon. Files already synced to this computer work with Choose above."));
        saveSource=()=>{
            validatePersonal();
            string next=path.Text.Trim();bool changeSource=next!=(direct?"":selected.path);
            WorkSourceChoice nextChoice=null;
            if(changeSource){nextChoice=new WorkSourceChoice{schemaVersion=1,kind=next.StartsWith(@"\\")?"network":"local",path=next};WorkSources.Validate(nextChoice);if(WorkSources.Token(profile)!=expected)throw new IOException("Agent tasks location changed. Reopen Settings.");}
            if(personal.Text!=current)ChecklistLocation.Choose(profile,personal.Text,configToken,currentToken,destinationToken,entries);
            if(changeSource){WorkSources.Save(profile,nextChoice,expected);workReadStatus="Agent tasks location saved. Use Refresh to read the list.";}
            savePersonal();
        };
        return body;
    }
    void ShowWorkProviders(){Settings("Tasks");} void ShowProviderHelp(){
        var content=new DockPanel{Margin=new Thickness(24)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});DockPanel.SetDock(close,Dock.Bottom);content.Children.Add(close);
        content.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=WorkText("Direct online connections are coming soon. Google Drive, Dropbox and OneDrive will offer sign-in here in a future version. For now, select a local text or Markdown file, including one in a folder managed by your existing sync application. Your original files and saved connection details are preserved.")});
        presentDialog(Dialog("Online connections: coming soon",720,620,content));
    }
}
