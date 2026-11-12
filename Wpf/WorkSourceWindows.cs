using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

internal sealed partial class WpfDashboard {
    readonly Task<WorkSourceCache>[] taskReads=new Task<WorkSourceCache>[3];
    readonly string[] taskReadTokens=new string[3];
    static Button SourceAction(string label,Action action){var b=CompactAction(ActionButton(label,action));b.Margin=new Thickness(0,0,8,8);b.HorizontalAlignment=HorizontalAlignment.Left;return b;}
    readonly string[] taskReadStatus={"","",""};
    void SetReadStatus(int list,string value){taskReadStatus[list]=value;}
    void RenderTaskRead(int index)
    {
        if(section!="work"||taskListView!=index)return;
        if(activeTaskPrompt!=null){taskPromptNeedsRefresh=true;return;}
        RenderCompact(true);
    }

    void RefreshWorkSource(bool personal,bool manual=true){RefreshWorkSource(personal?1:0,manual);}

    async void RefreshWorkSource(int index,bool manual=true)
    {
        string token=null;
        Task<WorkSourceCache> read=null;
        try
        {
            token=WorkSources.Token(profile,index);
            var choice=WorkSources.Read(profile,index);
            if(taskReads[index]!=null&&!taskReads[index].IsCompleted&&taskReadTokens[index]==token)return;
            if(CloudSources.IsCloud(choice.kind))
            {
                SetReadStatus(index,"Choose a file on this PC in Settings > Tasks to refresh this list.");
                RenderTaskRead(index);return;
            }
            if(manual)SetReadStatus(index,"Loading the selected file…");
            RenderTaskRead(index);
            taskReadTokens[index]=token;
            taskReads[index]=read=Task.Run(()=>WorkSources.Fetch(choice,profile));
            bool complete=await WorkSources.CompletesWithin(read,8000);
            if(!complete)ObserveFailedRead(read);
            if(taskReads[index]!=read||WorkSources.Token(profile,index)!=token)
            {
                if(complete)ObserveFailedRead(read);
                return;
            }
            if(!complete)SetReadStatus(index,"The file did not respond. Try Refresh after the current read finishes.");
            else
            {
                var cache=await read;
                WorkSources.Store(profile,choice,token,cache,index);
                SetReadStatus(index,manual?"Read succeeded. The source file was not changed.":"");
            }
        }
        catch(TaskSourceNotSelectedException){SetReadStatus(index,"");}
        catch(Exception error)
        {
            if(read!=null&&taskReads[index]!=read)return;
            SetReadStatus(index,"Could not load the selected file. "+error.Message);
        }
        RenderTaskRead(index);
    }
    static void ObserveFailedRead(Task<WorkSourceCache> task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
    void ShowWorkSource(){Settings("Tasks");}
    StackPanel BuildWorkSource(out Action saveSource,bool showPreview=false,TextBox[] labels=null,int taskList=-1)
    {
        var body=new StackPanel{Margin=new Thickness(0,0,14,0)};
        var validateLists=new Action[3];
        var saveLists=new Action[3];
        string checklistFile=null,checklistToken=null;
        bool useDefault=false;
        var expectedChecklist=ChecklistLocation.Token(ChecklistLocation.Config(profile));
        body.Children.Add(WorkText("Choose or create a TXT or Markdown file. Disconnect an extra list to hide its tab."));
        for(int list=0;list<3;list++)
        {
            AddTaskSource(body,list,showPreview,labels==null?null:labels[list],list==taskList,out validateLists[list],out saveLists[list]);
            if(list<2||!showPreview)body.Children.Add(TaskSourceRule());
        }
        if(!showPreview)
        {
            string currentPath;
            try{currentPath=ChecklistLocation.PathFor(profile);}
            catch(IOException){currentPath="The saved location is unavailable.";}
            catch(ArgumentException){currentPath="The saved location is unavailable.";}
            catch(InvalidOperationException){currentPath="The saved location is unavailable.";}
            var checklistLocation=WorkText(currentPath);
            var resetChecklist=SourceAction("Use default checklist",()=>{
                checklistFile=null;useDefault=true;
                checklistLocation.Text=ChecklistLocation.DefaultPath(profile)+" will be used after Save.";
            });
            resetChecklist.IsEnabled=expectedChecklist!=null;
            var chooseChecklist=SourceAction("Choose checklist file…",()=>Protect(()=>{
                var picker=new OpenFileDialog{Title="Choose checklist file",Filter="Dashboard checklist|*.json",CheckFileExists=true};
                if(picker.ShowDialog(Window.GetWindow(body)??window)!=true)return;
                var file=ChecklistLocation.Validate(profile,picker.FileName);
                new ChecklistStore(file,entries).Read();
                var token=ChecklistLocation.Token(file);
                if(token==null)throw new IOException("Choose an existing checklist file.");
                checklistFile=file;checklistToken=token;useDefault=false;
                checklistLocation.Text=file+" will be used after Save.";
                resetChecklist.IsEnabled=true;
            }));
            NameControl(chooseChecklist,"Choose checklist file","choose-checklist-file");
            NameControl(resetChecklist,"Use default checklist","use-default-checklist");
            AddTaskHeading(body,TaskLabel(false),labels==null?null:labels[3],"checklists");
            body.Children.Add(checklistLocation);
            var actions=new WrapPanel();
            actions.Children.Add(chooseChecklist);actions.Children.Add(resetChecklist);
            body.Children.Add(actions);
        }
        saveSource=()=>{
            foreach(var validate in validateLists)validate();
            if(useDefault)ChecklistLocation.UseDefault(profile,expectedChecklist,entries);
            else if(checklistFile!=null)
                ChecklistLocation.Choose(profile,checklistFile,expectedChecklist,ChecklistLocation.Token(ChecklistLocation.PathFor(profile)),checklistToken,entries);
            foreach(var save in saveLists)save();
        };
        return body;
    }

    static TextBlock TaskSourceHeading(string label)
    {
        return new TextBlock{Text=label,FontSize=20,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,4,0,4)};
    }

