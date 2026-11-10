using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

internal sealed partial class WpfDashboard {
    readonly Task<WorkSourceCache>[] taskReads=new Task<WorkSourceCache>[2];
    readonly string[] taskReadTokens=new string[2];
    static Button SourceAction(string label,Action action){var b=CompactAction(ActionButton(label,action));b.Width=Double.NaN;b.MinWidth=72;b.Margin=new Thickness(0,0,8,8);b.HorizontalAlignment=HorizontalAlignment.Left;return b;}
    string workReadStatus="";
    void RenderTaskRead(bool personal)
    {
        if(section=="work"&&!agentUpdatesView&&(personal?personalTasksView:assistantWorkView))RenderCompact(true);
    }

    async void RefreshWorkSource(bool personal)
    {
        int index=personal?1:0;
        string token=null;
        Task<WorkSourceCache> read=null;
        try
        {
            token=WorkSources.Token(profile,personal);
            var choice=WorkSources.Read(profile,personal);
            if(taskReads[index]!=null&&!taskReads[index].IsCompleted&&taskReadTokens[index]==token)return;
            if(CloudSources.IsCloud(choice.kind))
            {
                SetReadStatus(personal,"Choose a file on this PC in Settings > Tasks to refresh this list.");
                RenderTaskRead(personal);return;
            }
            SetReadStatus(personal,"Loading the selected file…");
            RenderTaskRead(personal);
            taskReadTokens[index]=token;
            taskReads[index]=read=Task.Run(()=>WorkSources.Fetch(choice,profile));
            bool complete=await WorkSources.CompletesWithin(read,8000);
            if(!complete)ObserveFailedRead(read);
            if(taskReads[index]!=read||WorkSources.Token(profile,personal)!=token)
            {
                if(complete)ObserveFailedRead(read);
                return;
            }
            if(!complete)SetReadStatus(personal,"The file did not respond. Try Refresh after the current read finishes.");
            else
            {
                var cache=await read;
                WorkSources.Store(profile,choice,token,cache,personal);
                SetReadStatus(personal,"Read succeeded. The source file was not changed.");
            }
        }
        catch(TaskSourceNotSelectedException){SetReadStatus(personal,"");}
        catch(Exception error)
        {
            if(read!=null&&taskReads[index]!=read)return;
            SetReadStatus(personal,"Could not load the selected file. "+error.Message);
        }
        RenderTaskRead(personal);
    }
    static void ObserveFailedRead(Task<WorkSourceCache> task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
    void ShowWorkSource(){Settings("Tasks");}
    StackPanel BuildWorkSource(out Action saveSource,bool showPreview=false,TextBox[] labels=null)
    {
        var body=new StackPanel{Margin=new Thickness(0,0,14,0)};
        Action validateAgent,saveAgent,validatePersonal,savePersonal;
        AddTaskSource(body,false,showPreview,labels==null?null:labels[0],out validateAgent,out saveAgent);
        body.Children.Add(WorkRule());
        AddTaskSource(body,true,showPreview,labels==null?null:labels[1],out validatePersonal,out savePersonal);
        body.Children.Add(WorkRule());
        AddTaskHeading(body,TaskLabel(false),labels==null?null:labels[2]);
        body.Children.Add(WorkText("Dashboard saves your checklist automatically."));
        saveSource=()=>{validateAgent();validatePersonal();saveAgent();savePersonal();};
        return body;
    }

    static TextBlock TaskSourceHeading(string label)
    {
        return new TextBlock{Text=label,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,10,0,4)};
    }

    void AddTaskHeading(StackPanel body,string label,TextBox name)
    {
        var heading=TaskSourceHeading(label);
        if(name==null){body.Children.Add(heading);return;}
        var row=new DockPanel{Margin=new Thickness(0,8,0,2)};
        var rename=StandardAction(ActionButton("Rename",()=>{name.Visibility=Visibility.Visible;name.Focus();name.SelectAll();}));
        rename.HorizontalAlignment=HorizontalAlignment.Right;
        System.Windows.Automation.AutomationProperties.SetName(rename,"Rename "+label);
        DockPanel.SetDock(rename,Dock.Right);row.Children.Add(rename);
        heading.Margin=new Thickness(0);heading.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(heading);body.Children.Add(row);
        name.Visibility=Visibility.Collapsed;
        name.TextChanged+=(sender,args)=>heading.Text=String.IsNullOrWhiteSpace(name.Text)?label:name.Text.Trim();
        body.Children.Add(name);
    }

