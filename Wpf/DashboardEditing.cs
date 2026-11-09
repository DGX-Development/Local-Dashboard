using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

internal sealed partial class WpfDashboard
{
    void EditCatalog()
    {
        var draft=new CatalogDraft(Path.Combine(profile,"catalog.json"));
        var body=new DockPanel{Margin=new Thickness(24)};
        var title=new TextBlock{Text="Edit dashboard links",FontSize=27,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)};
        DockPanel.SetDock(title,Dock.Top);body.Children.Add(title);
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var left=new DockPanel{Width=245,Margin=new Thickness(0,0,20,12)};
        DockPanel.SetDock(left,Dock.Left);body.Children.Add(left);
        var picker=new ComboBox{ItemsSource=layout.items.Where(i=>SidebarStore.Known(i.id)).ToList(),SelectedValuePath="id",ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding label}'/></DataTemplate>")};
        System.Windows.Automation.AutomationProperties.SetName(picker,"Section");
        DockPanel.SetDock(picker,Dock.Top);left.Children.Add(picker);
        var list=new ListBox{DisplayMemberPath="name"};
        System.Windows.Automation.AutomationProperties.SetName(list,"Dashboard entries");
        left.Children.Add(list);
        var fields=new StackPanel();
        var empty=WorkText("No links in this category. Choose another category to edit its links.");
        empty.Visibility=Visibility.Collapsed;
        fields.Children.Add(empty);
        body.Children.Add(new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var name=new TextBox{MaxLength=120};
        Field(fields,"Display name",name);
        fields.Children.Add(new TextBlock{Text="Destination"});
        var destinations=new ListBox{DisplayMemberPath="label",MaxHeight=180,Margin=new Thickness(0,5,0,8)};
        System.Windows.Automation.AutomationProperties.SetName(destinations,"Destination");
        fields.Children.Add(destinations);
        var location=new TextBox();
        var icon=new TextBox();
        var browseLocation=ActionButton("Browse…",()=>Browse(location,destinations.SelectedItem as LinkTarget,false));
        var browseIcon=ActionButton("Browse…",()=>Browse(icon,destinations.SelectedItem as LinkTarget,true));
        Field(fields,"Target location",location,browseLocation);
        Field(fields,"Icon file (optional)",icon,browseIcon);
        var dialog=Dialog("Edit dashboard links",860,620,body);
        Entry selected=null;
        LinkTarget target=null;
        bool loading=false;
        var sections=picker.ItemsSource.Cast<SidebarItem>().Select(i=>new SidebarItem{id=i.id,label=i.id=="all"?"All entries":SectionLabel(i.id,i.label)}).ToList();
        picker.ItemsSource=sections;
        Action showTarget=()=>{
            target=destinations.SelectedItem as LinkTarget;
            location.Text=target==null?"":target.kind=="web"?target.url:target.path;
            location.IsEnabled=target!=null;
            browseLocation.IsEnabled=target!=null&&target.kind!="web";
        };
        list.SelectionChanged+=(s,e)=>{
            loading=true;
            selected=list.SelectedItem as Entry;
            name.IsEnabled=icon.IsEnabled=browseIcon.IsEnabled=selected!=null;
            name.Text=selected==null?"":selected.name;
            icon.Text=selected==null?"":selected.iconPath??"";
            destinations.ItemsSource=selected==null?null:selected.targets;
            destinations.SelectedIndex=selected!=null&&selected.targets.Count>0?0:-1;
            showTarget();
            loading=false;
        };
        destinations.SelectionChanged+=(s,e)=>{if(!loading){loading=true;showTarget();loading=false;}};
        name.TextChanged+=(s,e)=>{if(!loading&&selected!=null)selected.name=name.Text;};
        icon.TextChanged+=(s,e)=>{if(!loading&&selected!=null)selected.iconPath=icon.Text.Trim();};
        location.TextChanged+=(s,e)=>{
            if(loading||target==null)return;
            if(target.kind=="web")target.url=location.Text.Trim();
            else target.path=location.Text.Trim();
        };
        Action refreshEntries=()=>{
            var previous=selected;
            var active=picker.SelectedItem as SidebarItem;
            list.ItemsSource=draft.Entries.Where(i=>active!=null&&DashboardForm.EntryInSection(i,active.id))
                .OrderBy(i=>i.name,StringComparer.CurrentCultureIgnoreCase).ToList();
            if(previous!=null&&list.Items.Contains(previous))list.SelectedItem=previous;
            else if(list.Items.Count>0)list.SelectedIndex=0;
            else{selected=null;target=null;name.Text=icon.Text=location.Text="";name.IsEnabled=icon.IsEnabled=location.IsEnabled=browseIcon.IsEnabled=browseLocation.IsEnabled=false;destinations.ItemsSource=null;}
            empty.Visibility=list.Items.Count==0?Visibility.Visible:Visibility.Collapsed;
        };
        picker.SelectionChanged+=(s,e)=>refreshEntries();
        picker.SelectedItem=sections.FirstOrDefault(i=>i.id==section)
            ??sections.FirstOrDefault(i=>draft.Entries.Any(entry=>DashboardForm.EntryInSection(entry,i.id)))
            ??sections.FirstOrDefault();
        refreshEntries();
        footer.Children.Add(StandardAction(ActionButton("Save",()=>Protect(()=>{draft.Save();dialog.DialogResult=true;}))));
        footer.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        dialog.Loaded+=(s,e)=>picker.Focus();
        if(presentDialog(dialog)==true)Reload();
    }