    static Border TaskSourceRule(){
        var rule=WorkRule();
        rule.Margin=new Thickness(0,6,0,8);
        return rule;
    }
    static TextBlock TaskSourceMessage(string text){
        var message=WorkText(text);
        var style=new Style(typeof(TextBlock));
        var empty=new Trigger{Property=TextBlock.TextProperty,Value=""};
        empty.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Collapsed));
        style.Triggers.Add(empty);
        message.Style=style;
        return message;
    }

    void AddTaskHeading(StackPanel body,string label,TextBox name,string id)
    {
        var heading=TaskSourceHeading(label);
        if(name==null){body.Children.Add(heading);return;}
        var row=new DockPanel{Margin=new Thickness(0,4,0,4)};
        var rename=StandardAction(ActionButton("Rename",()=>{name.Visibility=Visibility.Visible;name.Focus();name.SelectAll();}));
        rename.HorizontalAlignment=HorizontalAlignment.Right;
        NameControl(rename,"Rename "+label,id+"-rename");
        DockPanel.SetDock(rename,Dock.Right);row.Children.Add(rename);
        heading.Margin=new Thickness(0);heading.VerticalAlignment=VerticalAlignment.Center;
        row.Children.Add(heading);body.Children.Add(row);
        name.Visibility=Visibility.Collapsed;
        name.TextChanged+=(sender,args)=>heading.Text=String.IsNullOrWhiteSpace(name.Text)?label:name.Text.Trim();
        body.Children.Add(name);
    }

    void AddTaskSource(StackPanel body,int index,bool showPreview,TextBox name,bool focus,out Action validate,out Action save)
    {
        string label=TaskListLabel(index);
        string expected=WorkSources.Token(profile,index);
        WorkSourceChoice selected=null;
        try{selected=WorkSources.Read(profile,index);}catch(IOException){}
        bool online=selected!=null&&CloudSources.IsCloud(selected.kind);
        string original=selected==null||online?"":selected.path;
        bool disconnect=false;
        AddTaskHeading(body,label,name,"task-list-"+(index+1));
        var location=new TextBox{Text=original,IsReadOnly=true,Margin=new Thickness(0,2,0,8)};
        System.Windows.Automation.AutomationProperties.SetName(location,"Task list "+(index+1)+" file location");
        var status=TaskSourceMessage(online?"An online source is selected.":original.Length==0?"No file selected.":"");
        body.Children.Add(status);
        body.Children.Add(location);
        location.Visibility=original.Length==0?Visibility.Collapsed:Visibility.Visible;
        var actions=new WrapPanel();
        var preview=TaskSourceMessage("");
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
            status.Text="";
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
        NameControl(choose,"Choose source for "+label,"task-list-"+(index+1)+"-choose-source");
        NameControl(clear,"Disconnect "+label,"task-list-"+(index+1)+"-disconnect");
        choose.Margin=new Thickness(0,0,8,0);clear.Margin=new Thickness(0);
        actions.Children.Add(choose);actions.Children.Add(clear);
        if(focus)choose.Loaded+=(sender,args)=>choose.Dispatcher.BeginInvoke(new Action(()=>{
            if(choose.IsVisible){choose.BringIntoView();choose.Focus();}
        }),System.Windows.Threading.DispatcherPriority.Input);
        Func<bool> changed=()=>disconnect?selected!=null:location.Text!=original;
        validate=()=>{
            if(!changed())return;
            if(!disconnect)WorkSources.Validate(LocalTaskChoice(location.Text));
            if(WorkSources.Token(profile,index)!=expected)throw new IOException("The task source changed elsewhere. Reopen this window.");
        };
        save=()=>{
            if(!changed())return;
            WorkSources.Save(profile,disconnect?null:LocalTaskChoice(location.Text),expected,index);
            SetReadStatus(index,"");
            if(!disconnect)RefreshWorkSource(index);
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

    static void CreateTaskFile(string path)
    {
        WorkSources.Validate(LocalTaskChoice(path));
        using(var file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){}
    }

    string ChooseTaskSource(Window owner,string label,string current)
    {
        var body=new StackPanel{Margin=new Thickness(28)};
        body.Children.Add(new TextBlock{Text=label,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,16)});
        var dialog=Dialog("Choose task file",580,370,body);
        if(owner!=null&&!nonWindowedVerification)dialog.Owner=owner;
        string file=null;
        var local=SourceAction("Choose existing file…",()=>{
            var picker=new OpenFileDialog{Title="Choose a file for "+label,Filter="Text and Markdown|*.txt;*.md",CheckFileExists=true};
            if(File.Exists(current))picker.FileName=current;
            if(picker.ShowDialog(dialog)!=true)return;
            file=picker.FileName;
            dialog.DialogResult=true;
        });
        var create=SourceAction("Create task file…",()=>{
            var picker=new SaveFileDialog{Title="Create a task file for "+label,Filter="Text file|*.txt|Markdown file|*.md",
                FileName="Task list",DefaultExt=".txt",AddExtension=true,OverwritePrompt=false};
            if(picker.ShowDialog(dialog)!=true)return;
            try
            {
                CreateTaskFile(picker.FileName);
                file=picker.FileName;
                dialog.DialogResult=true;
            }
            catch(Exception error){ShowNotice(dialog,"Create task file",error.Message);}
        });
        var fileChoices=new Grid();
        fileChoices.ColumnDefinitions.Add(new ColumnDefinition());
        fileChoices.ColumnDefinitions.Add(new ColumnDefinition());
        var existingChoice=new StackPanel{Margin=new Thickness(0,0,12,0)};
        existingChoice.Children.Add(local);
        existingChoice.Children.Add(WorkText("Browse this PC, a synced folder, or a network share."));
        var newChoice=new StackPanel();
        newChoice.Children.Add(create);
        newChoice.Children.Add(WorkText("Name and save a new blank file."));
        Grid.SetColumn(newChoice,1);
        fileChoices.Children.Add(existingChoice);
        fileChoices.Children.Add(newChoice);
        body.Children.Add(fileChoices);
        body.Children.Add(WorkRule());
        body.Children.Add(WorkText("Online connections",true));
        var providers=new WrapPanel();
        foreach(string provider in new[]{"Google Drive","Dropbox","OneDrive"})
        {
            var labelRow=new StackPanel{Orientation=Orientation.Horizontal};
            labelRow.Children.Add(TaskProviderMark(provider));
            labelRow.Children.Add(new TextBlock{Text=provider,VerticalAlignment=VerticalAlignment.Center});
            var choice=StandardAction(new Button{Content=labelRow,IsEnabled=false,Width=Double.NaN,MinWidth=105});
            NameControl(choice,provider,"task-source-"+provider.Replace(" ","").ToLowerInvariant());
            choice.Margin=new Thickness(0,0,8,0);
            providers.Children.Add(choice);
        }
        body.Children.Add(providers);
        var cancel=StandardAction(new Button{Content="Cancel",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});
        cancel.Margin=new Thickness(0,18,0,0);
        body.Children.Add(cancel);
        return presentDialog(dialog)==true?file:null;
    }
    static FrameworkElement TaskProviderMark(string provider)
    {
        var canvas=new Canvas{Width=24,Height=24};
        Action<string,string> add=(geometry,color)=>canvas.Children.Add(new System.Windows.Shapes.Path{
            Data=Geometry.Parse(geometry),Fill=(Brush)new BrushConverter().ConvertFromString(color)});
        switch(provider){
            case "Google Drive":
                add("M8,3 L13,3 L6,16 L1,16 Z","#34A853");
                add("M8,3 L14,3 L22,16 L16,16 Z","#FBBC04");
                add("M1,16 L22,16 L19,21 L4,21 Z","#4285F4");
                break;
            case "Dropbox":
                add("M6,3 L12,7 L6,11 L0,7 Z M18,3 L24,7 L18,11 L12,7 Z M6,11 L12,15 L6,19 L0,15 Z M18,11 L24,15 L18,19 L12,15 Z M6,20 L12,16 L18,20 L12,24 Z","#4089FF");
                break;
            default:
                add("M2,17 C2,13 5,11 8,12 C8,7 12,4 16,6 C19,7 20,10 20,12 C25,13 25,20 20,20 L5,20 C3,20 2,19 2,17 Z","#3478D1");
                add("M3,16 C7,11 12,11 16,14 L22,19 C21,20 20,20 19,20 L5,20 C3,20 2,18 3,16 Z","#55B5F5");
                break;
        }
        return new Viewbox{Width=18,Height=18,Margin=new Thickness(0,0,6,0),Child=canvas};
    }
    void ShowWorkProviders(){Settings("Tasks");} void ShowProviderHelp(){
        var content=new DockPanel{Margin=new Thickness(24)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});DockPanel.SetDock(close,Dock.Bottom);content.Children.Add(close);
        content.Children.Add(new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Content=WorkText("Direct online connections are coming soon. Google Drive, Dropbox and OneDrive will offer sign-in here in a future version. For now, select a local text or Markdown file, including one in a folder managed by your existing sync application. Your original files and saved connection details are preserved.")});
        presentDialog(Dialog("Online connections: coming soon",720,620,content));
    }
}
