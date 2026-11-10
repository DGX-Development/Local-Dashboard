using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

internal sealed partial class WpfDashboard
{
    void EditCatalog()
    {
        var draft=new CatalogDraft(Path.Combine(profile,"catalog.json"));
        var snapshot=sidebarStore.Read();
        var groups=SidebarStore.Copy(snapshot.Layout);
        foreach(var item in layout.items.Where(item=>!groups.items.Any(saved=>saved.id==item.id)))
            groups.items.Add(new SidebarItem{id=item.id,label=item.label,icon=item.icon,iconPath=item.iconPath});
        bool categoriesChanged=false;
        var body=new DockPanel{Margin=new Thickness(24)};
        var title=new TextBlock{Text="Edit Dashboard",FontSize=27,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,18)};
        DockPanel.SetDock(title,Dock.Top);body.Children.Add(title);
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        DockPanel.SetDock(footer,Dock.Bottom);body.Children.Add(footer);
        var left=new Border{Width=286,Padding=new Thickness(0,0,20,0),Margin=new Thickness(0,0,22,12),BorderThickness=new Thickness(0,0,1,0)};
        left.SetResourceReference(Border.BorderBrushProperty,"Theme4A4A4A");
        DockPanel.SetDock(left,Dock.Left);body.Children.Add(left);
        var picker=new ListBox{MaxHeight=184,SelectedValuePath="id",ItemTemplate=ReorderTemplate("label")};
        System.Windows.Automation.AutomationProperties.SetName(picker,"Section");
        var categoryPanel=new StackPanel{Margin=new Thickness(0,0,0,12)};
        left.Child=new ScrollViewer{Content=categoryPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        categoryPanel.Children.Add(new TextBlock{Text="Categories",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,8)});
        categoryPanel.Children.Add(picker);
        var categoryDetails=new StackPanel{Margin=new Thickness(0,10,0,0)};
        categoryPanel.Children.Add(categoryDetails);
        var categoryName=new TextBox{MaxLength=60};
        Field(categoryDetails,"Category name",categoryName);
        var categoryIconRow=new WrapPanel();
        var categoryIcon=new Image{Width=24,Height=24,Margin=new Thickness(0,0,8,0)};
        var changeCategoryIcon=StandardAction(new Button{Content="Change category icon"});
        categoryIconRow.Children.Add(categoryIcon);categoryIconRow.Children.Add(changeCategoryIcon);
        categoryDetails.Children.Add(categoryIconRow);
        var categoryActions=new WrapPanel{Margin=new Thickness(0,8,0,4)};
        var addCategory=StandardAction(new Button{Content="Add category",Margin=new Thickness(0,0,8,8)});
        var removeCategory=StandardAction(new Button{Content="Remove category",Margin=new Thickness(0,0,0,8)});
        categoryActions.Children.Add(addCategory);categoryActions.Children.Add(removeCategory);
        categoryPanel.Children.Add(categoryActions);
        var right=new DockPanel{Margin=new Thickness(0,0,0,12)};
        body.Children.Add(right);
        var linksHeading=new TextBlock{FontSize=22,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap};
        DockPanel.SetDock(linksHeading,Dock.Top);right.Children.Add(linksHeading);
        var feedback=WorkText("");
        DockPanel.SetDock(feedback,Dock.Top);right.Children.Add(feedback);
        var linkActions=new WrapPanel{Margin=new Thickness(0,8,0,8)};
        DockPanel.SetDock(linkActions,Dock.Top);right.Children.Add(linkActions);
        var addLink=StandardAction(new Button{Content="Add link"});
        var removeLink=StandardAction(new Button{Content="Remove link"});
        linkActions.Children.Add(addLink);linkActions.Children.Add(removeLink);
        var list=new ListBox{ItemTemplate=ReorderTemplate("name"),MaxHeight=150,Margin=new Thickness(0,0,0,14)};
        System.Windows.Automation.AutomationProperties.SetName(list,"Dashboard entries");
        DockPanel.SetDock(list,Dock.Top);right.Children.Add(list);
        var empty=WorkText("");
        DockPanel.SetDock(empty,Dock.Top);right.Children.Add(empty);
        var fields=new StackPanel();
        right.Children.Add(new ScrollViewer{Content=fields,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        var name=new TextBox{MaxLength=120};
        Field(fields,"Name",name);
        fields.Children.Add(new TextBlock{Text="Opens"});
        var kinds=new[]{"folder","document","web"};
        var kind=new ComboBox{Margin=new Thickness(0,5,0,14),ItemsSource=new[]{"Folder","Document","Website"}};
        System.Windows.Automation.AutomationProperties.SetName(kind,"Opens");
        fields.Children.Add(kind);
        var destinationLabel=new TextBlock{Text="Destinations"};
        fields.Children.Add(destinationLabel);
        var destinations=new ListBox{DisplayMemberPath="label",MaxHeight=180,Margin=new Thickness(0,5,0,8)};
        System.Windows.Automation.AutomationProperties.SetName(destinations,"Destination");
        fields.Children.Add(destinations);
        var location=new TextBox();
        var removeDestination=StandardAction(new Button{Content="Remove destination",HorizontalAlignment=HorizontalAlignment.Left});
        fields.Children.Add(removeDestination);
        var browseLocation=ActionButton("Browse…",()=>Browse(location,destinations.SelectedItem as LinkTarget));
        var locationLabel=Field(fields,"Address or location",location,browseLocation);
        var linkIcon=new Image{Width=24,Height=24,Margin=new Thickness(0,0,8,0)};
        var changeLinkIcon=StandardAction(new Button{Content="Change link icon"});
        fields.Children.Add(WorkText("Link icon",true));
        var linkIconRow=new WrapPanel();linkIconRow.Children.Add(linkIcon);linkIconRow.Children.Add(changeLinkIcon);fields.Children.Add(linkIconRow);
        var dialog=Dialog("Edit Dashboard",920,720,body);
        Entry selected=null;
        LinkTarget target=null;
        bool loading=false;
        var sections=new ObservableCollection<SidebarItem>(groups.items.Where(item=>item.id!="all"&&SidebarStore.Known(item.id)));
        picker.ItemsSource=sections;
        bool loadingCategory=false;
        SidebarItem activeCategory=null;
        Action showCategory=()=>{
            loadingCategory=true;
            activeCategory=picker.SelectedItem as SidebarItem;
            bool editable=activeCategory!=null&&activeCategory.id!="all";
            categoryDetails.Visibility=removeCategory.Visibility=editable?Visibility.Visible:Visibility.Collapsed;
            addLink.Visibility=editable?Visibility.Visible:Visibility.Collapsed;
            linksHeading.Text=editable?"Links in "+activeCategory.label:"Links";
            categoryName.Text=activeCategory==null?"":activeCategory.label;
            categoryIcon.Source=activeCategory==null?null:CategoryIcon(activeCategory);
            loadingCategory=false;
        };
        categoryName.TextChanged+=(s,e)=>{
            if(!loadingCategory&&activeCategory!=null&&activeCategory.id!="all")
            {
                activeCategory.label=categoryName.Text;
                UpdateEditorRowLabel(picker);
                linksHeading.Text="Links in "+activeCategory.label;
                categoriesChanged=true;
            }
        };

        changeCategoryIcon.Click+=(s,e)=>Protect(()=>{
            var item=activeCategory;
            if(item!=null&&item.id!="all")ChooseDashboardIcon(dialog,item.icon,item.iconPath,(glyph,path)=>{
                item.icon=glyph;item.iconPath=path;categoryIcon.Source=CategoryIcon(item);categoriesChanged=true;
            });
        });
        Action showTarget=()=>{
            target=destinations.SelectedItem as LinkTarget;
            kind.SelectedIndex=target==null?-1:Array.IndexOf(kinds,target.kind);
            kind.IsEnabled=target!=null;
            destinationLabel.Visibility=destinations.Visibility=removeDestination.Visibility=selected!=null&&selected.targets.Count>1?Visibility.Visible:Visibility.Collapsed;
            locationLabel.Text=target==null?"Location":target.kind=="web"?"Website address":target.kind=="folder"?"Folder":"Document";
            System.Windows.Automation.AutomationProperties.SetName(location,locationLabel.Text);
            location.Text=target==null?"":target.kind=="web"?target.url:target.path;
            location.IsEnabled=target!=null;
            browseLocation.Visibility=target!=null&&target.kind!="web"?Visibility.Visible:Visibility.Collapsed;
        };
        list.SelectionChanged+=(s,e)=>{
            loading=true;
            selected=list.SelectedItem as Entry;
            removeLink.IsEnabled=selected!=null;
            removeLink.Visibility=fields.Visibility=selected!=null?Visibility.Visible:Visibility.Collapsed;
            name.IsEnabled=changeLinkIcon.IsEnabled=selected!=null;
            name.Text=selected==null?"":selected.name;
            linkIcon.Source=selected==null?null:EntryIcon(selected);
            destinations.ItemsSource=selected==null?null:selected.targets;
            destinations.SelectedIndex=selected!=null&&selected.targets.Count>0?0:-1;
            showTarget();
            loading=false;
        };
        destinations.SelectionChanged+=(s,e)=>{if(!loading){loading=true;showTarget();loading=false;}};
        kind.SelectionChanged+=(s,e)=>{
            if(loading||target==null||!kind.IsEnabled)return;
            int index=kind.SelectedIndex;if(index<0)return;string value=kinds[index];if(value==target.kind)return;
            string previousKind=target.kind;
            target.kind=value;
            if(previousKind=="web"||value=="web")
            {
                target.path=value=="web"?null:"";
                target.url=value=="web"?"":null;
            }
            loading=true;var changedTarget=target;destinations.Items.Refresh();destinations.SelectedItem=changedTarget;showTarget();loading=false;
        };
        name.TextChanged+=(s,e)=>{if(!loading&&selected!=null){selected.name=name.Text;UpdateEditorRowLabel(list);}};

        changeLinkIcon.Click+=(s,e)=>Protect(()=>{
            var entry=selected;
            if(entry!=null)ChooseDashboardIcon(dialog,entry.iconGlyph,entry.iconPath,(glyph,path)=>{
                entry.iconGlyph=glyph;entry.iconPath=path;entry.iconAsset=null;linkIcon.Source=EntryIcon(entry);
            });
        });
        removeDestination.Click+=(s,e)=>{
            if(selected==null||target==null||selected.targets.Count<=1)return;
            if(MessageBox.Show(dialog,"Remove this destination from the link?","Remove destination",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            loading=true;
            int index=destinations.SelectedIndex;
            selected.targets.Remove(target);
            destinations.Items.Refresh();
            destinations.SelectedIndex=Math.Min(index,selected.targets.Count-1);
            showTarget();
            loading=false;
        };
        location.TextChanged+=(s,e)=>{
            if(loading||target==null)return;
            if(target.kind=="web")target.url=location.Text.Trim();
            else target.path=location.Text.Trim();
        };
        Action refreshEntries=()=>{
            var previous=selected;
            var active=picker.SelectedItem as SidebarItem;
            list.ItemsSource=new ObservableCollection<Entry>(OrderedDashboardEntries(
                draft.Entries.Where(i=>active!=null&&DashboardForm.EntryInSection(i,active.id)),groups,active==null?"":active.id));
            if(previous!=null&&list.Items.Contains(previous))list.SelectedItem=previous;
            else if(list.Items.Count>0)list.SelectedIndex=0;
            else{selected=null;target=null;name.Text=location.Text="";linkIcon.Source=null;name.IsEnabled=changeLinkIcon.IsEnabled=removeLink.IsEnabled=false;destinations.ItemsSource=null;showTarget();}
            bool hasLinks=list.Items.Count>0;
            list.Visibility=hasLinks?Visibility.Visible:Visibility.Collapsed;
            fields.Visibility=removeLink.Visibility=selected!=null?Visibility.Visible:Visibility.Collapsed;
            empty.Text=active==null?"Choose a category on the left to see its links.":"No links in this category. Choose Add link to create one.";
            empty.Visibility=hasLinks?Visibility.Collapsed:Visibility.Visible;
        };
        Action<SidebarItem> refreshSections=item=>{
            picker.SelectedItem=item;
            addLink.IsEnabled=sections.Any(group=>group.id!="all");
        };
        addCategory.Click+=(s,e)=>Protect(()=>{
            if(groups.items.Count>=100)throw new IOException("Dashboard supports up to 100 sidebar categories.");
            var item=new SidebarItem{id="category-"+Guid.NewGuid().ToString("N"),label="New category"};
            int all=groups.items.FindIndex(group=>group.id=="all");
            groups.items.Insert(all<0?groups.items.Count:all,item);
            sections.Add(item);
            categoriesChanged=true;refreshSections(item);categoryName.Focus();categoryName.SelectAll();
        });
        removeCategory.Click+=(s,e)=>Protect(()=>{
            var item=activeCategory;
            if(item==null||item.id=="all")return;
            var linked=draft.Entries.Where(entry=>entry.section==item.id).ToList();
            var others=sections.Where(group=>group!=item&&group.id!="all").ToList();
            SidebarItem destination=null;
            if(linked.Count>0)
            {
                if(others.Count==0)throw new IOException("Add another category before moving these links.");
                destination=ChooseCategoryDestination(dialog,item,linked.Count,others);
                if(destination==null)return;
                foreach(var entry in linked)entry.section=destination.id;
            }
            else if(MessageBox.Show(dialog,"Remove \""+item.label+"\"? This takes effect when you save.","Remove category",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            groups.items.Remove(item);groups.orders.Remove(item.id);groups.customized=true;
            sections.Remove(item);categoriesChanged=true;
            refreshSections(destination??others.FirstOrDefault());
            if(destination!=null)feedback.Text=linked.Count+" "+(linked.Count==1?"link":"links")+" moved here in this draft. Click Save to keep the move.";
        });
        addLink.IsEnabled=sections.Any(item=>item.id!="all");
        addLink.Click+=(s,e)=>Protect(()=>{
            var active=picker.SelectedItem as SidebarItem;
            if(active==null)return;
            var added=draft.Add(active.id,"folder");
            picker.SelectedItem=active;refreshEntries();list.SelectedItem=added;
            name.Focus();name.SelectAll();
        });
        removeLink.Click+=(s,e)=>{
            if(selected==null)return;
            if(MessageBox.Show(dialog,"Remove \""+selected.name+"\" from Dashboard?\n\nThe linked file, folder or website will not be deleted. This change takes effect when you save.","Remove link",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            draft.Remove(selected);selected=null;refreshEntries();
        };
        EnableEditorReorder(picker,(from,to)=>{
            var moved=sections[from];
            sections.Move(from,to);
            groups.items.Remove(moved);
            int index=to+1<sections.Count?groups.items.IndexOf(sections[to+1]):groups.items.FindIndex(item=>item.id=="all");
            groups.items.Insert(index<0?groups.items.Count:index,moved);
            categoriesChanged=true;
        });
        EnableEditorReorder(list,(from,to)=>{
            var active=picker.SelectedItem as SidebarItem;
            var items=(ObservableCollection<Entry>)list.ItemsSource;
            items.Move(from,to);
            var order=items.Select(item=>item.id).ToList();
            List<string> previous;
            if(groups.orders.TryGetValue(active.id,out previous))
                order.AddRange(previous.Where(id=>!order.Contains(id)));
            groups.orders[active.id]=order;
            categoriesChanged=true;
        });
        picker.SelectionChanged+=(s,e)=>{feedback.Text="";showCategory();refreshEntries();};
        picker.SelectedItem=sections.FirstOrDefault(i=>i.id==section);
        showCategory();
        refreshEntries();
        footer.Children.Add(StandardAction(ActionButton("Save",()=>Protect(()=>{
            if(categoriesChanged&&groups.items.Any(item=>!SidebarStore.ValidLabel(item.label)))
                throw new IOException("Enter a category name of 1 to 60 characters.");
            DashboardData.SaveDashboardEdit(profile,draft,sidebarStore,snapshot,categoriesChanged?groups:null);
            if(!nonWindowedVerification)dialog.DialogResult=true;
        }))));
        footer.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        dialog.Loaded+=(s,e)=>picker.Focus();
        if(presentDialog(dialog)==true)
        {
            if(activeCategory!=null&&categoriesChanged)section=activeCategory.id;
            Reload();
        }
    }

    SidebarItem ChooseCategoryDestination(Window editor,SidebarItem category,int count,List<SidebarItem> options)
    {
        var content=new StackPanel{Margin=new Thickness(28)};
        content.Children.Add(WorkText("Move "+count+" "+(count==1?"link":"links")+" from "+category.label+" to:"));
        var picker=new ComboBox{ItemsSource=options,SelectedIndex=0,ItemTemplate=(DataTemplate)System.Windows.Markup.XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><TextBlock Text='{Binding label}'/></DataTemplate>")};
        System.Windows.Automation.AutomationProperties.SetName(picker,"Destination category");
        content.Children.Add(picker);
        content.Children.Add(WorkText("The category is removed and its links are moved when you save Edit Dashboard."));
        var dialog=Dialog("Remove category",560,340,content);
        if(!nonWindowedVerification)dialog.Owner=editor;
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
        actions.Children.Add(StandardAction(ActionButton("Move links",()=>{if(!nonWindowedVerification)dialog.DialogResult=true;})));
        actions.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        content.Children.Add(actions);
        return presentDialog(dialog)==true?picker.SelectedItem as SidebarItem:null;
    }

    static System.Windows.Media.ImageSource CategoryIcon(SidebarItem item)
    {
        string glyph=HasDashboardGlyph(item.icon)?item.icon:SidebarStore.DefaultIcon(item.id);
        if(!String.IsNullOrEmpty(item.iconPath)&&File.Exists(item.iconPath))
        {
            var local=EntryIcon(new Entry{iconPath=item.iconPath});
            if(local!=null)return local;
        }
        return GlyphSource(glyph);
    }

    void ChooseDashboardIcon(Window editor,string currentGlyph,string currentPath,Action<string,string> apply)
    {
        var root=new DockPanel{Margin=new Thickness(28)};
        var footer=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right};
        var footerArea=new Border{Tag="icon-picker-footer",Child=footer,BorderThickness=new Thickness(0,1,0,0),
            Margin=new Thickness(0,14,0,0),Padding=new Thickness(0,14,0,0)};
        footerArea.SetResourceReference(Border.BorderBrushProperty,"Theme4A4A4A");
        DockPanel.SetDock(footerArea,Dock.Bottom);root.Children.Add(footerArea);
        var searchLabel=WorkText("Search built-in icons",true);
        DockPanel.SetDock(searchLabel,Dock.Top);root.Children.Add(searchLabel);
        var filter=new TextBox();
        System.Windows.Automation.AutomationProperties.SetName(filter,"Search built-in icons");
        DockPanel.SetDock(filter,Dock.Top);root.Children.Add(filter);
        var modes=new WrapPanel{Margin=new Thickness(0,0,0,12)};
        DockPanel.SetDock(modes,Dock.Top);root.Children.Add(modes);
        var common=StandardAction(new Button{Content="Common"});
        common.Margin=new Thickness(0,0,8,0);
        var all=StandardAction(new Button{Content="All icons ("+pickerArt.Value.Count+")"});
        all.Margin=new Thickness(0);
        modes.Children.Add(common);modes.Children.Add(all);
        var icons=new StackPanel();
        var scroll=new ScrollViewer{Tag="icon-picker-grid",Content=icons,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
        root.Children.Add(scroll);
        var dialog=Dialog("Choose an icon",520,550,root);
        if(!nonWindowedVerification)dialog.Owner=editor;
        string selectedGlyph=currentGlyph,selectedPath=currentPath;
        Action<string,string> select=(glyph,path)=>{selectedGlyph=glyph;selectedPath=path;if(!nonWindowedVerification)dialog.DialogResult=true;};
        var choices=new[]{
            new{Key="categories",Label="Projects"},new{Key="folder",Label="Folder"},
            new{Key="dev-guide",Label="Documents"},new{Key="globe",Label="Websites"},
            new{Key="dashboard",Label="Workspace"},new{Key="reports",Label="Reports"},
            new{Key="checklist",Label="Tasks"},new{Key="schedule",Label="Calendar"},
            new{Key="workflow",Label="Workflow"},new{Key="settings",Label="Tools"},
            new{Key="favorite",Label="Favorites"},new{Key="pin",Label="Reference"},
            new{Key="music",Label="Music"},new{Key="play",Label="Video"},
            new{Key="fluent:archive",Label="Archive"}
        };
        bool showAll=false;
        Func<System.Windows.Controls.Primitives.UniformGrid> makeGrid=()=>{
            var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=10};
            grid.SizeChanged+=(s,e)=>{
                int columns=Math.Max(1,(int)(e.NewSize.Width/74));
                if(grid.Columns!=columns)grid.Columns=columns;
            };
            return grid;
        };
        Action<Panel,string,string> addTile=(grid,key,label)=>{
            var content=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};
            content.Children.Add(GlyphImage(key));
            content.Children.Add(new TextBlock{Text=label,Width=64,FontSize=11,TextAlignment=TextAlignment.Center,
                TextWrapping=TextWrapping.NoWrap,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)});
            var tile=ActionButton(label,()=>select(key,null));
            tile.Content=content;tile.Height=52;tile.Padding=new Thickness(2);
            tile.Margin=new Thickness(2,0,2,4);
            tile.ToolTip=label;
            System.Windows.Automation.AutomationProperties.SetName(tile,label);
            SetNavigationSelected(tile,key==currentGlyph&&String.IsNullOrEmpty(currentPath));
            grid.Children.Add(tile);
        };
        Action refresh=()=>{
            string query=filter.Text.Trim();
            bool expanded=showAll||query.Length>0;
            SetNavigationSelected(common,!expanded);
            SetNavigationSelected(all,expanded);
            icons.Children.Clear();
            if(!expanded)
            {
                var grid=new System.Windows.Controls.Primitives.UniformGrid{Columns=5};
                foreach(var choice in choices)addTile(grid,choice.Key,choice.Label);
                icons.Children.Add(grid);
            }
            else
            {
                var grid=makeGrid();
                foreach(var icon in pickerArt.Value.Values.Where(icon=>icon.Search.IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(icon=>icon.Label))
                    addTile(grid,icon.Key,icon.Label);
                if(grid.Children.Count>0)icons.Children.Add(grid);
            }
            if(icons.Children.Count==0)icons.Children.Add(WorkText("No icons match your search."));
            scroll.ScrollToTop();
        };
        common.Click+=(s,e)=>{showAll=false;filter.Clear();refresh();};
        all.Click+=(s,e)=>{showAll=true;filter.Clear();refresh();};
        filter.TextChanged+=(s,e)=>refresh();
        refresh();
        footer.Children.Add(StandardAction(ActionButton("Use default icon",()=>select(null,null))));
        footer.Children.Add(StandardAction(ActionButton("Browse local image…",()=>Protect(()=>{
            var picker=new Microsoft.Win32.OpenFileDialog{Title="Choose a local icon",CheckFileExists=true,
                Filter="Images|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif",FileName=currentPath??""};
            if(picker.ShowDialog(dialog)!=true)return;
            if(EntryIcon(new Entry{iconPath=picker.FileName})==null)throw new IOException("This image could not be opened.");
            select(null,picker.FileName);
        }))));
        footer.Children.Add(StandardAction(new Button{Content="Cancel",IsCancel=true}));
        if(presentDialog(dialog)==true)apply(selectedGlyph,selectedPath);
    }

    static TextBlock Field(Panel panel,string label,TextBox input,Button browse=null)
    {
        var caption=new TextBlock{Text=label};
        panel.Children.Add(caption);
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
        return caption;
    }

    void Browse(TextBox field,LinkTarget target)
    {
        var owner=Window.GetWindow(field)??window;
        if(target!=null&&target.kind=="folder")
        {
            using(var picker=new System.Windows.Forms.FolderBrowserDialog{SelectedPath=field.Text,ShowNewFolderButton=false})
                if(picker.ShowDialog(new PickerOwner(new System.Windows.Interop.WindowInteropHelper(owner).Handle))==System.Windows.Forms.DialogResult.OK)field.Text=picker.SelectedPath;
            return;
        }
        var dialog=new Microsoft.Win32.OpenFileDialog{CheckFileExists=true,
            Filter="Documents|*.md;*.txt;*.pdf;*.docx;*.xlsx;*.pptx;*.html"};
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


    IEnumerable<Entry> OrderedDashboardEntries(IEnumerable<Entry> candidates,SidebarLayout groups,string category)
    {
        List<string> order;
        if(!groups.orders.TryGetValue(category,out order))order=new List<string>();
        return candidates.OrderBy(entry=>{
            int index=order.IndexOf(entry.id);
            return index>=0?index:order.Count==0&&pins.Contains(entry.id)?-1:Int32.MaxValue;
        }).ThenBy(entry=>entry.name,StringComparer.CurrentCultureIgnoreCase);
    }

    static DataTemplate ReorderTemplate(string label)
    {
        return (DataTemplate)System.Windows.Markup.XamlReader.Parse(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>"+
            "<DockPanel><Button DockPanel.Dock='Left' Tag='editor-grip' Content='&#x283f;' Width='28' Height='28' "+
            "Padding='0' Margin='0,0,8,0' VerticalAlignment='Center' Cursor='SizeNS' "+
            "ToolTip='Drag to reorder, or press Ctrl+Up or Ctrl+Down' AutomationProperties.Name='{Binding "+label+", Mode=OneWay}'/>"+
            "<TextBlock Tag='editor-label' Text='{Binding "+label+"}' VerticalAlignment='Center' TextTrimming='CharacterEllipsis'/></DockPanel></DataTemplate>");
    }

    static void UpdateEditorRowLabel(ListBox list)
    {
        var row=list.ItemContainerGenerator.ContainerFromItem(list.SelectedItem) as ListBoxItem;
        if(row==null)return;
        var label=Visuals<TextBlock>(row).FirstOrDefault(text=>Equals(text.Tag,"editor-label"));
        if(label!=null)label.GetBindingExpression(TextBlock.TextProperty).UpdateTarget();
        var grip=Visuals<Button>(row).FirstOrDefault(button=>Equals(button.Tag,"editor-grip"));
        if(grip!=null)grip.GetBindingExpression(System.Windows.Automation.AutomationProperties.NameProperty).UpdateTarget();
    }

    static Button EditorGrip(DependencyObject source,ListBox list)
    {
        for(var element=source;element!=null&&element!=list;element=element is Visual?VisualTreeHelper.GetParent(element):LogicalTreeHelper.GetParent(element))
        {
            var button=element as Button;
            if(button!=null&&Equals(button.Tag,"editor-grip"))return button;
        }
        return null;
    }

    static void EnableEditorReorder(ListBox list,Action<int,int> move)
    {
        ScrollViewer.SetCanContentScroll(list,false);
        list.CommandBindings.Add(new CommandBinding(ComponentCommands.MoveUp,(s,e)=>MoveEditorSelection(list,move,-1)));
        list.CommandBindings.Add(new CommandBinding(ComponentCommands.MoveDown,(s,e)=>MoveEditorSelection(list,move,1)));
        list.PreviewKeyDown+=(s,e)=>{
            if(Keyboard.Modifiers!=ModifierKeys.Control||(e.Key!=Key.Up&&e.Key!=Key.Down))return;
            var grip=EditorGrip(e.OriginalSource as DependencyObject,list);
            if(grip==null)return;
            list.SelectedItem=grip.DataContext;
            (e.Key==Key.Up?ComponentCommands.MoveUp:ComponentCommands.MoveDown).Execute(null,list);
            e.Handled=true;
        };

        ListBoxItem dragged=null;
        Point start=new Point();
        int from=-1,to=-1;
        var shifted=new List<ListBoxItem>();
        bool moving=false;
        ScrollViewer scroll=null;
        double startOffset=0;
        var autoScroll=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        Action clear=()=>{
            autoScroll.Stop();
            foreach(var row in shifted)row.ClearValue(UIElement.RenderTransformProperty);
            shifted.Clear();
            if(dragged!=null)dragged.ClearValue(Panel.ZIndexProperty);
            dragged=null;
            moving=false;
            if(list.IsMouseCaptured)list.ReleaseMouseCapture();
        };
        list.PreviewMouseLeftButtonDown+=(s,e)=>{
            var grip=EditorGrip(e.OriginalSource as DependencyObject,list);
            if(grip==null)return;
            dragged=ItemsControl.ContainerFromElement(list,grip) as ListBoxItem;
            if(dragged==null)return;
            from=to=list.ItemContainerGenerator.IndexFromContainer(dragged);
            start=e.GetPosition(list);
            scroll=Visuals<ScrollViewer>(list).FirstOrDefault();
            startOffset=scroll==null?0:scroll.VerticalOffset;
            grip.Focus();
            if(!list.CaptureMouse()){dragged=null;return;}
            e.Handled=true;
        };
        Action<double> preview=y=>{
            if(dragged==null||!list.IsMouseCaptured)return;
            double delta=y-start.Y+(scroll==null?0:scroll.VerticalOffset-startOffset);
            if(!moving&&Math.Abs(delta)<SystemParameters.MinimumVerticalDragDistance)return;
            moving=true;
            foreach(var row in shifted)row.ClearValue(UIElement.RenderTransformProperty);
            shifted.Clear();
            double center=dragged.TranslatePoint(new Point(0,dragged.ActualHeight/2),list).Y+delta;
            to=from;
            for(int index=0;index<list.Items.Count;index++)
            {
                var row=list.ItemContainerGenerator.ContainerFromIndex(index) as ListBoxItem;
                if(row==null||row==dragged)continue;
                double midpoint=row.TranslatePoint(new Point(0,row.ActualHeight/2),list).Y;
                if((index<from&&center<midpoint)||(index>from&&center>midpoint))
                {
                    to=index<from?Math.Min(to,index):Math.Max(to,index);
                    row.RenderTransform=new TranslateTransform(0,(index<from?1:-1)*(dragged.ActualHeight+dragged.Margin.Top+dragged.Margin.Bottom));
                    shifted.Add(row);
                }
            }
            Panel.SetZIndex(dragged,1);
            dragged.RenderTransform=new TranslateTransform(0,delta);
            shifted.Add(dragged);
            if(scroll!=null&&!autoScroll.IsEnabled)autoScroll.Start();
        };
        list.PreviewMouseMove+=(s,e)=>{
            if(dragged==null)return;
            preview(e.GetPosition(list).Y);
            e.Handled=true;
        };
        autoScroll.Tick+=(s,e)=>{
            var position=Mouse.GetPosition(scroll);
            if(position.X<0||position.X>scroll.ActualWidth)return;
            double offset=scroll.VerticalOffset;
            if(position.Y<20)scroll.ScrollToVerticalOffset(offset-16);
            else if(position.Y>scroll.ActualHeight-20)scroll.ScrollToVerticalOffset(offset+16);
            else return;
            scroll.UpdateLayout();
            preview(Mouse.GetPosition(list).Y);
        };
        list.PreviewMouseLeftButtonUp+=(s,e)=>{
            if(dragged==null)return;
            var item=list.Items[from];
            bool apply=moving&&to!=from;
            clear();
            if(apply)move(from,to);
            list.SelectedItem=item;
            e.Handled=true;
        };
        list.LostMouseCapture+=(s,e)=>{if(dragged!=null&&!list.IsMouseCaptured)clear();};
        list.PreviewKeyDown+=(s,e)=>{
            if(e.Key!=Key.Escape||dragged==null)return;
            clear();
            e.Handled=true;
        };
    }

    static void MoveEditorSelection(ListBox list,Action<int,int> move,int offset)
    {
        int from=list.SelectedIndex,to=from+offset;
        if(from<0||to<0||to>=list.Items.Count)return;
        var item=list.SelectedItem;
        move(from,to);
        list.SelectedItem=item;
        list.ScrollIntoView(item);
        list.UpdateLayout();
        var row=list.ItemContainerGenerator.ContainerFromItem(item) as ListBoxItem;
        if(row!=null)
        {
            var grip=Visuals<Button>(row).FirstOrDefault(button=>Equals(button.Tag,"editor-grip"));
            if(grip!=null)grip.Focus();
        }
    }
}