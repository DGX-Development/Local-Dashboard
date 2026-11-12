using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text.RegularExpressions;

internal sealed partial class WpfDashboard {
    static Expander TaskDetails(string header,string text){
        var more=new Expander{Header=header,IsExpanded=false,Content=WorkText(text),Margin=new Thickness(0,6,0,8)};
        more.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");
        return more;
    }
    void RenderReadableTaskText(string text,StackPanel target,string query=""){
        var document=ReadableTasks.Parse(text);
        if(!document.Blocks.Any(b=>b.Kind=="task")){
            target.Children.Add(WorkText(MatchesTaskSearch(query,text)?text:"No tasks match your search."));
            return;
        }
        var groups=new Dictionary<ReadableTaskBlock,string>();var counts=new Dictionary<string,int>();string group="";
        foreach(var block in document.Blocks){
            if(block.Kind=="heading")group=block.Raw.TrimStart().StartsWith("### ")?Regex.Replace(block.Title,@"^\d+(?:\.\d+)*[.)]?\s+",""):"";
            if(block.Kind=="task"){groups[block]=group;if(group!="")counts[group]=counts.ContainsKey(group)?counts[group]+1:1;}
        }
        var deferred=new StackPanel();
        int activeNumber=0,deferredNumber=0,matches=0;
        foreach(var block in document.Blocks){
            if(block.Kind!="task")continue;
            string project=groups[block],title=block.Title;
            if(project!=""){
                if(counts[project]==1&&title.IndexOf(project,StringComparison.CurrentCultureIgnoreCase)>=0)title=project;
                else if(title.IndexOf(project,StringComparison.CurrentCultureIgnoreCase)<0)title=project+": "+title;
            }
            int number=block.Deferred?++deferredNumber:++activeNumber;
            if(!MatchesTaskSearch(query,title,project,block.Raw))continue;
            matches++;
            var content=new StackPanel();
            content.Children.Add(WorkText(number+". "+title,true));
            if(block.Deferred)content.Children.Add(WorkText("Deferred / do not start",true));
            if(!String.IsNullOrWhiteSpace(block.Summary))content.Children.Add(WorkText(block.Summary));
            var extra=new List<string>{"File task ID "+block.Number};
            if(title!=block.Title)extra.Add("Task: "+block.Title);
            foreach(var field in new[]{new[]{"Status",block.Status},new[]{"Next",block.Next}}){
                if(String.IsNullOrWhiteSpace(field[1]))continue;
                extra.Add(field[0]+": "+field[1]);
            }
            if(block.Ambiguous)content.Children.Add(WorkText("Several task sections are preserved together in Details."));
            if(!String.IsNullOrWhiteSpace(block.Details))extra.Add(block.Details);
            if(extra.Count>0)content.Children.Add(TaskDetails("Details",String.Join("\n\n",extra)));
            var cardRow=new StackPanel{Tag="readable-task"};
            cardRow.Children.Add(DashboardCard(content));
            var destination=block.Deferred?deferred:target;destination.Children.Add(cardRow);
        }
        if(deferred.Children.Count>0)target.Children.Add(new Expander{Header="Deferred work · Do not start",Content=deferred,IsExpanded=query.Length>0,Margin=new Thickness(0,12,0,12)});
        if(matches==0)target.Children.Add(WorkText("No tasks match your search."));
    }
    static void CopyAgentPrompt(string prompt,TextBlock status){
        try{
            Clipboard.SetText(prompt);
            status.Text="Prompt copied.";
        }
        catch(System.Runtime.InteropServices.ExternalException){
            status.Text="The clipboard is busy. Try again.";
        }
    }
    Window activeTaskPrompt;
    bool taskPromptNeedsRefresh;
    Button taskPromptButton;
    int? taskPromptList;
    string taskPromptPath;

    Button TaskPromptAction(){
        var button=SourceAction("Agent prompt",()=>{
            if(taskPromptList.HasValue)ShowTaskAgentPrompt(taskPromptList.Value,taskPromptPath);
        });
        button.Margin=new Thickness(9,0,0,0);
        button.Tag="task-agent-prompt";
        NameControl(button,"Agent prompt","task-agent-prompt");
        return button;
    }

    void UpdateTaskPromptButton(int? list,string path){
        taskPromptList=list;
        taskPromptPath=path;
        taskPromptButton.Visibility=list.HasValue&&preferenceStore.Value.showAgentPrompts==true
            ?Visibility.Visible:Visibility.Collapsed;
    }

    void FocusTaskPromptAction(){
        var trigger=taskPromptButton;
        if(trigger==null||!trigger.IsVisible)return;
        FocusManager.SetFocusedElement(window,trigger);
        if(window.IsActive)Keyboard.Focus(trigger);
    }

