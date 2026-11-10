using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

internal sealed class CatalogDraft
{
    readonly string file;
    readonly string original;
    internal readonly List<Entry> Entries;

    internal CatalogDraft(string path)
    {
        file=path;
        original=File.ReadAllText(path);
        Entries=AppData.ReadEntries(original,Path.GetDirectoryName(file));
    }

    internal Entry Add(string section,string kind)
    {
        if(section=="all"||!SidebarStore.Known(section))throw new IOException("Choose a category for the new link.");
        if(!new[]{"folder","document","web"}.Contains(kind))throw new IOException("Choose Folder, Document or Website.");
        var entry=new Entry{id="link-"+Guid.NewGuid().ToString("N"),section=section,name="New link",displayName="New link",
            targets=new List<LinkTarget>{new LinkTarget{id="open",kind=kind,label=kind=="web"?"Website":kind=="folder"?"Folder":"Open",path=kind=="web"?null:"",url=kind=="web"?"":null}}};
        Entries.Add(entry);
        return entry;
    }

    internal void Remove(Entry entry){Entries.Remove(entry);}

    internal string Prepare()
    {
        CheckOriginal();
        var rows=AppData.Json.Deserialize<List<Dictionary<string,object>>>(original);
        var baseline=AppData.ReadEntries(original,Path.GetDirectoryName(file));
        var originals=baseline.Select((entry,index)=>new{entry,index}).ToDictionary(item=>item.entry.id,StringComparer.Ordinal);
        if(Entries.Any(entry=>entry==null||String.IsNullOrWhiteSpace(entry.id))||Entries.Select(entry=>entry.id).Distinct(StringComparer.Ordinal).Count()!=Entries.Count)
            throw new IOException("The directory contains duplicate or invalid link identifiers.");
        bool changed=Entries.Count!=baseline.Count;
        var result=new List<Dictionary<string,object>>();
        foreach(var entry in Entries)
        {
            if(originals.ContainsKey(entry.id))
            {
                var before=originals[entry.id];
                var row=rows[before.index];
                changed=ApplyEntry(row,entry,before.entry)||changed;
                result.Add(row);
            }
            else{result.Add(NewRow(entry));changed=true;}
        }
        return changed?AppData.Json.Serialize(result):null;
    }

    internal void Save()
    {
        using(var mutex=new Mutex(false,ChecklistStore.LockName(file)))
        {
            bool acquired;
            try { acquired=mutex.WaitOne(0); }
            catch(AbandonedMutexException) { acquired=true; }
            if(!acquired)throw new IOException("The directory is busy. Try again.");
            try
            {
                string json=Prepare();
                if(json==null)return;
                string temporary=Path.Combine(Path.GetDirectoryName(file),Guid.NewGuid().ToString("N")+".tmp");
                try
                {
                    File.WriteAllText(temporary,json);
                    CheckOriginal();
                    File.Replace(temporary,file,null);
                }
                finally { if(File.Exists(temporary))File.Delete(temporary); }
            }
            finally { mutex.ReleaseMutex(); }
        }
    }

    static void CheckName(string name)
    {
        if(String.IsNullOrWhiteSpace(name)||name.Length>120||name.Any(Char.IsControl))
            throw new IOException("Enter a display name of 1 to 120 characters.");
    }

    static Dictionary<string,object> NewRow(Entry entry)
    {
        CheckName(entry.name);
        if(entry.section=="all"||!SidebarStore.Known(entry.section)||entry.targets==null||entry.targets.Count!=1)
            throw new IOException("Choose a category and destination for the new link.");
        var target=entry.targets[0];
        if(target.kind=="web"&&(String.IsNullOrEmpty(target.url)||target.url.Contains("\"")||target.url.Any(Char.IsControl)))
            throw new IOException("Enter a valid website address.");
        AppData.Validate(target);
        var destination=new Dictionary<string,object>{{"id",target.id},{"kind",target.kind},{"label",target.label}};
        destination[target.kind=="web"?"url":"path"]=target.kind=="web"?target.url:target.path;
        var row=new Dictionary<string,object>{{"id",entry.id},{"name",entry.name.Trim()},{"displayName",entry.name.Trim()},{"section",entry.section},{"targets",new[]{destination}}};
        if(!String.IsNullOrWhiteSpace(entry.iconPath))
        {
            using(var image=Path.GetExtension(entry.iconPath).Equals(".ico",StringComparison.OrdinalIgnoreCase)?DashboardForm.ReadIcon(entry.iconPath):Image.FromFile(entry.iconPath)){}
            row["iconPath"]=entry.iconPath;
        }
        if(!String.IsNullOrEmpty(entry.iconGlyph))row["iconGlyph"]=entry.iconGlyph;
        return row;
    }