    static void Field(Panel panel,string label,TextBox input,Button browse=null)
    {
        panel.Children.Add(new TextBlock{Text=label});
        System.Windows.Automation.AutomationProperties.SetName(input,label);
        var row=new DockPanel();
        if(browse!=null)
        {
            browse.Height=36;browse.MinWidth=84;browse.Margin=new Thickness(8,5,0,14);
            DockPanel.SetDock(browse,Dock.Right);
            row.Children.Add(browse);
        }
        row.Children.Add(input);
        panel.Children.Add(row);
    }

    void Browse(TextBox field,LinkTarget target,bool image)
    {
        var owner=Window.GetWindow(field)??window;
        if(!image&&target!=null&&target.kind=="folder")
        {
            using(var picker=new System.Windows.Forms.FolderBrowserDialog{SelectedPath=field.Text,ShowNewFolderButton=false})
                if(picker.ShowDialog(new PickerOwner(new System.Windows.Interop.WindowInteropHelper(owner).Handle))==System.Windows.Forms.DialogResult.OK)field.Text=picker.SelectedPath;
            return;
        }
        var dialog=new Microsoft.Win32.OpenFileDialog{CheckFileExists=true,
            Filter=image?"Images|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif":"Documents|*.md;*.txt;*.pdf;*.docx;*.xlsx;*.pptx;*.html"};
        if(File.Exists(field.Text))dialog.FileName=field.Text;
        else if(target!=null&&Directory.Exists(target.path))dialog.InitialDirectory=target.path;
        else if(target!=null&&File.Exists(target.path))dialog.InitialDirectory=Path.GetDirectoryName(target.path);
        else dialog.InitialDirectory=profile;
        if(dialog.ShowDialog(owner)==true)field.Text=dialog.FileName;
    }

    sealed class PickerOwner:System.Windows.Forms.IWin32Window
    {
        public IntPtr Handle{get;private set;}
        internal PickerOwner(IntPtr handle){Handle=handle;}
    }

    SidebarLayout moveDraft;
    bool Moving { get { return moveDraft!=null; } }

    void BeginMove()
    {
        if(Compact||Moving)return;
        sidebarSnapshot=sidebarStore.Read();
        moveDraft=SidebarStore.Copy(sidebarSnapshot.Layout);
        search.Clear();
        search.IsEnabled=false;
        RefreshNavigation();
        Render();
    }

    void FinishMove(bool save)
    {
        if(!Moving)return;
        if(save)sidebarStore.Save(sidebarSnapshot,moveDraft);
        moveDraft=null;
        search.IsEnabled=true;
        Reload();
    }

    void Reorder(StackPanel panel,string id,int offset)
    {
        var order=panel.Children.OfType<Border>().Select(row=>row.Tag as string).Where(key=>key!=null).ToList();
        int source=order.IndexOf(id), destination=source+offset;
        if(!Moving||source<0||destination<0||destination>=order.Count||source==destination)return;
        order.RemoveAt(source);
        order.Insert(destination,id);
        if(panel==rows)
        {
            List<string> previous;
            if(moveDraft.orders.TryGetValue(section,out previous))
                order.AddRange(previous.Where(key=>!order.Contains(key)).ToList());
            moveDraft.orders[section]=order;
        }
        else
        {
            var byId=moveDraft.items.ToDictionary(item=>item.id);
            moveDraft.items=order.Select(key=>byId[key]).Concat(moveDraft.items.Where(item=>!order.Contains(item.id))).ToList();
        }
        RefreshNavigation();
        Render();
        var updated=panel.Children.OfType<Border>().FirstOrDefault(row=>Equals(row.Tag,id));
        var handle=updated==null?null:((DockPanel)updated.Child).Children.OfType<Button>().FirstOrDefault(button=>Equals(button.Content,"⠿"));
        if(handle!=null){handle.BringIntoView();handle.Focus();}
    }

