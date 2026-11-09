using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    static TextBox PersonalEditorField(StackPanel body,string label,string value,int height){
        body.Children.Add(WorkText(label,true));
        var field=new TextBox{Text=value??"",TextWrapping=TextWrapping.Wrap,AcceptsReturn=height>36,MinHeight=height,VerticalScrollBarVisibility=height>36?ScrollBarVisibility.Auto:ScrollBarVisibility.Disabled,Margin=new Thickness(0,0,0,12)};
        if(height>36)field.MaxHeight=240;
        System.Windows.Automation.AutomationProperties.SetName(field,label);body.Children.Add(field);return field;
    }
    void EditPersonalTask(string displayed,ReadableTaskBlock selected,bool wholeDocument=false){
        var snapshot=PersonalTextStore.Open(profile);
        if(selected!=null&&snapshot.Text!=displayed){personalReadStatus="The file changed since it was displayed. Select Refresh, then edit the latest task.";RenderCompact(true);return;}
        var draft=selected==null?null:new PersonalTaskDraft(snapshot.Text,selected);
        var content=new DockPanel{Margin=new Thickness(28)};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(footer,Dock.Bottom);content.Children.Add(footer);
        var body=new StackPanel{Margin=new Thickness(0,0,14,0)};content.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        string caption=wholeDocument?"Edit Personal tasks document":selected==null?"Add personal task":"Edit personal task";
        var dialog=Dialog(caption,700,710,content);
        body.Children.Add(WorkText(wholeDocument?"Edit your selected text file. Save keeps the changes; Cancel leaves the file alone.":selected==null?"Add a title and short description. Longer notes start collapsed. The new task goes above the first task; existing numbers stay unchanged.":"Edit this task. Other tasks and supplied Status/Next fields stay unchanged. Save keeps your changes."));
        var location=TaskDetails("File location",snapshot.Source.path);body.Children.Add(location);
        TextBox title=null,summary=null,notes=null,document=null;
        if(wholeDocument){document=PersonalEditorField(body,"Document text",snapshot.Text,360);document.MaxHeight=Double.PositiveInfinity;}
        else{
            title=PersonalEditorField(body,"Title",draft==null?"":draft.Title,36);
            if(draft==null||draft.Simple){summary=PersonalEditorField(body,"Brief description",draft==null?"":draft.Summary,80);notes=PersonalEditorField(body,"Longer notes (optional)",draft==null?"":draft.Notes,160);}
            else{body.Children.Add(WorkText("This task has additional formatting. Its complete text is editable below so none of your information is lost."));document=PersonalEditorField(body,"Task text",draft.Body,300);document.MaxHeight=Double.PositiveInfinity;}
        }
        var feedback=WorkText("");body.Children.Add(feedback);
        string initialDocument=document==null?null:document.Text;
        var save=StandardAction(ActionButton("Save",()=>{
            try{
                string text=wholeDocument?(document.Text==initialDocument?snapshot.Text:document.Text):draft==null?PersonalTaskDraft.Add(snapshot.Text,title.Text,summary.Text,notes.Text):draft.Apply(title.Text,summary==null?null:summary.Text,notes==null?null:notes.Text,document==null?null:document.Text==initialDocument?draft.Body:document.Text);
                personalReadStatus=PersonalTextStore.Save(profile,snapshot,text);dialog.Close();RenderCompact(true);
            }catch(Exception error){feedback.Text=error.Message;feedback.BringIntoView();}
        }));save.Tag="personal-save";footer.Children.Add(save);
        var cancel=StandardAction(ActionButton("Cancel",()=>dialog.Close()));cancel.IsCancel=true;footer.Children.Add(cancel);
        dialog.Loaded+=(s,e)=>(title??document).Focus();presentDialog(dialog);
    }
}