    void CheckOriginal()
    {
        if(File.ReadAllText(file)!=original)
            throw new IOException("The directory changed elsewhere. Cancel and reopen Edit before saving.");
    }

    static bool ApplyEntry(Dictionary<string,object> row,Entry entry,Entry previous)
    {
        bool changed=false;
        if(entry.section!=previous.section)
        {
            if(entry.section=="all"||!SidebarStore.Known(entry.section))throw new IOException("Choose a valid category.");
            row["section"]=entry.section;
            changed=true;
        }
        if(entry.iconGlyph!=previous.iconGlyph)
        {
            if(String.IsNullOrEmpty(entry.iconGlyph))row.Remove("iconGlyph");else row["iconGlyph"]=entry.iconGlyph;
            changed=true;
        }
        if(entry.iconAsset!=previous.iconAsset)
        {
            if(String.IsNullOrEmpty(entry.iconAsset))row.Remove("iconAsset");else row["iconAsset"]=entry.iconAsset;
            changed=true;
        }
        if(entry.name!=previous.name)
        {
            CheckName(entry.name);
            row["displayName"]=entry.name.Trim();
            changed=true;
        }
        if(entry.iconPath!=previous.iconPath)
        {
            if(String.IsNullOrEmpty(entry.iconPath))row.Remove("iconPath");
            else
            {
                using(var image=Path.GetExtension(entry.iconPath).Equals(".ico",StringComparison.OrdinalIgnoreCase)
                    ?DashboardForm.ReadIcon(entry.iconPath):Image.FromFile(entry.iconPath)){}
                row["iconPath"]=entry.iconPath;
            }
            changed=true;
        }
        var targets=(IList)row["targets"];
        if(entry.targets.Count==0&&previous.targets.Count>0)throw new IOException("Keep one destination or remove the link.");
        var retained=new List<object>();
        foreach(var target in entry.targets)
        {
            int index=previous.targets.FindIndex(item=>item.id==target.id);
            if(index<0)throw new IOException("The destination is no longer available.");
            var before=previous.targets[index];
            var raw=(Dictionary<string,object>)targets[index];
            retained.Add(raw);
            if(target.kind==before.kind&&target.path==before.path&&target.url==before.url)continue;
            if(target.kind=="web"&&(String.IsNullOrEmpty(target.url)||target.url.Contains("\"")||target.url.Any(Char.IsControl)))
                throw new IOException("Enter a valid website address.");
            AppData.Validate(target);
            if(target.kind!=before.kind)
            {
                raw["kind"]=target.kind;
                string oldLabel=before.kind=="web"?"Website":before.kind=="folder"?"Folder":"Open";
                if(String.IsNullOrWhiteSpace(before.label)||before.label==oldLabel)
                    raw["label"]=target.kind=="web"?"Website":target.kind=="folder"?"Folder":"Open";
                if(target.kind=="web")raw.Remove("path");
                else raw.Remove("url");
            }
            if(target.kind=="web")raw["url"]=target.url;
            else if(target.path!=before.path)raw["path"]=target.path;
            if(target.kind=="folder"&&String.IsNullOrWhiteSpace(entry.displayName))
                row["displayName"]=entry.name.Trim();
            changed=true;
        }
        if(retained.Count!=targets.Count)
        {
            row["targets"]=retained;
            changed=true;
        }
        return changed;
    }
}

internal sealed class CatalogEditor : DashboardDialog
{
    readonly CatalogDraft draft;
    readonly ComboBox sections=new ComboBox{AccessibleName="Section",DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed};
    readonly List<string> sectionIds=new List<string>();
    List<Entry> visibleEntries;
    readonly ListBox entryList=new ListBox{AccessibleName="Dashboard entries",DrawMode=DrawMode.OwnerDrawFixed,BorderStyle=BorderStyle.FixedSingle,IntegralHeight=false};
    readonly ListBox targetList=new ListBox{AccessibleName="Destination",DrawMode=DrawMode.OwnerDrawFixed,BorderStyle=BorderStyle.FixedSingle,IntegralHeight=false};
    readonly TextBox displayName=new TextBox{AccessibleName="Display name",MaxLength=120};
    readonly TextBox location=new TextBox{AccessibleName="Target location"};
    readonly TextBox iconPath=new TextBox{AccessibleName="Icon file"};
    readonly Button browseTarget=DashboardForm.LinkButton("Browse…");
    Entry selected;
    LinkTarget target;
    bool loading;