    void AddMoveHandle(Border row,DockPanel content,StackPanel panel,string id,string label)
    {
        var handle=new Button{Content="⠿",Width=36,Height=36,Padding=new Thickness(0),Margin=new Thickness(0,0,6,0),
            Cursor=Cursors.SizeAll,ToolTip="Drag to move, or press Ctrl+Up or Ctrl+Down"};
        System.Windows.Automation.AutomationProperties.SetName(handle,"Move "+label);
        content.Children.Insert(0,handle);
        row.Tag=id;
        row.AllowDrop=true;
        Point start=new Point();
        handle.PreviewMouseLeftButtonDown+=(s,e)=>start=e.GetPosition(handle);
        handle.PreviewMouseMove+=(s,e)=>{
            if(e.LeftButton!=MouseButtonState.Pressed||!handle.IsMouseCaptured)return;
            Point point=e.GetPosition(handle);
            if(Math.Abs(point.X-start.X)<SystemParameters.MinimumHorizontalDragDistance&&
               Math.Abs(point.Y-start.Y)<SystemParameters.MinimumVerticalDragDistance)return;
            handle.ReleaseMouseCapture();
            var drag=new RowDrag(panel,row,label);
            try{DragDrop.DoDragDrop(handle,new DataObject(typeof(RowDrag),drag),DragDropEffects.Move);}
            finally{drag.Clear();}
        };
        handle.PreviewKeyDown+=(s,e)=>{
            if((Keyboard.Modifiers&ModifierKeys.Control)==0||(e.Key!=Key.Up&&e.Key!=Key.Down))return;
            Reorder(panel,id,e.Key==Key.Up?-1:1);
            e.Handled=true;
        };
        row.DragOver+=(s,e)=>{
            var drag=e.Data.GetData(typeof(RowDrag)) as RowDrag;
            e.Effects=drag!=null&&drag.Panel==panel?DragDropEffects.Move:DragDropEffects.None;
            if(e.Effects==DragDropEffects.Move)drag.Track(row,e.GetPosition(row));
            e.Handled=true;
        };
        row.DragLeave+=(s,e)=>{
            var drag=e.Data.GetData(typeof(RowDrag)) as RowDrag;
            if(drag!=null&&drag.Target==row)drag.Clear();
        };
        row.Drop+=(s,e)=>{
            var drag=e.Data.GetData(typeof(RowDrag)) as RowDrag;
            if(drag==null||drag.Panel!=panel)return;
            var items=panel.Children.OfType<Border>().Where(item=>item.Tag is string).ToList();
            int source=items.IndexOf(drag.Source);
            int insertion=items.IndexOf(row)+(e.GetPosition(row).Y>=row.ActualHeight/2?1:0);
            drag.Clear();
            if(source>=0)
            {
                int offset=insertion-(insertion>source?1:0)-source;
                window.Dispatcher.BeginInvoke(new Action(()=>Reorder(panel,(string)drag.Source.Tag,offset)));
            }
            e.Handled=true;
        };
    }

    sealed class RowDrag
    {
        internal readonly StackPanel Panel;
        internal readonly Border Source;
        internal Border Target;
        readonly string label;
        System.Windows.Documents.AdornerLayer layer;
        MoveCue cue;
        ScrollViewer scroll;
        int direction;
        readonly System.Windows.Threading.DispatcherTimer timer;

        internal RowDrag(StackPanel panel,Border source,string label)
        {
            Panel=panel;Source=source;this.label=label;
            timer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(85)};
            timer.Tick+=(s,e)=>{if(direction<0)scroll.LineUp();else if(direction>0)scroll.LineDown();};
        }

        internal void Track(Border target,Point pointer)
        {
            if(Target!=target)
            {
                Clear();
                Target=target;
                layer=System.Windows.Documents.AdornerLayer.GetAdornerLayer(target);
                if(layer!=null){cue=new MoveCue(target,label);layer.Add(cue);}
                for(DependencyObject parent=target;parent!=null;parent=System.Windows.Media.VisualTreeHelper.GetParent(parent))
                    if(parent is ScrollViewer){scroll=(ScrollViewer)parent;break;}
            }
            if(cue!=null){cue.Pointer=pointer;cue.InvalidateVisual();}
            if(scroll!=null)
            {
                double y=target.TranslatePoint(pointer,scroll).Y;
                direction=y<24?-1:y>scroll.ActualHeight-24?1:0;
                if(direction==0)timer.Stop();else if(!timer.IsEnabled)timer.Start();
            }
        }

        internal void Clear()
        {
            timer.Stop();direction=0;scroll=null;
            if(layer!=null&&cue!=null)layer.Remove(cue);
            cue=null;layer=null;Target=null;
        }
    }

    sealed class MoveCue:System.Windows.Documents.Adorner
    {
        readonly string label;
        internal Point Pointer;
        internal MoveCue(Border row,string label):base(row){this.label=label;IsHitTestVisible=false;}
        protected override void OnRender(System.Windows.Media.DrawingContext drawing)
        {
            var element=(FrameworkElement)AdornedElement;
            var accent=(System.Windows.Media.Brush)element.FindResource("DashboardAccent");
            double y=Pointer.Y<element.ActualHeight/2?0:element.ActualHeight;
            drawing.DrawLine(new System.Windows.Media.Pen(accent,2),new Point(0,y),new Point(element.ActualWidth,y));
            var text=new System.Windows.Media.FormattedText(label,System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,new System.Windows.Media.Typeface("Segoe UI"),13,System.Windows.Media.Brushes.Black,
                System.Windows.Media.VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double x=Math.Max(0,Math.Min(Pointer.X+10,element.ActualWidth-text.Width-12));
            drawing.DrawRoundedRectangle(accent,null,new Rect(x,Pointer.Y+8,text.Width+12,text.Height+6),4,4);
            drawing.DrawText(text,new Point(x+6,Pointer.Y+11));
        }
    }
}
