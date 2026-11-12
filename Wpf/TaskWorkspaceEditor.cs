using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

internal sealed partial class WpfDashboard
{
    sealed class TaskSectionDraft:INotifyPropertyChanged
    {
        internal readonly int Id;
        internal bool Hidden;
        internal string SourceToken, SourcePath, NewPath, NewPathToken, Error, Text, ChecklistToken;
        internal bool Loaded, ChecklistChanged;
        internal PersonalTextSnapshot Snapshot;
        internal WorkDocument Checklist;
        string name;
        public string label { get { return name+(Hidden?" (hidden)":""); } }
        internal string Name { get { return name; } set { name=value; Changed(); } }
        internal bool ItemsChanged { get { return ChecklistChanged||Snapshot!=null&&Text!=Snapshot.Text; } }
        internal TaskSectionDraft(int id,string label,bool hidden){Id=id;name=label;Hidden=hidden;}
        public event PropertyChangedEventHandler PropertyChanged;
        internal void Changed(){if(PropertyChanged!=null)PropertyChanged(this,new PropertyChangedEventArgs("label"));}
    }

    static readonly int[] DefaultTaskTabs={0,1,2,-1};
    static IEnumerable<int> TaskTabIds(string stored)
    {
        return (stored??"").Split(',').Select(part=>{
            int value;
            return Int32.TryParse(part,out value)?value:Int32.MinValue;
        }).Where(value=>DefaultTaskTabs.Contains(value)).Distinct();
    }
    int[] VisibleTaskTabs()
    {
        var settings=preferenceStore.Value;
        var hidden=new HashSet<int>(TaskTabIds(settings.hiddenTaskTabs));
        var visible=TaskTabIds(settings.taskTabOrder).Concat(DefaultTaskTabs).Distinct()
            .Where(id=>!hidden.Contains(id)&&(id<=0||HasTaskListFile(id))).ToArray();
        return visible.Length==0?new[]{0}:visible;
    }

    void LoadTaskSection(TaskSectionDraft draft)
    {
        if(draft.Loaded)return;
        draft.Loaded=true;
        try{
            if(draft.Id<0){
                draft.SourceToken=ChecklistLocation.Token(ChecklistLocation.Config(profile));
                draft.SourcePath=ChecklistLocation.PathFor(profile);
                ChecklistLocation.RequireAvailable(profile);
                draft.ChecklistToken=ChecklistLocation.Token(draft.SourcePath);
                draft.Checklist=new ChecklistStore(draft.SourcePath,entries).Read();
            }else{
                draft.SourceToken=WorkSources.Token(profile,draft.Id);
                draft.SourcePath=WorkSources.Read(profile,draft.Id).path;
                draft.Snapshot=PersonalTextStore.Open(profile,draft.Id);
                draft.Text=draft.Snapshot.Text;
            }
        }catch(TaskSourceNotSelectedException){draft.Error="Choose a source to add and edit tasks.";}
        catch(Exception error){draft.Error=error.Message;}
    }

    void PickTaskEditorSource(Window owner,TaskSectionDraft draft)
    {
        if(draft.ItemsChanged)throw new IOException("Save or cancel your item changes before changing this list's source.");
        var picker=new OpenFileDialog{
            Title="Choose a source for "+draft.Name,CheckFileExists=true,
            Filter=draft.Id<0?"Dashboard checklist|*.json":"Text and Markdown|*.txt;*.md"
        };
        string current=draft.NewPath??draft.SourcePath;
        if(File.Exists(current))picker.FileName=current;
        if(picker.ShowDialog(owner)!=true)return;
        if(draft.Id<0){
            ChecklistLocation.Validate(profile,picker.FileName);
            new ChecklistStore(picker.FileName,entries).Read();
            draft.ChecklistToken=ChecklistLocation.Token(draft.SourcePath);
        }else WorkSources.Validate(LocalTaskChoice(picker.FileName));
        draft.NewPath=String.Equals(draft.SourcePath,picker.FileName,StringComparison.OrdinalIgnoreCase)?null:picker.FileName;
        draft.NewPathToken=ChecklistLocation.Token(picker.FileName);
    }