    void ShowTaskAgentPrompt(int list,string path){
        string prompt=list<0
            ?"Help me edit "+TaskLabel(false)+" in Local Dashboard. Ask what I want to add, change or remove. "+
                "Show me the proposed changes and get my approval. If you can use my desktop, select Tasks and choose Edit below the lists, then select "+TaskLabel(false)+
                ". Apply the approved changes in Dashboard and select Save. Preserve unrelated items and their checked state. "+
                "Without desktop access, give me the exact items and steps to enter in Dashboard."
            :"Help me edit "+TaskListLabel(list)+". The selected file is: "+path+
                "\r\nAsk what I want changed. Read the current file if you have access, or ask me to share its contents. "+
                "Preserve existing task identifiers and unrelated entries. Use a numbered title, a short description, and an optional Notes: section for longer details. "+
                "Show me the proposed changes and get my approval before saving. If you cannot edit the file, give me the exact text to paste.";
        var body=new DockPanel{Margin=new Thickness(24)};
        var footer=new StackPanel{HorizontalAlignment=HorizontalAlignment.Right,Orientation=Orientation.Horizontal};
        var status=WorkText("");
        status.Visibility=Visibility.Collapsed;
        var copy=StandardAction(ActionButton("Copy prompt",()=>{
            CopyAgentPrompt(prompt,status);status.Visibility=Visibility.Visible;
        }));
        copy.Width=112;copy.Margin=new Thickness(0,0,8,0);
        copy.IsDefault=true;footer.Children.Add(copy);
        var close=StandardAction(new Button{Content="Close",IsCancel=true});
        close.Width=112;close.Margin=new Thickness(0);footer.Children.Add(close);
        footer.Margin=new Thickness(0,12,0,0);
        DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        DockPanel.SetDock(status,Dock.Bottom);body.Children.Add(status);
        var intro=WorkText("Review the prompt, then copy it into your agent chat.");
        DockPanel.SetDock(intro,Dock.Top);body.Children.Add(intro);
        var preview=WorkText(prompt);
        preview.Margin=new Thickness(12);
        preview.SetResourceReference(TextBlock.ForegroundProperty,"ThemeF2F2F2");
        preview.Tag="agent-prompt-preview";
        var scroll=new ScrollViewer{Content=preview,Focusable=false,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        var frame=new Border{Child=scroll,BorderThickness=new Thickness(1)};
        frame.SetResourceReference(Border.BorderBrushProperty,"Theme4A4A4A");
        frame.SetResourceReference(Border.BackgroundProperty,"Theme252525");
        body.Children.Add(frame);
        var dialog=Dialog("Agent prompt",680,420,body);
        dialog.Topmost=window.Topmost;
        KeyboardNavigation.SetTabNavigation(body,KeyboardNavigationMode.Cycle);
        FocusManager.SetFocusedElement(dialog,copy);
        bool initialFocusSet=false;
        Action focus=()=>{
            if(initialFocusSet||!dialog.IsActive||!copy.IsVisible)return;
            initialFocusSet=Keyboard.Focus(copy)==copy;
        };
        dialog.Activated+=(s,e)=>dialog.Dispatcher.BeginInvoke(focus,DispatcherPriority.Input);
        dialog.ContentRendered+=(s,e)=>dialog.Dispatcher.BeginInvoke(focus,DispatcherPriority.Input);
        dialog.PreviewKeyDown+=(s,e)=>{
            if(e.Key==System.Windows.Input.Key.PageUp){scroll.PageUp();e.Handled=true;}
            else if(e.Key==System.Windows.Input.Key.PageDown){scroll.PageDown();e.Handled=true;}
        };
        if(!nonWindowedVerification)FocusTaskPromptAction();
        activeTaskPrompt=dialog;
        try{presentDialog(dialog);}
        finally{
            activeTaskPrompt=null;
            if(taskPromptNeedsRefresh){
                taskPromptNeedsRefresh=false;
                if(section=="work")RenderTaskItems();
            }
            if(!nonWindowedVerification)
                window.Dispatcher.BeginInvoke(new Action(FocusTaskPromptAction),DispatcherPriority.Input);
        }
    }
    void RenderSavedTaskList(int list){
        string status=taskReadStatus[list];
        if(!String.IsNullOrWhiteSpace(status))rows.Children.Add(WorkText(status));
        try{
            var source=WorkSources.Read(profile,list);var cache=WorkSources.Cached(profile,source);
            bool local=!CloudSources.IsCloud(source.kind);
            UpdateTaskPromptButton(local?(int?)list:null,local?source.path:null);
            if(cache==null){if(String.IsNullOrWhiteSpace(status))rows.Children.Add(WorkText("A file is selected. Choose Refresh to load it."));rows.Children.Add(TaskDetails("Source details",source.path));return;}
            if(String.IsNullOrWhiteSpace(cache.text))rows.Children.Add(WorkText("This text file is empty."));else RenderReadableTaskText(cache.text,rows,search.Text.Trim());
            var sourceDetails=new StackPanel();sourceDetails.Children.Add(WorkText("File: "+source.path));
            sourceDetails.Children.Add(WorkText("Read "+DateTimeOffset.Parse(cache.refreshedAt).ToLocalTime().ToString("g")));
            sourceDetails.Children.Add(WorkText("The complete document below keeps all headings, instructions and additional text."));
            sourceDetails.Children.Add(new TextBox{Text=cache.text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true});
            rows.Children.Add(new Expander{Header="Source and full document",IsExpanded=false,Content=sourceDetails,Margin=new Thickness(0,12,0,0)});
        }catch(TaskSourceNotSelectedException){
            UpdateTaskPromptButton(null,null);
            rows.Children.Add(WorkText("Choose an existing file or create one for "+TaskListLabel(list)+"."));
            rows.Children.Add(SourceAction("Set up this list",()=>Protect(()=>Settings("Tasks",list))));
        }
        catch(Exception error){UpdateTaskPromptButton(null,null);rows.Children.Add(WorkText(TaskListLabel(list)+" unavailable. "+error.Message));}
    }
}
