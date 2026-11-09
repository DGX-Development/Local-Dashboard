using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.Text.RegularExpressions;

internal sealed partial class WpfDashboard {
    bool personalTasksView;
    string personalReadStatus="";
    string CurrentReadStatus {get{return personalTasksView?personalReadStatus:workReadStatus;}set{if(personalTasksView)personalReadStatus=value;else workReadStatus=value;}}
    void SetReadStatus(bool personal,string value){if(personal)personalReadStatus=value;else workReadStatus=value;}
    static Expander TaskDetails(string header,string text){
        var more=new Expander{Header=header,IsExpanded=false,Content=WorkText(text),Margin=new Thickness(0,6,0,8)};
        more.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");
        return more;
    }
    void RenderReadableTaskText(string text,StackPanel target,bool editable=false){
        var document=ReadableTasks.Parse(text);
        if(!document.Blocks.Any(b=>b.Kind=="task")){target.Children.Add(WorkText(text));return;}
        var groups=new Dictionary<ReadableTaskBlock,string>();var counts=new Dictionary<string,int>();string group="";
        foreach(var block in document.Blocks){
            if(block.Kind=="heading")group=block.Raw.TrimStart().StartsWith("### ")?Regex.Replace(block.Title,@"^\d+(?:\.\d+)*[.)]?\s+",""):"";
            if(block.Kind=="task"){groups[block]=group;if(group!="")counts[group]=counts.ContainsKey(group)?counts[group]+1:1;}
        }
        var deferred=new StackPanel();
        foreach(var block in document.Blocks){
            if(block.Kind!="task")continue;
            var content=new StackPanel{Margin=new Thickness(0,6,0,8),Tag="readable-task"};
            var taskTitle=new DockPanel();
            if(editable){var current=block;var edit=CompactAction(ActionButton("Edit",()=>Protect(()=>EditPersonalTask(text,current))));edit.Tag="personal-edit";edit.Margin=new Thickness(8,0,0,4);DockPanel.SetDock(edit,Dock.Right);taskTitle.Children.Add(edit);}
            string project=groups[block],title=block.Title;
            if(project!=""){
                if(counts[project]==1&&title.IndexOf(project,StringComparison.CurrentCultureIgnoreCase)>=0)title=project;
                else if(title.IndexOf(project,StringComparison.CurrentCultureIgnoreCase)<0)title=project+": "+title;
            }
            taskTitle.Children.Add(WorkText(block.Number+". "+title,true));content.Children.Add(taskTitle);
            if(block.Deferred)content.Children.Add(WorkText("Deferred / do not start",true));
            if(!String.IsNullOrWhiteSpace(block.Summary))content.Children.Add(WorkText(block.Summary));
            var extra=new List<string>();
            if(title!=block.Title)extra.Add("Task: "+block.Title);
            foreach(var field in new[]{new[]{"Status",block.Status},new[]{"Next",block.Next}}){
                if(String.IsNullOrWhiteSpace(field[1]))continue;
                extra.Add(field[0]+": "+field[1]);
            }
            if(block.Ambiguous)content.Children.Add(WorkText("Several task sections are preserved together in Details."));
            if(!String.IsNullOrWhiteSpace(block.Details))extra.Add(block.Details);
            if(extra.Count>0)content.Children.Add(TaskDetails("Details",String.Join("\n\n",extra)));
            var destination=block.Deferred?deferred:target;destination.Children.Add(content);destination.Children.Add(WorkRule());
        }
        if(deferred.Children.Count>0)target.Children.Add(new Expander{Header="Deferred work · Do not start",Content=deferred,IsExpanded=false,Margin=new Thickness(0,12,0,12)});
    }
    void AddPersonalSource(StackPanel body,out Action validate,out Action save){
        string expected=WorkSources.Token(profile,true);WorkSourceChoice selected=null;
        try{selected=WorkSources.Read(profile,true);}catch(IOException){}
        string original=selected==null?"":selected.path;
        body.Children.Add(WorkRule());body.Children.Add(WorkText("Personal tasks",true));
        body.Children.Add(WorkText("Your personal text file. Edit it here in Personal tasks, or in your usual editor. Both use this same file."));
        body.Children.Add(WorkText("List location"));
        var field=new TextBox{Text=original,IsReadOnly=true,Margin=new Thickness(0,0,8,0)};
        System.Windows.Automation.AutomationProperties.SetName(field,"Personal tasks list location");
        var row=new DockPanel{Margin=new Thickness(0,0,0,8)};
        var choose=StandardAction(ActionButton("Choose...",()=>{
            var picker=new OpenFileDialog{Title="Choose Personal tasks file",Filter="Text and Markdown|*.txt;*.md",CheckFileExists=true};
            if(picker.ShowDialog(Window.GetWindow(body)??window)==true)field.Text=picker.FileName;
        }));DockPanel.SetDock(choose,Dock.Right);row.Children.Add(choose);row.Children.Add(field);body.Children.Add(row);
        body.Children.Add(WorkText("An existing local, synced or network file works. This is separate from Checklists below."));
        Func<WorkSourceChoice> choice=()=>new WorkSourceChoice{schemaVersion=1,kind=field.Text.StartsWith(@"\\")?"network":"local",path=field.Text};
        validate=()=>{if(field.Text==original)return;WorkSources.Validate(choice());if(WorkSources.Token(profile,true)!=expected)throw new IOException("Personal tasks location changed. Reopen Settings.");};
        save=()=>{if(field.Text==original)return;WorkSources.Save(profile,choice(),expected,true);personalReadStatus="Personal tasks location saved. Select Refresh to load it.";};
    }
    void RenderSavedTaskList(bool personal){
        string status=personal?personalReadStatus:workReadStatus;
        if(!String.IsNullOrWhiteSpace(status))rows.Children.Add(WorkText(status));
        try{
            var source=WorkSources.Read(profile,personal);var cache=WorkSources.Cached(profile,source);
            if(personal&&!CloudSources.IsCloud(source.kind)){
                var actions=new WrapPanel();actions.Children.Add(SourceAction("Add task",()=>Protect(()=>EditPersonalTask(null,null))));actions.Children.Add(SourceAction("Edit document",()=>Protect(()=>EditPersonalTask(null,null,true))));rows.Children.Add(actions);
            }
            if(cache==null){rows.Children.Add(WorkText("This list has not been loaded yet.",true));rows.Children.Add(WorkText("Choose its file in Settings > Tasks, then select Refresh."));rows.Children.Add(TaskDetails("Source details",source.path));return;}
            if(String.IsNullOrWhiteSpace(cache.text))rows.Children.Add(WorkText("This text file is empty."));else RenderReadableTaskText(cache.text,rows,personal&&!CloudSources.IsCloud(source.kind));
            var sourceDetails=new StackPanel();sourceDetails.Children.Add(WorkText("File: "+source.path));
            sourceDetails.Children.Add(WorkText("Read "+DateTimeOffset.Parse(cache.refreshedAt).ToLocalTime().ToString("g")));
            sourceDetails.Children.Add(WorkText("The complete document below keeps all headings, instructions and additional text."));
            sourceDetails.Children.Add(new TextBox{Text=cache.text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true});
            rows.Children.Add(new Expander{Header="Source and full document",IsExpanded=false,Content=sourceDetails,Margin=new Thickness(0,12,0,0)});
        }catch(Exception error){rows.Children.Add(WorkText((personal?"Personal tasks":"Agent tasks")+" unavailable. "+error.Message));}
    }
}