    void CheckTaskSectionDraft(TaskSectionDraft draft)
    {
        if(!draft.ItemsChanged&&draft.NewPath==null)return;
        if(draft.Id<0){
            if(ChecklistLocation.Token(ChecklistLocation.Config(profile))!=draft.SourceToken||
                ChecklistLocation.Token(draft.SourcePath)!=draft.ChecklistToken)
                throw new IOException("The checklist changed outside this editor. Your draft is still open.");
        }else{
            if(WorkSources.Token(profile,draft.Id)!=draft.SourceToken)
                throw new IOException(draft.Name+" changed sources. Your draft is still open.");
            if(draft.ItemsChanged&&ChecklistLocation.Token(draft.SourcePath)!=draft.Snapshot.Hash)
                throw new IOException(draft.Name+" changed outside Dashboard. Your draft is still open.");
        }
        if(draft.NewPath!=null&&ChecklistLocation.Token(draft.NewPath)!=draft.NewPathToken)
            throw new IOException("The new source changed. Choose it again before saving.");
    }

    void SaveTaskSectionDraft(TaskSectionDraft draft)
    {
        if(draft.ChecklistChanged){
            draft.Checklist=new ChecklistStore(draft.SourcePath,entries)
                .SaveDraft(draft.Checklist.revision,()=>ChecklistLocation.Token(draft.SourcePath)==draft.ChecklistToken,draft.Checklist.items);
            draft.ChecklistChanged=false;
            draft.ChecklistToken=ChecklistLocation.Token(draft.SourcePath);
        }else if(draft.ItemsChanged){
            SaveTaskText(draft.Snapshot,draft.Text);
            draft.Snapshot=PersonalTextStore.Open(profile,draft.Id);
            draft.Text=draft.Snapshot.Text;
        }
        if(draft.NewPath==null)return;
        if(draft.Id<0)ChecklistLocation.Choose(profile,draft.NewPath,draft.SourceToken,
            draft.ChecklistToken,draft.NewPathToken,entries);
        else WorkSources.Save(profile,LocalTaskChoice(draft.NewPath),draft.SourceToken,draft.Id);
        draft.NewPath=null;
        draft.Loaded=false;
        draft.Error=null;
        LoadTaskSection(draft);
    }

    Window DashboardEditorDialog(string title,UIElement body)
    {
        return Dialog(title,window.ActualWidth>0?window.ActualWidth:window.Width,
            window.ActualHeight>0?window.ActualHeight:window.Height,body);
    }

