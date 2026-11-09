using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    string ReportRoot {get{return verifying?profile:AppData.Root;}}
    Window reportsWindow;
    StackPanel reportsRows,lastReportPanel;
    void ShowReports(){
        if(reportsWindow!=null){reportsWindow.Activate();return;}
        var layout=new DockPanel{Margin=new Thickness(28)};
        var title=new TextBlock{Text="Agent updates",FontSize=27,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)};DockPanel.SetDock(title,Dock.Top);layout.Children.Add(title);
        var footer=new Grid{Margin=new Thickness(0,14,0,0)};DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        var refresh=CompactAction(ActionButton("Refresh",()=>RenderReportsInto(reportsRows,true)));refresh.HorizontalAlignment=HorizontalAlignment.Left;refresh.Margin=new Thickness(0);footer.Children.Add(refresh);
        var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)});close.Margin=new Thickness(0);footer.Children.Add(close);
        reportsRows=new StackPanel{Margin=new Thickness(0,0,14,0)};
        layout.Children.Add(new ScrollViewer{Content=reportsRows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        reportsWindow=Dialog("Agent updates",680,640,layout);RenderReportsInto(reportsRows,true);
        try{presentDialog(reportsWindow);}finally{reportsWindow=null;reportsRows=null;lastReportPanel=null;lastReportView=null;}
    }
    void AddReviewAction(StackPanel item,StatusTask task){
        if(task.review==null){if(task.state=="Waiting for review")item.Children.Add(WorkText("No verified review link supplied."));return;}
        try{
            ReviewLinks.Target(task.review);
            string destination=ReviewLinks.Destination(profile);
            item.Children.Add(WorkText("Review: "+task.review.title));
            item.Children.Add(WorkText(task.review.target));
            item.Children.Add(WorkText("Target verified by publisher: "+task.review.verifiedAt+". Web access may still require sign-in; it has not been checked by this app."));
            var result=WorkText("Save link to: "+destination);
            var action=SourceAction(String.Equals(destination.TrimEnd('\\'),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)?"Send to Desktop":"Save review link",()=>{
                try{string saved=ReviewLinks.Create(profile,task);result.Text="Shortcut saved: "+saved+". Review is still awaiting your decision.";}
                catch(Exception error){result.Text="Shortcut not saved. "+error.Message;}
            });action.ToolTip="Create a shortcut to this exact review in "+destination;
            item.Children.Add(action);item.Children.Add(result);
        }catch(Exception error){item.Children.Add(WorkText("Review link unavailable: "+error.Message));}
    }
    void ReviewDestination(){string expected=ReviewLinks.SettingsToken(profile);
        var panel=new DockPanel{Margin=new Thickness(28)};var content=new StackPanel();
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);
        panel.Children.Add(new ScrollViewer{Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        content.Children.Add(new TextBlock{Text="Review-link destination",FontSize=24});
        content.Children.Add(WorkText("Individual Save review link buttons use this folder. The Windows Desktop is the default. Saving a link does not approve the review or move its original files."));
        var input=new TextBox{Text=ReviewLinks.Destination(profile),Height=36,Padding=new Thickness(10,0,10,0),VerticalContentAlignment=VerticalAlignment.Center};content.Children.Add(input);
        var actions=new WrapPanel{Margin=new Thickness(0,16,0,0)};content.Children.Add(actions);
        var dialog=Dialog("Review-link destination",700,380,panel);
        actions.Children.Add(SourceAction("Use desktop",()=>input.Text=Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)));
        actions.Children.Add(SourceAction("Browse…",()=>{using(var picker=new System.Windows.Forms.FolderBrowserDialog()){picker.Description="Choose where review shortcuts will be saved";picker.SelectedPath=input.Text;if(picker.ShowDialog(new PickerOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK)input.Text=picker.SelectedPath;}}));
        footer.Children.Add(StandardAction(ActionButton("Save",()=>Protect(()=>{ReviewLinks.SaveDestination(profile,input.Text,expected);dialog.DialogResult=true;}))));
        footer.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        presentDialog(dialog);
    }
    void RenderReportsInto(StackPanel target,bool force){
        try{var feed=AgentFeed.Read(entries,ReportRoot);
            string signature=feed==null?"none":AppData.Json.Serialize(feed)+String.Join("",feed.tasks.Select(t=>AgentFeed.Fresh(feed,t,DateTimeOffset.UtcNow)));
            if(!force&&lastReportPanel==target&&signature==lastReportView)return;lastReportPanel=target;lastReportView=signature;target.Children.Clear();

            target.Children.Add(WorkText("Latest saved progress from participating agents. Look here for work ready for review or needing your attention. Refresh rereads saved updates; it does not contact agents or change their timestamps."));
            var location=WorkText("Source: project-local agent reports");location.ToolTip=Path.Combine(ReportRoot,".local","agent-reports");target.Children.Add(location);
            if(feed==null)target.Children.Add(WorkText("No updates have been supplied yet. Participating agents can publish their progress here."));
            else{target.Children.Add(WorkText(feed.SourceCount+" source(s)"+(feed.InvalidSources>0?" - "+feed.InvalidSources+" invalid source(s) skipped":"")));
                target.Children.Add(WorkText("Reported attention items: "+feed.tasks.Count(t=>t.state=="Waiting for review"||t.state=="Waiting for user"||t.state=="Blocked")+" (check timestamps; stale reports are not current confirmation)"));foreach(var task in feed.tasks.OrderBy(t=>t.state=="Waiting for review"?0:t.state=="Blocked"?1:t.state=="Waiting for user"?2:3).ThenByDescending(t=>AgentFeed.Time(t.updatedAt))){bool fresh=AgentFeed.Fresh(feed,task,DateTimeOffset.UtcNow);var item=new StackPanel{Margin=new Thickness(0,8,0,12)};item.Children.Add(WorkText(task.task,true));var state=WorkText((fresh?"Recently reported: ":"Stale - last reported: ")+task.state);if(fresh)state.SetResourceReference(TextBlock.ForegroundProperty,"DashboardAccent");item.Children.Add(state);item.Children.Add(WorkText(task.agent+" - "+AgentFeed.Project(task.projectPath)));item.Children.Add(WorkText("Source: "+task.Source.producer.name+" - "+AgentFeed.Stamp(task.updatedAt)));AddReviewAction(item,task);target.Children.Add(item);target.Children.Add(WorkRule());}
            }
        }catch(Exception error){target.Children.Clear();target.Children.Add(WorkText("Reports unavailable. "+error.Message));}
    }
}