    void AddTaskSource(StackPanel body,bool personal,bool showPreview,TextBox name,out Action validate,out Action save)
    {
        string label=personal?PersonalTaskLabel():TaskLabel(true);
        string expected=WorkSources.Token(profile,personal);
        WorkSourceChoice selected=null;
        try{selected=WorkSources.Read(profile,personal);}catch(IOException){}
        bool online=selected!=null&&CloudSources.IsCloud(selected.kind);
        string original=selected==null||online?"":selected.path;
        bool disconnect=false;
        AddTaskHeading(body,label,name);
        body.Children.Add(WorkText(personal?"A separate task file you can edit in Dashboard.":"A task file Dashboard reads without changing."));
        var location=new TextBox{Text=original,IsReadOnly=true};
        System.Windows.Automation.AutomationProperties.SetName(location,personal?"Personal tasks list location":"Agent tasks list location");
        var status=WorkText(online?"An online source is selected.":original.Length==0?"No source selected.":"Selected file");
        body.Children.Add(status);
        body.Children.Add(location);
        location.Visibility=original.Length==0?Visibility.Collapsed:Visibility.Visible;
        var actions=new WrapPanel{Margin=new Thickness(0,2,0,4)};
        var preview=WorkText("");
        body.Children.Add(actions);
        if(showPreview)body.Children.Add(preview);
        var clear=SourceAction("Disconnect",()=>{disconnect=true;location.Clear();status.Text="The source will be disconnected when you save.";preview.Text="";});
        clear.IsEnabled=selected!=null;
        location.TextChanged+=(sender,args)=>{
            clear.IsEnabled=location.Text.Length>0||online&&!disconnect;
            location.Visibility=location.Text.Length==0?Visibility.Collapsed:Visibility.Visible;
        };
        var choose=SourceAction("Choose source…",async ()=>{
            string file;
            try{file=ChooseTaskSource(Window.GetWindow(body),label,location.Text);}
            catch(Exception error){status.Text=error.Message;return;}
            if(file==null)return;
            disconnect=false;
            location.Text=file;
            status.Text="Selected file";
            if(!showPreview)return;
            preview.Text="Reading preview…";
            try
            {
                var choice=LocalTaskChoice(file);
                var read=Task.Run(()=>WorkSources.Fetch(choice,profile));
                if(!await WorkSources.CompletesWithin(read,8000))
                {
                    ObserveFailedRead(read);
                    if(location.Text==file)preview.Text="The preview timed out. You can try the file again from Tasks.";
                    return;
                }
                var result=await read;
                if(location.Text==file)preview.Text=SetupTaskPreview(result.text);
            }
            catch(Exception error){if(location.Text==file)preview.Text="Could not read this file. "+error.Message;}
        });
        actions.Children.Add(choose);actions.Children.Add(clear);
        Func<bool> changed=()=>disconnect?selected!=null:location.Text!=original;
        validate=()=>{
            if(!changed())return;
            if(!disconnect)WorkSources.Validate(LocalTaskChoice(location.Text));
            if(WorkSources.Token(profile,personal)!=expected)throw new IOException("The task source changed elsewhere. Reopen this window.");
        };
        save=()=>{
            if(!changed())return;
            WorkSources.Save(profile,disconnect?null:LocalTaskChoice(location.Text),expected,personal);
            SetReadStatus(personal,"");
            if(!disconnect)RefreshWorkSource(personal);
        };
    }

    static string SetupTaskPreview(string text)
    {
        if(String.IsNullOrWhiteSpace(text))return "This file is empty.";
        var tasks=ReadableTasks.Parse(text).Blocks.Where(block=>block.Kind=="task").Take(3).ToList();
        if(tasks.Count==0)return "File is readable. Open Tasks to see your list.";
        return String.Join(Environment.NewLine,tasks.Select(block=>block.Number+". "+block.Title));
    }

    static WorkSourceChoice LocalTaskChoice(string path)
    {
        return new WorkSourceChoice{schemaVersion=1,kind=path.StartsWith(@"\\")?"network":"local",path=path};
    }

    string ChooseTaskSource(Window owner,string label,string current)
    {
        var body=new StackPanel{Margin=new Thickness(28)};
        body.Children.Add(new TextBlock{Text=label,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
        var dialog=Dialog("Choose source",520,400,body);
        if(owner!=null&&!nonWindowedVerification)dialog.Owner=owner;
        string file=null;
        var local=StandardAction(ActionButton("File on this PC",()=>{
            var picker=new OpenFileDialog{Title="Choose a file for "+label,Filter="Text and Markdown|*.txt;*.md",CheckFileExists=true};
            if(File.Exists(current))picker.FileName=current;
            if(picker.ShowDialog(dialog)!=true)return;
            file=picker.FileName;
            dialog.DialogResult=true;
        }));
        local.Margin=new Thickness(0,0,0,6);
        body.Children.Add(local);
        body.Children.Add(WorkText("Files synced to this PC can be selected here."));
        foreach(string provider in new[]{"Google Drive","Dropbox","OneDrive"})
        {
            var choice=StandardAction(new Button{Content=provider,IsEnabled=false});
            choice.Margin=new Thickness(0,8,0,0);
            body.Children.Add(choice);
        }
        var cancel=StandardAction(new Button{Content="Cancel",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});
        cancel.Margin=new Thickness(0,18,0,0);
        body.Children.Add(cancel);
        return presentDialog(dialog)==true?file:null;
    }
    void ShowWorkProviders(){Settings("Tasks");} void ShowProviderHelp(){
        var content=new DockPanel{Margin=new Thickness(24)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});DockPanel.SetDock(close,Dock.Bottom);content.Children.Add(close);
        content.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=WorkText("Direct online connections are coming soon. Google Drive, Dropbox and OneDrive will offer sign-in here in a future version. For now, select a local text or Markdown file, including one in a folder managed by your existing sync application. Your original files and saved connection details are preserved.")});
        presentDialog(Dialog("Online connections: coming soon",720,620,content));
    }
}