    void EditTaskWorkspace()
    {
        var saved=preferenceStore.Value;
        var hidden=new HashSet<int>(TaskTabIds(saved.hiddenTaskTabs));
        var drafts=new ObservableCollection<TaskSectionDraft>(TaskTabIds(saved.taskTabOrder).Concat(DefaultTaskTabs).Distinct()
            .Select(id=>new TaskSectionDraft(id,id<0?TaskLabel(false):TaskListLabel(id),hidden.Contains(id))));
        var body=new DockPanel{Margin=new Thickness(24)};
        var title=new TextBlock{Text="Edit Tasks",FontSize=27,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)};
        DockPanel.SetDock(title,Dock.Top);body.Children.Add(title);
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var feedback=WorkText("");DockPanel.SetDock(feedback,Dock.Bottom);body.Children.Add(feedback);
        var left=new Border{Width=306,Padding=new Thickness(0,0,20,0),Margin=new Thickness(0,0,22,12),BorderThickness=new Thickness(0,0,1,0)};
        left.SetResourceReference(Border.BorderBrushProperty,"Theme4A4A4A");
        DockPanel.SetDock(left,Dock.Left);body.Children.Add(left);
        var leftPanel=new DockPanel{Margin=new Thickness(0,0,0,12)};left.Child=leftPanel;
        var listsTitle=new TextBlock{Text="Lists",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)};
        DockPanel.SetDock(listsTitle,Dock.Top);leftPanel.Children.Add(listsTitle);
        var listDetails=new StackPanel{Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(listDetails,Dock.Bottom);leftPanel.Children.Add(listDetails);
        var listName=new TextBox{MaxLength=40};Field(listDetails,"List name",listName);
        var source=WorkText("");source.TextTrimming=TextTrimming.CharacterEllipsis;
        source.TextWrapping=TextWrapping.NoWrap;listDetails.Children.Add(source);
        var leftActions=new WrapPanel();listDetails.Children.Add(leftActions);
        var changeSource=StandardAction(new Button{Content="Change source…",Margin=new Thickness(0,0,8,8)});
        var visibility=StandardAction(new Button{Margin=new Thickness(0,0,0,8)});
        leftActions.Children.Add(changeSource);leftActions.Children.Add(visibility);
        var lists=new ListBox{ItemTemplate=ReorderTemplate("label"),ItemContainerStyle=EditorRowStyle(),ItemsSource=drafts};
        System.Windows.Automation.AutomationProperties.SetName(lists,"Task lists");
        leftPanel.Children.Add(lists);
        var right=new DockPanel{Margin=new Thickness(0,0,0,12)};body.Children.Add(right);
        var itemsHeading=new TextBlock{FontSize=22,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
        DockPanel.SetDock(itemsHeading,Dock.Top);right.Children.Add(itemsHeading);
        var itemActions=new WrapPanel{Margin=new Thickness(0,8,0,8)};
        DockPanel.SetDock(itemActions,Dock.Top);right.Children.Add(itemActions);
        var add=StandardAction(new Button{Content="Add item",Margin=new Thickness(0,0,8,8)});
        var edit=StandardAction(new Button{Content="Edit item",Margin=new Thickness(0,0,8,8)});
        var remove=StandardAction(new Button{Content="Remove item",Margin=new Thickness(0,0,8,8)});
        var document=StandardAction(new Button{Content="Edit document",Margin=new Thickness(0,0,0,8)});
        itemActions.Children.Add(add);itemActions.Children.Add(edit);itemActions.Children.Add(remove);itemActions.Children.Add(document);
        var detail=WorkText("");detail.MaxHeight=66;detail.TextTrimming=TextTrimming.CharacterEllipsis;
        DockPanel.SetDock(detail,Dock.Bottom);right.Children.Add(detail);
        var items=new ListBox{ItemTemplate=ReorderTemplate("Title"),ItemContainerStyle=EditorRowStyle()};
        System.Windows.Automation.AutomationProperties.SetName(items,"List items");
        right.Children.Add(items);
        var dialog=DashboardEditorDialog("Edit Tasks",body);
        TaskSectionDraft active=null;
        bool loading=false;
        Action updateButtons=()=>{
            bool available=active!=null&&active.Error==null&&active.NewPath==null;
            add.IsEnabled=available;edit.IsEnabled=remove.IsEnabled=available&&items.SelectedItem!=null;
            document.Visibility=active!=null&&active.Id>=0?Visibility.Visible:Visibility.Collapsed;
            document.IsEnabled=available;
        };
        Action loadItems=()=>{
            itemsHeading.Text="Items in "+active.Name;
            string path=active.NewPath??active.SourcePath;
            source.Text=path==null?"No source selected":"Source: "+Path.GetFileName(path);
            source.ToolTip=path;
            changeSource.Content=path==null?"Choose source…":"Change source…";
            changeSource.IsEnabled=!active.ItemsChanged;
            visibility.Content=active.Hidden?"Show in Tasks":"Remove from Tasks";
            detail.Text=active.NewPath!=null?"Save to switch to this source.":active.Error??"";
            if(active.Error!=null||active.NewPath!=null)items.ItemsSource=null;
            else if(active.Id<0)items.ItemsSource=new ObservableCollection<TaskEditorItem>(active.Checklist.items.Select(item=>new TaskEditorItem{
                Title=item.title+(item.state=="done"?" (Done)":""),Checklist=item}));
            else items.ItemsSource=new ObservableCollection<TaskEditorItem>(ReadableTasks.Parse(active.Text).Blocks.Where(block=>block.Kind=="task")
                .Select(block=>new TaskEditorItem{Title=block.Title,Task=block}));
            if(items.Items.Count>0)items.SelectedIndex=0;
            updateButtons();
        };
        lists.SelectionChanged+=(s,e)=>{
            var selected=lists.SelectedItem as TaskSectionDraft;
            if(selected==null)return;
            active=selected;LoadTaskSection(active);
            loading=true;listName.Text=active.Name;loading=false;loadItems();
        };
        listName.TextChanged+=(s,e)=>{if(!loading&&active!=null){active.Name=listName.Text;itemsHeading.Text="Items in "+active.Name;}};
        items.SelectionChanged+=(s,e)=>{
            updateButtons();
            var selected=items.SelectedItem as TaskEditorItem;
            if(selected!=null&&active.Error==null&&active.NewPath==null)
                detail.Text=selected.Checklist==null?selected.Task.Summary??"":selected.Checklist.details??"";
        };
        EnableEditorReorder(lists,(from,to)=>{
            var row=drafts[from];drafts.Move(from,to);lists.SelectedItem=row;
        });
        EnableEditorReorder(items,(from,to)=>{
            var itemRows=(ObservableCollection<TaskEditorItem>)items.ItemsSource;
            try{
                if(active.Id<0){
                    var item=active.Checklist.items[from];
                    active.Checklist.items.RemoveAt(from);
                    active.Checklist.items.Insert(to,item);
                    active.ChecklistChanged=true;
                }else{
                    active.Text=PersonalTaskDraft.Move(active.Text,from,to);
                }
                itemRows.Move(from,to);
                if(active.Id>=0){
                    var reordered=ReadableTasks.Parse(active.Text).Blocks.Where(block=>block.Kind=="task").ToArray();
                    for(int index=0;index<itemRows.Count;index++)itemRows[index].Task=reordered[index];
                }
                feedback.Text="";
            }catch(IOException error){feedback.Text=error.Message;}
        });
        visibility.Click+=(s,e)=>{
            if(active==null)return;
            active.Hidden=!active.Hidden;active.Changed();loadItems();
        };
        changeSource.Click+=(s,e)=>{
            try{PickTaskEditorSource(dialog,active);loadItems();}
            catch(Exception error){feedback.Text=error.Message;}
        };
        add.Click+=(s,e)=>{
            if(!EditTaskWorkspaceItem(dialog,active,null))return;
            loadItems();
            items.SelectedIndex=items.Items.Count-1;
            items.ScrollIntoView(items.SelectedItem);
        };
        edit.Click+=(s,e)=>{if(EditTaskWorkspaceItem(dialog,active,(TaskEditorItem)items.SelectedItem))loadItems();};
        document.Click+=(s,e)=>{if(EditTaskWorkspaceDocument(dialog,active))loadItems();};
        remove.Click+=(s,e)=>{
            var selected=(TaskEditorItem)items.SelectedItem;
            if(selected==null)return;
            if(!ShowNotice(dialog,"Remove item","Remove \""+selected.Title+"\"? This takes effect when you save.","Remove"))return;
            if(selected.Checklist!=null){active.Checklist.items.Remove(selected.Checklist);active.ChecklistChanged=true;}
            else active.Text=active.Text.Remove(selected.Task.Start,selected.Task.Length);
            loadItems();
        };
        var save=StandardAction(new Button{Content="Save",IsDefault=true,Margin=new Thickness(0,0,8,0)});
        var cancel=StandardAction(new Button{Content="Cancel",IsCancel=true});
        footer.Children.Add(save);footer.Children.Add(cancel);
        bool applied=false;
        save.Click+=(s,e)=>{
            var written=new List<string>();
            try{
                var names=drafts.ToDictionary(draft=>draft.Id,draft=>ValidTaskLabel(draft.Name));
                if(names.Values.Distinct(StringComparer.CurrentCultureIgnoreCase).Count()!=4)
                    throw new IOException("Give each list a different name.");
                if(!drafts.Any(draft=>!draft.Hidden&&(draft.Id<=0||draft.NewPath!=null||HasTaskListFile(draft.Id))))
                    throw new IOException("Keep at least one list visible in Tasks.");
                if(!preferenceStore.IsCurrent())throw new IOException("Task settings changed elsewhere. Your draft is still open.");
                var pending=drafts.Where(draft=>draft.ItemsChanged||draft.NewPath!=null).ToArray();
                if(pending.Where(draft=>draft.Id>=0&&draft.ItemsChanged)
                    .GroupBy(draft=>draft.SourcePath,StringComparer.OrdinalIgnoreCase).Any(group=>group.Count()>1))
                    throw new IOException("Two lists use the same task file. Edit that file from one list at a time.");
                foreach(var draft in pending)CheckTaskSectionDraft(draft);
                foreach(var draft in pending){SaveTaskSectionDraft(draft);written.Add(names[draft.Id]);applied=true;}
                var next=preferenceStore.Copy();
                next.agentTasksLabel=names[0];next.personalTasksLabel=names[1];
                next.thirdTaskListLabel=names[2];next.checklistLabel=names[-1];
                next.taskTabOrder=String.Join(",",drafts.Select(draft=>draft.Id));
                next.hiddenTaskTabs=String.Join(",",drafts.Where(draft=>draft.Hidden).Select(draft=>draft.Id));
                preferenceStore.Save(next,false);
                applied=true;
                if(!nonWindowedVerification)dialog.DialogResult=true;
            }catch(Exception error){
                feedback.Text=(written.Count==0?"":"Already saved: "+String.Join(", ",written)+". ")+error.Message;
            }
        };
        lists.SelectedItem=drafts.FirstOrDefault(draft=>draft.Id==taskListView)??drafts[0];
        presentDialog(dialog);
        if(applied){RenderCompact(true);RefreshDisplayedTasks(false);}
    }

    sealed class TaskEditorItem
    {
        public string Title{get;set;}
        internal WorkItem Checklist;
        internal ReadableTaskBlock Task;
    }

    bool EditTaskWorkspaceDocument(Window owner,TaskSectionDraft section)
    {
        var body=new DockPanel{Margin=new Thickness(24)};
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
        DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);
        var field=new TextBox{Text=section.Text,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,
            VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0)};
        string initialText=field.Text;
        NameControl(field,"Document text","task-document-text");
        body.Children.Add(field);
        var dialog=Dialog("Edit document",700,650,body);
        if(!nonWindowedVerification)dialog.Owner=owner;
        bool accepted=false;
        actions.Children.Add(StandardAction(ActionButton("Done",()=>{
            if(field.Text!=initialText)section.Text=field.Text;
            accepted=true;
            if(!nonWindowedVerification)dialog.DialogResult=true;
        })));
        actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        dialog.Loaded+=(s,e)=>field.Focus();
        presentDialog(dialog);
        return accepted;
    }

    bool EditTaskWorkspaceItem(Window owner,TaskSectionDraft section,TaskEditorItem item)
    {
        bool checklist=section.Id<0;
        var task=item==null||checklist?null:new PersonalTaskDraft(section.Text,item.Task);
        var check=item==null?null:item.Checklist;
        var body=new DockPanel{Margin=new Thickness(24)};
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(actions,Dock.Bottom);body.Children.Add(actions);
        var fields=new StackPanel{Margin=new Thickness(0,0,10,0)};
        body.Children.Add(new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});
        var title=PersonalEditorField(fields,"Title",task!=null?task.Title:check==null?"":check.title,36);
        var description=!checklist&&(task==null||task.Simple)?
            PersonalEditorField(fields,"Brief description",task==null?"":task.Summary,65):null;
        bool raw=task!=null&&!task.Simple;
        var notes=PersonalEditorField(fields,raw?"Task text":"Notes",raw?task.Body:task!=null?task.Notes:check==null?"":check.details,105);
        var feedback=WorkText("");fields.Children.Add(feedback);
        var dialog=Dialog(item==null?"Add item":"Edit item",640,470,body);
        if(!nonWindowedVerification)dialog.Owner=owner;
        bool accepted=false;
        actions.Children.Add(StandardAction(ActionButton("Done",()=>{
            try{
                if(checklist){
                    string value=title.Text.Trim();
                    if(value.Length==0||value.Length>240||value.Any(Char.IsControl)||
                        notes.Text.Length>2000||notes.Text.Any(character=>Char.IsControl(character)&&character!='\r'&&character!='\n'))
                        throw new IOException("Enter a title up to 240 characters and notes up to 2,000 characters.");
                    if(check==null){
                        if(section.Checklist.items.Count>=500)throw new IOException("This checklist already has 500 items.");
                        check=new WorkItem{id="personal-"+Guid.NewGuid().ToString("N"),state="current"};
                        section.Checklist.items.Add(check);
                    }
                    check.title=value;check.details=notes.Text;
                    check.updatedAt=DateTimeOffset.UtcNow.ToString("o");section.ChecklistChanged=true;
                }else section.Text=task==null?PersonalTaskDraft.Add(section.Text,title.Text,description.Text,notes.Text):
                    task.Apply(title.Text,description==null?null:description.Text,raw?null:notes.Text,raw?notes.Text:null);
                accepted=true;
                if(!nonWindowedVerification)dialog.DialogResult=true;
            }catch(Exception error){feedback.Text=error.Message;}
        })));
        actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        dialog.Loaded+=(s,e)=>title.Focus();
        presentDialog(dialog);
        return accepted;
    }
}