    internal CatalogEditor(string path,string initialSection,Func<string,string> sectionLabel) : base("Edit dashboard links",new Size(860,555))
    {
        draft=new CatalogDraft(path);
        MaximizeBox=false;
        FormBorderStyle=FormBorderStyle.FixedDialog;
        Add(Label("Edit dashboard links",new Font("Segoe UI Variable Text",20,FontStyle.Bold),DashboardForm.Ink),20,15,790,40);
        Add(Label("Section",Font,DashboardForm.Muted),20,69,250,23);
        sections.ItemHeight=26;
        sections.BackColor=DashboardForm.Surface;
        sections.ForeColor=DashboardForm.Ink;
        sections.DrawItem+=(sender,e)=>DrawSelection(e,sections.Items);
        Add(sections,20,96,250,32);
        Add(Label("Dashboard entries",Font,DashboardForm.Muted),20,143,250,23);
        Add(entryList,20,170,250,273);
        Add(Label("Display name",Font,DashboardForm.Muted),290,69,500,23);
        Add(new DashboardField(displayName),290,96,525,41);
        Add(Label("Destination",Font,DashboardForm.Muted),290,148,500,23);
        Add(targetList,290,175,525,62);
        Add(new DashboardField(location),290,249,420,41);
        Add(browseTarget,720,249,95,41);
        Add(Label("Icon file (optional)",Font,DashboardForm.Muted),290,305,500,23);
        Add(new DashboardField(iconPath),290,332,420,41);
        var browseIcon=DashboardForm.LinkButton("Browse…");
        Add(browseIcon,720,332,95,41);
        Add(Label("Clear the icon path to use the default icon.",Font,DashboardForm.Muted),290,383,520,28);
        entryList.ItemHeight=32;
        targetList.ItemHeight=30;
        entryList.DrawItem+=(sender,e)=>DrawSelection(e,entryList.Items);
        targetList.DrawItem+=(sender,e)=>DrawSelection(e,targetList.Items);
        foreach(Control field in new Control[]{entryList,targetList,displayName,location,iconPath})
        {
            field.BackColor=DashboardForm.Surface;
            field.ForeColor=DashboardForm.Ink;
        }

        var save=DashboardForm.LinkButton("Save");
        var cancel=DashboardForm.LinkButton("Cancel");
        Add(save,620,462,90,36);
        Add(cancel,725,462,90,36);
        cancel.DialogResult=DialogResult.Cancel;
        CancelButton=cancel;
        save.Click+=(s,e)=>{
            try { draft.Save(); DialogResult=DialogResult.OK; Close(); }
            catch(Exception error) { MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning); }
        };
        entryList.SelectedIndexChanged+=(s,e)=>SelectEntry();
        targetList.SelectedIndexChanged+=(s,e)=>SelectTarget();
        displayName.TextChanged+=(s,e)=>{if(!loading&&selected!=null)selected.name=displayName.Text;};
        iconPath.TextChanged+=(s,e)=>{if(!loading&&selected!=null)selected.iconPath=iconPath.Text.Trim();};
        location.TextChanged+=(s,e)=>{
            if(loading||target==null)return;
            if(target.kind=="web")target.url=location.Text.Trim();
            else target.path=location.Text.Trim();
        };
        browseTarget.Click+=(s,e)=>BrowseDestination();
        browseIcon.Click+=(s,e)=>ChooseFile(iconPath,"Image files|*.ico;*.png;*.jpg;*.jpeg;*.bmp;*.gif");
        browseIcon.Enabled=false;
        entryList.SelectedIndexChanged+=(s,e)=>browseIcon.Enabled=selected!=null;
        for(int i=0;i<SidebarStore.Groups.GetLength(0);i++)
        {
            string id=SidebarStore.Groups[i,0];
            sectionIds.Add(id);
            sections.Items.Add(id=="all"?"All entries":sectionLabel(id));
        }
        sections.SelectedIndexChanged+=(s,e)=>ShowSection();
        int initialIndex=sectionIds.IndexOf(initialSection);
        sections.SelectedIndex=initialIndex<0?sectionIds.IndexOf("all"):initialIndex;
        save.Enabled=draft.Entries.Count>0;
        ActiveControl=sections;
    }

    void ShowSection()
    {
        string section=sectionIds[sections.SelectedIndex];
        visibleEntries=draft.Entries.Where(e=>DashboardForm.EntryInSection(e,section))
            .OrderBy(e=>e.name,StringComparer.CurrentCultureIgnoreCase).ThenBy(e=>e.id,StringComparer.Ordinal).ToList();
        var previous=selected;
        entryList.BeginUpdate();
        try
        {
            loading=true;
            entryList.Items.Clear();
            foreach(var entry in visibleEntries)entryList.Items.Add(entry.name);
        }
        finally
        {
            loading=false;
            entryList.EndUpdate();
        }
        if(visibleEntries.Count==0)SelectEntry();
        else entryList.SelectedIndex=Math.Max(0,visibleEntries.IndexOf(previous));


    }

    static void DrawSelection(DrawItemEventArgs e,IList items)
    {
        if(e.Index<0)return;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        bool highContrast=SystemInformation.HighContrast;
        Color background=selected?(highContrast?SystemColors.Highlight:DashboardForm.Accent):DashboardForm.Surface;
        Color text=selected?(highContrast?SystemColors.HighlightText:DashboardForm.Background):DashboardForm.Ink;
        using(var fill=new SolidBrush(background))e.Graphics.FillRectangle(fill,e.Bounds);
        var textBounds=Rectangle.Inflate(e.Bounds,-9,0);
        TextRenderer.DrawText(e.Graphics,Convert.ToString(items[e.Index]),e.Font,textBounds,text,
            TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds,text,background);
    }

    void Add(Control control,int x,int y,int width,int height)
    {
        control.SetBounds(x,y,width,height);
        Controls.Add(control);
    }

    void SelectEntry()
    {
        if(loading)return;
        loading=true;
        selected=entryList.SelectedIndex<0?null:visibleEntries[entryList.SelectedIndex];
        displayName.Text=selected==null?"":selected.name;
        iconPath.Text=selected==null?"":selected.iconPath??"";
        targetList.Items.Clear();
        displayName.Enabled=iconPath.Enabled=selected!=null;
        if(selected!=null)foreach(var destination in selected.targets)
            targetList.Items.Add(String.IsNullOrWhiteSpace(destination.label)?destination.kind:destination.label);
        if(targetList.Items.Count>0)targetList.SelectedIndex=0;
        loading=false;
        SelectTarget();
    }

    void SelectTarget()
    {
        if(loading)return;
        target=selected==null||targetList.SelectedIndex<0?null:selected.targets[targetList.SelectedIndex];
        loading=true;
        location.Text=target==null?"":target.kind=="web"?target.url:target.path;
        location.Enabled=target!=null;
        browseTarget.Enabled=target!=null&&target.kind!="web";
        loading=false;
    }

    void BrowseDestination()
    {
        if(target==null)return;
        if(target.kind=="folder")
        {
            using(var dialog=new FolderBrowserDialog{SelectedPath=location.Text,ShowNewFolderButton=false})
                if(dialog.ShowDialog(this)==DialogResult.OK)location.Text=dialog.SelectedPath;
        }
        else if(target.kind=="document")
            ChooseFile(location,"Supported documents|*.md;*.txt;*.pdf;*.docx;*.xlsx;*.pptx;*.html");
    }

    void ChooseFile(TextBox field,string filter)
    {
        string initial=File.Exists(field.Text)?Path.GetFileName(field.Text):"";
        using(var dialog=new OpenFileDialog{Filter=filter,CheckFileExists=true,FileName=initial})
            if(dialog.ShowDialog(this)==DialogResult.OK)field.Text=dialog.FileName;
    }
}

internal sealed class DashboardField : Panel
{
    readonly TextBox input;

    internal DashboardField(TextBox input)
    {
        this.input=input;
        BackColor=DashboardForm.Surface;
        input.BorderStyle=BorderStyle.None;
        input.BackColor=DashboardForm.Surface;
        input.ForeColor=DashboardForm.Ink;
        Controls.Add(input);
        input.GotFocus+=(sender,e)=>Invalidate();
        input.LostFocus+=(sender,e)=>Invalidate();
        TabStop=false;
    }

    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int inset=Math.Max(8,Font.Height/2);
        input.SetBounds(inset,(Height-input.PreferredHeight)/2,Math.Max(1,Width-2*inset),input.PreferredHeight);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using(var border=new Pen(input.Focused?DashboardForm.Accent:
            SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(74,74,74)))
            e.Graphics.DrawRectangle(border,0,0,Width-1,Height-1);
    }
}
