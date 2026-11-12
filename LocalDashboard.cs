using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;
[assembly: AssemblyTitle("Local Dashboard")]
[assembly: AssemblyProduct("Local Dashboard")]
[assembly: AssemblyDescription("Local projects, documents and workflows")]
#if WPF_REBUILD
[assembly: AssemblyVersion("2.2.5.0")]
[assembly: AssemblyFileVersion("2.2.5.0")]
#else
[assembly: AssemblyVersion("1.5.6.0")]
[assembly: AssemblyFileVersion("1.5.6.0")]
#endif
public sealed class LinkTarget { public string id {get;set;} public string kind {get;set;} public string path {get;set;} public string url {get;set;} public string label {get;set;} public string openIn {get;set;} }
public sealed class Entry { public string id {get;set;} public string name {get;set;} public string displayName {get;set;} public string section {get;set;} public string description {get;set;} public string iconPath {get;set;} public string iconGlyph {get;set;} public string iconAsset {get;set;} public string iconWebsiteTargetId {get;set;} public List<LinkTarget> targets {get;set;} }
internal static class AppData {
    internal static readonly string Root=LocateRoot();
    internal static readonly JavaScriptSerializer Json=new JavaScriptSerializer { MaxJsonLength=32*1024*1024 };
    internal static readonly bool IsReview=Path.GetFileNameWithoutExtension(Application.ExecutablePath).EndsWith(".Review",StringComparison.OrdinalIgnoreCase);
#if WPF_REBUILD
    internal static readonly string IconPath=Path.Combine(Root,"dist","icons","local-dashboard.ico");
#else
    internal static readonly string IconPath=Path.Combine(Root,Path.GetFileNameWithoutExtension(Application.ExecutablePath).EndsWith(".Review",StringComparison.OrdinalIgnoreCase)?".dev\\Review\\Icon\\local-dashboard-draft.ico":"dist\\icons\\local-dashboard.ico");
#endif
    internal static readonly string WindowTitle=Path.GetFileNameWithoutExtension(Application.ExecutablePath).EndsWith(".Review",StringComparison.OrdinalIgnoreCase)?"Local Dashboard — Review 1.5.6":"Local Dashboard";
    internal static string LocateRoot(){
#if WPF_REBUILD
        var appDirectory=AppDomain.CurrentDomain.BaseDirectory;
        if(String.Equals(Path.GetFileName(Application.ExecutablePath),"LocalDashboard.exe",StringComparison.OrdinalIgnoreCase)
            &&File.Exists(Path.Combine(appDirectory,"catalog.json")))
            return Path.GetFullPath(appDirectory).TrimEnd(Path.DirectorySeparatorChar);
#endif
        var directory=new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);for(int i=0;i<6&&directory!=null;i++,directory=directory.Parent){if(File.Exists(Path.Combine(directory.FullName,"catalog.json"))&&File.Exists(Path.Combine(directory.FullName,"LocalDashboard.cs")))return directory.FullName;}throw new Exception("Local Dashboard catalog was not found. Keep the app in its project folder.");}
    internal static List<Entry> Load(){return ReadEntries(File.ReadAllText(Path.Combine(Root,"catalog.json")));}
    internal static List<Entry> ReadEntries(string json,string catalogRoot=null){var entries=Json.Deserialize<List<Entry>>(json);if(entries==null||entries.Select(e=>e.id).Distinct().Count()!=entries.Count)throw new Exception("The directory contains duplicate or invalid entries.");foreach(var item in entries)foreach(var target in item.targets)if(target.path!=null&&target.path.StartsWith("@app/",StringComparison.Ordinal))target.path=Path.GetFullPath(Path.Combine(catalogRoot??Root,target.path.Substring(5).Replace('/',Path.DirectorySeparatorChar)));foreach(var entry in entries.Where(e=>e.section=="software"||e.section=="website"||e.section=="secondary")){var folder=entry.targets.FirstOrDefault(t=>t.kind=="folder");if(folder!=null&&String.IsNullOrWhiteSpace(entry.displayName))entry.name=Path.GetFileName(Path.GetFullPath(folder.path).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar));}foreach(var entry in entries)if(!String.IsNullOrWhiteSpace(entry.displayName))entry.name=entry.displayName;return entries;}
    internal static LinkTarget Resolve(List<Entry> entries,string entryId,string targetId){var entry=entries.FirstOrDefault(e=>e.id==entryId);var target=entry==null?null:entry.targets.FirstOrDefault(t=>t.id==targetId);if(target==null)throw new Exception("This destination is not in the directory.");Validate(target);return target;}
    internal static void Validate(LinkTarget target){
        if(target.kind=="web"){Uri uri;if(!Uri.TryCreate(target.url,UriKind.Absolute,out uri)||(uri.Scheme!="https"&&uri.Scheme!="http"))throw new Exception("Unsupported website address.");return;}
        if(target.kind=="folder"){if(!Directory.Exists(target.path))throw new Exception("Folder not found: "+target.path);return;}
        if(target.kind!="document"||!File.Exists(target.path))throw new Exception("Document not found: "+target.path);
        string extension=Path.GetExtension(target.path).ToLowerInvariant();if(!new[]{".md",".txt",".pdf",".docx",".xlsx",".pptx",".html"}.Contains(extension))throw new Exception("Unsupported document type.");
    }
    internal static void Browser(string value){
        string firefox=new[]{Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Mozilla Firefox","firefox.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Mozilla Firefox","firefox.exe"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Mozilla Firefox","firefox.exe")}.FirstOrDefault(File.Exists);
        if(firefox!=null)Process.Start(new ProcessStartInfo(firefox,"-new-tab \""+value+"\""){UseShellExecute=true});else Process.Start(new ProcessStartInfo(value){UseShellExecute=true});
    }
    internal static void Open(List<Entry> entries,string entryId,string targetId,string profile=null){
        var target=Resolve(entries,entryId,targetId);
        if(target.kind=="folder")Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"\""+target.path+"\""){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Normal});
        else if(target.kind=="web")Browser(target.url);
        else if(target.openIn=="browser"||Path.GetExtension(target.path).Equals(".html",StringComparison.OrdinalIgnoreCase))Browser(new Uri(target.path).AbsoluteUri);
        else Process.Start(new ProcessStartInfo(target.path){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Normal});
        try {Directory.CreateDirectory(Path.Combine(profile??Root,".local"));File.WriteAllText(Path.Combine(profile??Root,".local","desktop-status.json"),Json.Serialize(new {at=DateTime.UtcNow.ToString("o"),entry=entryId,target=targetId,kind=target.kind,state="launched"}));}catch{}
    }
    internal static Tuple<string,string> Parse(string request,List<Entry> entries){
        if(request==null||request!=request.Trim()||request.Any(Char.IsControl)||request.IndexOf('"')>=0)throw new Exception("Unsupported dashboard link.");
        var uri=new Uri(request);if(uri.Scheme!="localdashboard"||uri.UserInfo!=""||uri.Port!=-1||uri.Query!=""||uri.Fragment!="")throw new Exception("Unsupported dashboard link.");
        if(uri.Host=="open"&&(uri.AbsolutePath==""||uri.AbsolutePath=="/"))return null;
        var parts=uri.AbsolutePath.Split(new[]{'/'},StringSplitOptions.RemoveEmptyEntries);if(uri.Host!="item"||parts.Length!=2)throw new Exception("Unsupported dashboard link.");
        string id=Uri.UnescapeDataString(parts[0]),target=Uri.UnescapeDataString(parts[1]);Resolve(entries,id,target);return Tuple.Create(id,target);
    }
}
internal sealed class IconView:Control {
    Image source;Bitmap display;
    internal Image Image {get{return source;}set{source=value;Prepare();Invalidate();}}
    internal IconView(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw|ControlStyles.SupportsTransparentBackColor,true);BackColor=Color.Transparent;TabStop=false;}
    void Prepare(){
        if(display!=null){display.Dispose();display=null;}
        if(source==null||Width<1||Height<1)return;
        double ratio=Math.Min((double)Width/source.Width,(double)Height/source.Height);
        int width=Math.Max(1,(int)Math.Round(source.Width*ratio)),height=Math.Max(1,(int)Math.Round(source.Height*ratio));
        display=new Bitmap(width,height,System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
        using(var graphics=Graphics.FromImage(display))using(var attributes=new System.Drawing.Imaging.ImageAttributes()){
            graphics.Clear(Color.Transparent);graphics.CompositingMode=CompositingMode.SourceCopy;graphics.CompositingQuality=CompositingQuality.HighQuality;graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;graphics.PixelOffsetMode=PixelOffsetMode.HighQuality;attributes.SetWrapMode(WrapMode.TileFlipXY);
            graphics.DrawImage(source,new Rectangle(0,0,width,height),0,0,source.Width,source.Height,GraphicsUnit.Pixel,attributes);
        }
    }
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);Prepare();}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(display!=null)e.Graphics.DrawImageUnscaled(display,(Width-display.Width)/2,(Height-display.Height)/2);}
    protected override void Dispose(bool disposing){if(disposing&&display!=null){display.Dispose();display=null;}base.Dispose(disposing);}
}
internal sealed class Card:Panel {
    internal Card(){DoubleBuffered=true;BackColor=DashboardForm.Surface;Margin=Padding.Empty;}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(DashboardForm.Border))e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,Width-1),Math.Max(0,Height-1));}
}
internal sealed class DestinationRow:Panel {
    internal void TestReorderKey(Keys key){OnKeyDown(new KeyEventArgs(Keys.Control|key));}internal string EntryId;const string DragFormat="LocalDashboard.Project.Row";
    readonly Label name;readonly Label detail;readonly List<Button> actions=new List<Button>();readonly IconView picture;readonly Button pin;Label grip;bool editingMode;
    internal static bool CompactRow(Entry entry){return entry.section=="software"&&entry.description=="Open the project folder or its current local project notes.";}
    internal static int HeightFor(Entry entry){return CompactRow(entry)?44:58;}
    internal DestinationRow(Entry entry,Image icon,HashSet<string> pinned,Action<Entry,LinkTarget> open,Action<string> toggle,ToolTip tips,bool editing=false,string group=null,Action<string,string,bool> moveBefore=null,Action<string,int> moveBy=null){
        EntryId=entry.id;Tag=entry.id;DoubleBuffered=true;SetStyle(ControlStyles.Selectable,true);TabStop=editing;AllowDrop=editing;Height=HeightFor(entry);BackColor=DashboardForm.Surface;AccessibleName=entry.name;
        picture=new IconView {Left=14,Top=13,Width=32,Height=32,Image=icon};Controls.Add(picture);
        name=new Label {Text=entry.name,Left=58,Top=9,Height=22,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,UseMnemonic=false,ForeColor=DashboardForm.Ink,Font=new Font("Segoe UI Variable Text",10)};Controls.Add(name);detail=new Label{Text=entry.description,Left=58,Top=33,Height=18,ForeColor=DashboardForm.Muted,Font=new Font("Segoe UI Variable Text",9),AutoEllipsis=true,UseMnemonic=false};Controls.Add(detail);
        if(CompactRow(entry)){detail.Visible=false;name.Top=11;picture.Top=6;}tips.SetToolTip(name,entry.name+Environment.NewLine+entry.description+Environment.NewLine+String.Join(Environment.NewLine,entry.targets.Select(t=>t.path??t.url)));
        foreach(var target in entry.targets){string text=target.id=="artwork-review"?"Artwork":target.kind=="folder"?"Folder":target.kind=="web"?(entry.section=="website"?"Website":"Open"):target.label=="Open catalog"?"Catalog":target.path.IndexOf("\\.dev\\",StringComparison.OrdinalIgnoreCase)>=0?"Notes":"Open";var button=DashboardForm.LinkButton(text);button.Width=Math.Max(50,TextRenderer.MeasureText(text,button.Font).Width+24);button.Height=34;button.Top=CompactRow(entry)?5:12;button.AccessibleName="Open "+target.kind+" for "+entry.name;button.Enabled=target.kind=="web"||File.Exists(target.path)||Directory.Exists(target.path);var captured=target;button.Click+=(s,e)=>open(entry,captured);tips.SetToolTip(button,target.path??target.url);Controls.Add(button);actions.Add(button);}
        pin=DashboardForm.LinkButton(pinned.Contains(entry.id)?"★":"☆");pin.Width=34;pin.Height=34;pin.Top=CompactRow(entry)?5:12;pin.Font=new Font("Segoe UI Symbol",11);pin.ForeColor=pinned.Contains(entry.id)?Color.FromArgb(230,200,136):DashboardForm.Muted;pin.AccessibleName="Pin "+entry.name;pin.Click+=(s,e)=>toggle(entry.id);tips.SetToolTip(pin,"Keep at the top of this section");Controls.Add(pin);Resize+=(s,e)=>PositionControls();{grip=new Label{Text="⋮⋮",Left=0,Top=0,Width=22,Height=Height,TextAlign=ContentAlignment.MiddleCenter,ForeColor=DashboardForm.Muted,Cursor=Cursors.SizeAll,AccessibleName="Drag "+entry.name};Controls.Add(grip);AccessibleDescription="Editing order only. Drag the handle or use Ctrl+Up/Down. Project names come from configured folders.";ReorderDrag.Source(grip,DragFormat,()=>group+"\\n"+entry.id,()=>editingMode,()=>entry.name);name.Click+=(s,e)=>Focus();KeyDown+=(s,e)=>{if(editingMode&&e.Control&&(e.KeyCode==Keys.Up||e.KeyCode==Keys.Down)){if(moveBy!=null)moveBy(entry.id,e.KeyCode==Keys.Up?-1:1);e.Handled=e.SuppressKeyPress=true;}};ReorderDrag.Target(this,DragFormat,data=>editingMode&&data.StartsWith(group+"\\n",StringComparison.Ordinal)&&data.Substring(group.Length+2)!=entry.id,(data,after)=>{if(moveBefore!=null)moveBefore(data.Substring(group.Length+2),entry.id,after);});SetEditing(editing);}

    }
    internal void SetEditing(bool value){editingMode=value;TabStop=AllowDrop=value;foreach(Control child in Controls)child.AllowDrop=value;if(grip!=null)grip.Visible=value;picture.Left=value?36:14;name.Left=detail.Left=value?80:58;PositionControls();Invalidate();}
    protected override void OnGotFocus(EventArgs e){base.OnGotFocus(e);Invalidate();}
    protected override void OnLostFocus(EventArgs e){base.OnLostFocus(e);Invalidate();}
    void PositionControls(){int x=Width-12-pin.Width;pin.Left=x;for(int i=actions.Count-1;i>=0;i--){x-=actions[i].Width+6;actions[i].Left=x;}name.Width=detail.Width=Math.Max(30,x-name.Left-10);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(DashboardForm.Border))e.Graphics.DrawLine(pen,0,0,Width,0);if(Focused&&TabStop)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(2,2,Math.Max(1,Width-5),Math.Max(1,Height-5)),DashboardForm.Accent,BackColor);}
}
internal sealed class DashboardForm:Form {
    internal static readonly Color Background=SystemInformation.HighContrast?SystemColors.Window:Color.FromArgb(32,32,32),Surface=SystemInformation.HighContrast?SystemColors.Window:Color.FromArgb(43,43,43),Border=SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(59,59,59),Ink=SystemInformation.HighContrast?SystemColors.WindowText:Color.FromArgb(242,244,248),Muted=SystemInformation.HighContrast?SystemColors.GrayText:Color.FromArgb(176,184,198);
    internal static Color Accent{get{return SystemInformation.HighContrast?SystemColors.Highlight:DashboardAccent.Selected;}}
    readonly IconView brandView=new IconView();
    readonly Label brandTitle=new Label(),brandSubtitle=new Label();
    Image brandImage;Icon brandWindowIcon;
    WindowPreferenceStore preferences;bool exitRequested,startHidden;SidebarEditor sidebarEditor;readonly MenuStrip appMenu=new MenuStrip();ToolStripMenuItem pinMenu;bool arrangingViews;readonly Label pageTitle=new Label(),pageSubtitle=new Label();readonly Dictionary<string,Button> navigation=new Dictionary<string,Button>();Image modeGlyph;Button refreshButton,rootButton,pinWindow,hideWindow;string activeSection="software";readonly Panel body=new Panel();readonly Panel header=new Panel();readonly AgentStatusView agentView=new AgentStatusView();readonly CurrentWorkView checklistView=new CurrentWorkView();internal bool showReports;readonly Panel modeBar=new Panel();readonly NotifyIcon tray=new NotifyIcon();bool compact;Size directorySize=new Size(1160,820),widgetSize=new Size(460,660);Button modeButton;readonly TextBox search=new TextBox();DashboardField searchField;readonly Label status=new Label();readonly ToolTip tips=new ToolTip();
    readonly List<Panel> left=new List<Panel>(),right=new List<Panel>();readonly Dictionary<string,Image> images=new Dictionary<string,Image>();
    List<Entry> entries;HashSet<string> pinned=new HashSet<string>();Panel secondary;bool expanded,layingOut,disposedResources;
    readonly string pinPath=Path.Combine(AppData.Root,".local",AppData.IsReview?"pins-review.json":"pins.json");
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr SendMessage(IntPtr handle,int message,IntPtr value,string text);
    [DllImport("dwmapi.dll")]static extern int DwmSetWindowAttribute(IntPtr handle,int attribute,ref int value,int size);
    internal static Button LinkButton(string text){return new DashboardButton {Text=text,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(56,56,56),ForeColor=Ink,Font=new Font("Segoe UI Variable Text",10.5f),Cursor=Cursors.Hand,UseVisualStyleBackColor=false,FlatAppearance={BorderSize=0,MouseOverBackColor=Color.FromArgb(53,53,53)},Margin=Padding.Empty};}
    internal DashboardForm(string layoutPath=null,string preferencePath=null,IStartupEntry startupBackend=null){
        Text=AppData.WindowTitle;Size=new Size(1280,860);directorySize=Size;MinimumSize=new Size(760,540);StartPosition=FormStartPosition.CenterScreen;BackColor=Background;ForeColor=Ink;Font=new Font("Segoe UI Variable Text",10.5f);AutoScaleMode=AutoScaleMode.Dpi;

        try{preferences=new WindowPreferenceStore(preferencePath??WindowPreferenceStore.FilePath,startupBackend??new DashboardStartup());}catch{preferences=null;}
        DashboardAccent.Selected=DashboardAccent.Read(preferences==null?null:preferences.Value.accentColor);
        try{string readPins=File.Exists(pinPath)?pinPath:Path.Combine(AppData.Root,".local","pins.json");if(File.Exists(readPins))pinned=new HashSet<string>(AppData.Json.Deserialize<List<string>>(File.ReadAllText(readPins)));}catch{}
        appMenu.BackColor=Color.FromArgb(38,38,38);appMenu.ForeColor=Ink;appMenu.Font=Font;appMenu.Renderer=new DashboardMenuRenderer();appMenu.Padding=new Padding(10,2,0,2);appMenu.Dock=DockStyle.None;appMenu.AutoSize=false;Controls.Add(appMenu);MainMenuStrip=appMenu;
        var file=new ToolStripMenuItem("&File");appMenu.Items.Add(file);file.DropDownItems.Add(Command("Refresh directory","updates",()=>Reload(),Keys.Control|Keys.R));file.DropDownItems.Add(new ToolStripSeparator());file.DropDownItems.Add(Command("Hide to tray","hide",()=>HideToTray(),Keys.None));file.DropDownItems.Add(Command("Exit","exit",()=>ExitApplication(),Keys.Control|Keys.Q));
        file.DropDownItems.Insert(1,Command("Settings…","settings",()=>ShowSettings(),Keys.None));
        var view=new ToolStripMenuItem("&View");appMenu.Items.Add(view);view.DropDownItems.Add(Command("Dashboard","dashboard",()=>SetCompact(false),Keys.None));view.DropDownItems.Add(Command("Current Work","checklist",()=>{showReports=false;SetCompact(true);LayoutViews();},Keys.Control|Keys.Shift|Keys.C));view.DropDownItems.Add(Command("Participating task reports","reports",()=>{showReports=true;SetCompact(true);LayoutViews();},Keys.None));pinMenu=Command("Always on top","pin",()=>SetPinned(!TopMost),Keys.None);view.DropDownItems.Add(pinMenu);view.DropDownOpening+=(s,e)=>SyncPin();
        var help=new ToolStripMenuItem("&Help");appMenu.Items.Add(help);help.DropDownItems.Add(Command("Help & user guide","help",()=>ShowGuide(),Keys.F1));help.DropDownItems.Add(Command("About Local Dashboard","info",()=>About(),Keys.None));
        foreach(ToolStripMenuItem menu in appMenu.Items){menu.Padding=new Padding(7,6,7,6);menu.DropDown.BackColor=Surface;menu.DropDown.ForeColor=Ink;menu.DropDown.Padding=new Padding(5);menu.DropDown.MinimumSize=new Size(250,0);foreach(ToolStripItem command in menu.DropDownItems){command.ForeColor=Ink;command.Padding=new Padding(11,8,11,8);}}
        modeBar.BackColor=Color.FromArgb(38,38,38);Controls.Add(modeBar);
        brandView.SetBounds(18,16,48,48);modeBar.Controls.Add(brandView);
        brandTitle.Text="Local Dashboard";brandTitle.SetBounds(18,80,220,28);brandTitle.Font=new Font("Segoe UI Variable Text",15,FontStyle.Bold);brandTitle.ForeColor=Ink;brandTitle.UseMnemonic=false;brandTitle.AutoEllipsis=true;modeBar.Controls.Add(brandTitle);
        brandSubtitle.Text="YOUR LOCAL WORKSPACE";brandSubtitle.SetBounds(18,114,220,20);brandSubtitle.Font=new Font("Segoe UI Variable Text",7.5f);brandSubtitle.ForeColor=Color.FromArgb(150,150,150);brandSubtitle.AutoEllipsis=true;modeBar.Controls.Add(brandSubtitle);
        string[,] categories=SidebarStore.Groups;
        for(int i=0;i<categories.GetLength(0);i++){string section=categories[i,0];var nav=LinkButton(categories[i,1]);nav.Image=DashboardMenuRenderer.Glyph(categories[i,2]);nav.TextAlign=ContentAlignment.MiddleLeft;nav.SetBounds(18,148+i*40,216,34);nav.Click+=(s,e)=>{SelectSection(section);};navigation[section]=nav;tips.SetToolTip(nav,categories[i,1]);modeBar.Controls.Add(nav);}sidebarEditor=new SidebarEditor(navigation,layoutPath??SidebarStore.FilePath);modeBar.Controls.Add(sidebarEditor);sidebarEditor.Changed=RefreshEditor;sidebarEditor.ManageLinks=EditCatalog;sidebarEditor.Message=text=>status.Text=text;
        modeButton=LinkButton("Current Work");modeGlyph=DashboardMenuRenderer.Glyph("checklist");modeButton.Image=modeGlyph;modeButton.TextAlign=ContentAlignment.MiddleLeft;modeButton.Click+=(s,e)=>{showReports=false;SetCompact(!compact);};modeBar.Controls.Add(modeButton);
        pinWindow=LinkButton("Pin on top");pinWindow.Image=DashboardMenuRenderer.Glyph("pin");pinWindow.TextAlign=ContentAlignment.MiddleLeft;pinWindow.AccessibleName="Always on top";pinWindow.Click+=(s,e)=>SetPinned(!TopMost);modeBar.Controls.Add(pinWindow);
        hideWindow=LinkButton("Hide to tray");hideWindow.Image=DashboardMenuRenderer.Glyph("hide");hideWindow.TextAlign=ContentAlignment.MiddleLeft;hideWindow.Click+=(s,e)=>HideToTray();modeBar.Controls.Add(hideWindow);tips.SetToolTip(hideWindow,"Double-click the notification icon or choose Show to return.");
        header.BackColor=Background;Controls.Add(header);
        pageTitle.Font=new Font("Segoe UI Variable Text",21.75f,FontStyle.Bold);pageTitle.ForeColor=Ink;pageTitle.UseMnemonic=false;header.Controls.Add(pageTitle);
        pageSubtitle.ForeColor=Muted;pageSubtitle.Font=Font;pageSubtitle.AutoEllipsis=true;pageSubtitle.UseMnemonic=false;header.Controls.Add(pageSubtitle);
        search.AccessibleName="Search all links";searchField=new DashboardField(search);header.Controls.Add(searchField);search.HandleCreated+=(s,e)=>SendMessage(search.Handle,0x1501,IntPtr.Zero,"Search all links…");search.TextChanged+=(s,e)=>{if(search.Text.Length>0&&activeSection!="all"){activeSection="all";UpdatePage();}Rebuild();};
        refreshButton=LinkButton("Refresh");refreshButton.Image=DashboardMenuRenderer.Glyph("updates");refreshButton.Click+=(s,e)=>Reload();header.Controls.Add(refreshButton);
        rootButton=LinkButton("Open folder");rootButton.Image=DashboardMenuRenderer.Glyph("folder");rootButton.Click+=(s,e)=>{var root=entries.FirstOrDefault(x=>x.id==(activeSection=="software"?"software-folder":"websites-folder"));if(root!=null)Open(root,root.targets[0]);};header.Controls.Add(rootButton);
        status.ForeColor=Muted;status.Padding=new Padding(30,0,15,0);status.TextAlign=ContentAlignment.MiddleLeft;Controls.Add(status);
        body.AutoScroll=true;body.BackColor=Background;Controls.Add(body);body.Resize+=(s,e)=>{PositionCards();ArrangeHeader();};Controls.Add(agentView);agentView.Dock=DockStyle.None;agentView.Visible=false;Controls.Add(checklistView);checklistView.Visible=false;
        tray.Text="Local Dashboard";UpdateBrandIcon();var trayMenu=new ContextMenuStrip{BackColor=Surface,ForeColor=Ink,Font=Font,Padding=new Padding(5),Renderer=new DashboardMenuRenderer()};trayMenu.Items.Add(Command("Show","restore",()=>RestoreWindow(),Keys.None));trayMenu.Items.Add(Command("Exit","exit",()=>ExitApplication(),Keys.None));foreach(ToolStripItem item in trayMenu.Items)item.Padding=new Padding(11,8,11,8);tray.ContextMenuStrip=trayMenu;tray.DoubleClick+=(s,e)=>RestoreWindow();
        Resize+=(s,e)=>{LayoutViews();if(WindowState==FormWindowState.Minimized&&preferences!=null&&preferences.Value.minimizeToTray)HideToTray();};Reload();LayoutViews();Shown+=(s,e)=>{DashboardWindowTheme.Apply(this);LayoutViews();PositionCards();};
    }
    void ShowSettings(){try{if(preferences==null)preferences=new WindowPreferenceStore(WindowPreferenceStore.FilePath,new DashboardStartup());preferences.Reload();var original=DashboardAccent.Selected;bool saved=false;
        try{using(var settings=new WindowSettings(preferences,ApplyAccent))saved=settings.ShowDialog(this)==DialogResult.OK;}
        finally{ApplyAccent(saved?DashboardAccent.Read(preferences.Value.accentColor):original);}}catch(Exception error){MessageBox.Show(this,error.Message,"Window settings",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    void ApplyAccent(){ApplyAccent(DashboardAccent.Read(preferences.Value.accentColor));}

    void ApplyAccent(Color color)
    {
        if(color==DashboardAccent.Selected)return;
        DashboardAccent.Selected=color;
        UpdateBrandIcon();
        foreach(string key in images.Keys.Where(k=>k.StartsWith("symbol:",StringComparison.Ordinal)||k=="dashboard-brand").ToList())
        {
            if(images[key]!=null)images[key].Dispose();
            images.Remove(key);
        }
        UpdatePage();
        Rebuild();
        agentView.Reload(entries);
        Invalidate(true);
    }

    void CheckSettingsEntry(Action open)
    {
        bool opened=false;
        using(var timer=new System.Windows.Forms.Timer{Interval=100})
        {
            timer.Tick+=(s,e)=>{
                var dialog=Application.OpenForms.OfType<WindowSettings>().FirstOrDefault();
                if(dialog==null)return;
                opened=dialog.Controls.OfType<CheckBox>().Count()==4;
                dialog.DialogResult=DialogResult.Cancel;
                dialog.Close();
            };
            timer.Start();
            open();
        }
        if(!opened)throw new Exception("Window Settings did not open with four options.");
    }
    internal void CheckLifecycle(){
        var fileMenu=(ToolStripMenuItem)appMenu.Items[0];
        CheckSettingsEntry(()=>fileMenu.DropDownItems[1].PerformClick());
        SetCompact(true);
        foreach(var size in new[]{Size,MinimumSize})
        {
            Size=size;
            Application.DoEvents();
            var controls=new[]{modeButton,pinWindow,hideWindow};
            if(appMenu.Visible||controls.Any(b=>!b.Visible||!b.TabStop||b.Right>modeBar.ClientSize.Width)||
                controls[0].Right>=controls[1].Left||controls[1].Right>=controls[2].Left)
                throw new Exception("Compact toolbar controls do not fit or accept keyboard focus.");
            foreach(var button in controls)
                if(TextRenderer.MeasureText(button.Text,button.Font).Width>button.Width-36)
                    throw new Exception("Compact toolbar label is too wide: "+button.Text);
        }
        SetCompact(false);
        Console.WriteLine("Full Settings and compact toolbar checks passed at default and minimum sizes.");
        var previousIcon=brandWindowIcon;
        preferences.Save(new WindowPreferences{closeToTray=true,minimizeToTray=true,accentColor="#A8CEFF"});
        ApplyAccent();
        preferences.Reload();
        if(DashboardAccent.Selected.ToArgb()!=Color.FromArgb(168,206,255).ToArgb()||
           preferences.Value.accentColor!="#A8CEFF"||brandWindowIcon==previousIcon||tray.Icon!=brandWindowIcon)
            throw new Exception("Saved accent or runtime icon was not applied.");
        Color contrast;
        if(DashboardAccent.TryParse("#101010",out contrast)||DashboardAccent.TryParse("blue",out contrast))
            throw new Exception("Invalid or low-contrast accent accepted.");
        Console.WriteLine("Accent checks passed: validation, persisted color, and window/tray icon update.");Close();Application.DoEvents();if(IsDisposed||Visible||!tray.Visible)throw new Exception("Close to tray failed.");RestoreWindow();if(!Visible||tray.Visible)throw new Exception("Restore failed.");WindowState=FormWindowState.Minimized;Application.DoEvents();if(Visible||!tray.Visible)throw new Exception("Minimize to tray failed.");RestoreWindow();preferences.Save(new WindowPreferences{startMinimized=true});PrepareStartup();if(!startHidden)throw new Exception("Start minimized preference not applied.");Hide();Show();Application.DoEvents();if(Visible||!tray.Visible)throw new Exception("Start minimized did not stay hidden.");RestoreWindow();if(!Visible||tray.Visible)throw new Exception("Initially hidden window did not restore.");ExitApplication();if(!IsDisposed)throw new Exception("Explicit Exit did not quit.");Console.WriteLine("Lifecycle form checks passed: close/minimize to tray, restore, start-hidden selection and explicit Exit. Isolated preferences; no Windows startup change.");}
    protected override void SetVisibleCore(bool value){if(value&&startHidden){startHidden=false;tray.Visible=true;if(!IsHandleCreated)CreateHandle();value=false;}base.SetVisibleCore(value);}
    internal void PrepareStartup(){startHidden=preferences!=null&&preferences.Value.startMinimized;}
    internal void ExitApplication(){exitRequested=true;Close();}
    protected override void OnFormClosing(FormClosingEventArgs e){if(!exitRequested&&e.CloseReason==CloseReason.UserClosing&&preferences!=null&&preferences.Value.closeToTray){e.Cancel=true;HideToTray();return;}base.OnFormClosing(e);}
    ToolStripMenuItem Command(string text,string glyph,Action action,Keys keys){var item=new ToolStripMenuItem(text,DashboardMenuRenderer.Glyph(glyph),(s,e)=>action()){ShortcutKeys=keys,ForeColor=Ink};return item;}
    void EditCatalog()
    {
        try
        {
            using(var editor=new CatalogEditor(Path.Combine(AppData.Root,"catalog.json"),activeSection,sidebarEditor.LabelFor))
                if(editor.ShowDialog(this)==DialogResult.OK)Reload();
        }
        catch(Exception error)
        {
            MessageBox.Show(this,error.Message,"Edit dashboard links",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
    }
    internal void PreviewSidebarEditor(){sidebarEditor.BeginEdit();}
    internal void PreviewDone(){SetCompact(true);checklistView.ShowDone();}
    internal void RenderMenu(string output,int index=0){var menu=(ToolStripMenuItem)appMenu.Items[index];menu.DropDown.CreateControl();menu.DropDown.Size=menu.DropDown.GetPreferredSize(Size.Empty);menu.DropDown.PerformLayout();using(var image=new Bitmap(menu.DropDown.Width,menu.DropDown.Height)){menu.DropDown.DrawToBitmap(image,new Rectangle(Point.Empty,menu.DropDown.Size));image.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
    void About(){using(var dialog=new DashboardAbout())dialog.ShowDialog(this);}
    void ShowGuide(){using(var dialog=new DashboardHelp())dialog.ShowDialog(this);}
    protected override bool ProcessCmdKey(ref Message message,Keys keyData){if(keyData==Keys.F1){ShowGuide();return true;}return base.ProcessCmdKey(ref message,keyData);}
    internal void SetPinned(bool value){TopMost=value;SyncPin();}
    void SyncPin(){if(pinMenu!=null)pinMenu.Checked=TopMost;if(pinWindow!=null){((DashboardButton)pinWindow).IsChecked=TopMost;pinWindow.Text=compact?"On top":"Always on top";}}
    void HideToTray(){tray.Visible=true;Hide();}
    internal void SelectSection(string section){activeSection=section;UpdatePage();Rebuild();}
    void UpdatePage(){pageTitle.Text=sidebarEditor==null?SidebarStore.DefaultLabel(activeSection):sidebarEditor.LabelFor(activeSection);foreach(var pair in navigation){pair.Value.BackColor=pair.Key==activeSection?Color.FromArgb(69,69,69):Color.FromArgb(56,56,56);pair.Value.ForeColor=pair.Key==activeSection?Accent:Ink;}rootButton.Visible=activeSection=="software"||activeSection=="website";var root=entries==null?null:entries.FirstOrDefault(e=>e.id==(activeSection=="software"?"software-folder":"websites-folder"));pageSubtitle.Text=(activeSection=="software"||activeSection=="website")&&root!=null?root.targets[0].path:activeSection=="media"?"Broadcast projects, Paranormal Archive and its media catalog.":activeSection=="all"?"Every saved destination, in one place.":"Open original files, folders and websites.";LayoutViews();}
    void Reload(){try{entries=AppData.Load();if(sidebarEditor!=null&&!sidebarEditor.IsEditing)sidebarEditor.ReloadSaved();UpdatePage();Rebuild();agentView.Reload(entries);checklistView.Reload(entries);status.Text=entries.Count+" saved destinations · Review build";}catch(Exception error){status.Text="Could not load the directory.";MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    internal void SetCompact(bool value){if(value&&sidebarEditor!=null&&sidebarEditor.IsEditing){status.Text="Save or Cancel editing before switching to compact view.";return;}if(compact==value)return;if(WindowState!=FormWindowState.Normal)WindowState=FormWindowState.Normal;if(value)directorySize=Size;else widgetSize=Size;compact=value;((DashboardButton)modeButton).ShowGlyph=((DashboardButton)pinWindow).ShowGlyph=((DashboardButton)hideWindow).ShowGlyph=!value;MinimumSize=value?new Size(380,300):new Size(760,540);header.Visible=body.Visible=appMenu.Visible=!value;agentView.Visible=value&&showReports;checklistView.Visible=value&&!showReports;status.Visible=!value;sidebarEditor.Visible=!value;foreach(var nav in navigation.Values)nav.Visible=!value;brandView.Visible=brandTitle.Visible=brandSubtitle.Visible=!value;modeButton.Text=value?"Dashboard":"Current Work";Size=value?widgetSize:directorySize;if(value){agentView.Reload(entries);checklistView.Reload(entries);}LayoutViews();PositionCards();}
    void LayoutViews(){if(refreshButton==null||arrangingViews)return;arrangingViews=true;try{int width=ClientSize.Width,height=ClientSize.Height;if(compact){modeBar.AutoScroll=false;modeBar.AutoScrollMinSize=Size.Empty;modeBar.AutoScrollPosition=Point.Empty;modeBar.SetBounds(0,0,width,48);if(modeButton.Image==modeGlyph)modeButton.Image=DashboardMenuRenderer.Glyph("dashboard");modeButton.SetBounds(8,6,112,36);pinWindow.SetBounds(126,6,100,36);hideWindow.Text="Hide";hideWindow.SetBounds(232,6,Math.Max(80,width-240),36);agentView.Visible=showReports;checklistView.Visible=!showReports;agentView.SetBounds(0,48,width,Math.Max(0,height-48));checklistView.SetBounds(0,48,width,Math.Max(0,height-48));}
        else{if(modeButton.Image!=modeGlyph){if(modeButton.Image!=null)modeButton.Image.Dispose();modeButton.Image=modeGlyph;}int side=width<1000?222:252;const int menuHeight=37;appMenu.SetBounds(0,0,width,menuHeight);modeBar.SetBounds(0,menuHeight,side,Math.Max(0,height-menuHeight));int sidebarMinimum=140+sidebarEditor.ContentHeight+26+132;bool scroll=modeBar.Height<sidebarMinimum;modeBar.AutoScroll=true;if(!scroll)modeBar.AutoScrollPosition=Point.Empty;Point offset=modeBar.AutoScrollPosition;int contentWidth=side-(scroll?SystemInformation.VerticalScrollBarWidth:0),buttonWidth=contentWidth-36;brandView.SetBounds(18+offset.X,16+offset.Y,48,48);brandTitle.SetBounds(18+offset.X,80+offset.Y,buttonWidth,28);brandSubtitle.SetBounds(18+offset.X,114+offset.Y,buttonWidth,20);sidebarEditor.SetBounds(18+offset.X,140+offset.Y,buttonWidth,sidebarEditor.ContentHeight);sidebarEditor.Arrange();int footer=Math.Max(140+sidebarEditor.ContentHeight+26,modeBar.Height-132);modeButton.SetBounds(18+offset.X,footer+offset.Y,buttonWidth,34);pinWindow.SetBounds(18+offset.X,footer+40+offset.Y,buttonWidth,34);hideWindow.Text="Hide to tray";hideWindow.SetBounds(18+offset.X,footer+80+offset.Y,buttonWidth,34);modeBar.AutoScrollMinSize=scroll?new Size(0,sidebarMinimum):Size.Empty;header.SetBounds(side,menuHeight,Math.Max(0,width-side),154);status.SetBounds(side,Math.Max(34,height-32),width-side,32);body.SetBounds(side+30,menuHeight+154,Math.Max(0,width-side-60),Math.Max(0,height-menuHeight-186));ArrangeHeader();}SyncPin();modeBar.BringToFront();appMenu.BringToFront();}finally{arrangingViews=false;}}
    void ArrangeHeader(){if(rootButton==null||compact)return;int reserve=body.VerticalScroll.Visible?SystemInformation.VerticalScrollBarWidth:0,end=header.ClientSize.Width-30-reserve;pageTitle.SetBounds(30,24,Math.Max(50,end-30),40);pageSubtitle.SetBounds(30,67,Math.Max(50,end-30),22);bool root=activeSection=="software"||activeSection=="website";int refreshLeft=end-(root?254:100);searchField.SetBounds(30,104,Math.Max(60,refreshLeft-42),40);refreshButton.SetBounds(refreshLeft,104,100,40);rootButton.SetBounds(end-142,104,142,40);}
    protected override void WndProc(ref Message message){if(message.Msg==0x8031){RestoreWindow();return;}if(message.Msg==0x8032){showReports=false;SetCompact(true);RestoreWindow();return;}base.WndProc(ref message);}
    internal void CheckEditorIntegration(){sidebarEditor.VerifyLocked();SelectSection("software");var original=OrderedRows("software").Select(e=>e.id).ToList();sidebarEditor.BeginViaControlForTest();if(!sidebarEditor.IsEditing||!left.SelectMany(c=>c.Controls.OfType<DestinationRow>()).All(r=>r.AllowDrop&&!r.Controls.OfType<TextBox>().Any()))throw new Exception("Project edit mode is not reorder-only.");sidebarEditor.TestRename("software","My apps");var retainedRow=left.SelectMany(c=>c.Controls.OfType<DestinationRow>()).First(r=>r.EntryId==original[1]);retainedRow.TestReorderKey(Keys.Up);if(retainedRow.IsDisposed)throw new Exception("Item reorder rebuilt the list.");if(OrderedRows("software")[0].id!=original[1])throw new Exception("Draft keyboard item reorder failed.");sidebarEditor.CancelEdit();if(OrderedRows("software")[0].id!=original[0]||pageTitle.Text!="Software Projects")throw new Exception("Cancel did not discard shared draft.");sidebarEditor.BeginEdit();sidebarEditor.TestRename("software","My apps");sidebarEditor.MoveProject("software",original[1],original[0],original);sidebarEditor.MoveBefore("media","software");sidebarEditor.SaveEdit();sidebarEditor.VerifyLocked();if(pageTitle.Text!="My apps"||OrderedRows("software")[0].id!=original[1])throw new Exception("Shared editor save failed.");if(left.SelectMany(c=>c.Controls.OfType<DestinationRow>()).Any(r=>r.AllowDrop))throw new Exception("Project rows did not relock.");SelectSection("media");var media=OrderedRows("media");if(!media.Any(e=>e.id=="paranormal-archive")||!media.Any(e=>e.id=="pa-media-catalog"))throw new Exception("PA navigation is buried.");AppData.Resolve(entries,"paranormal-archive","artwork-review");Console.WriteLine("Integrated editor checks passed: shared alias/item-order draft, locked project names, Cancel, Save/relock, Broadcast & Media and exact PA artwork target.");}
    internal void CheckRefinements(){foreach(var size in new[]{new Size(1280,860),new Size(760,540),new Size(1000,700),new Size(1160,820),new Size(1280,860)}){Size=size;LayoutViews();PositionCards();Application.DoEvents();LayoutViews();Application.DoEvents();if(modeBar.VerticalScroll.Visible!=(modeBar.Height<(140+sidebarEditor.ContentHeight+26+132)))throw new Exception("Sidebar scrollbar visibility mismatch at "+size);sidebarEditor.VerifyEditControl();if(appMenu.Bounds!=new Rectangle(0,0,ClientSize.Width,37)||modeBar.Top!=appMenu.Bottom||header.Top!=appMenu.Bottom)throw new Exception("Menu and content geometry mismatch.");if(modeBar.HorizontalScroll.Visible)throw new Exception("Sidebar has horizontal scrolling.");if(body.HorizontalScroll.Visible)throw new Exception("Directory has horizontal scrolling at "+size);foreach(Control control in modeBar.Controls)if(control.Visible&&(control.Left<0||control.Right>modeBar.ClientSize.Width))throw new Exception("Sidebar control clipped: "+control.Text);if(rootButton.Right>header.ClientSize.Width-30)throw new Exception("Header folder action overflows.");if(rootButton.Right+header.Left!=body.Left+body.ClientSize.Width)throw new Exception("Header folder action does not align with list content.");SelectSection("all");foreach(var card in left)foreach(Button button in card.Controls.OfType<Button>())if(button.Right>card.ClientSize.Width||button.Left<0)throw new Exception("Card action overflows.");SelectSection("software");}pinMenu.PerformClick();if(!TopMost||!pinMenu.Checked||!((DashboardButton)pinWindow).IsChecked)throw new Exception("Pin checked state mismatch.");pinMenu.PerformClick();if(TopMost||pinMenu.Checked||((DashboardButton)pinWindow).IsChecked)throw new Exception("Pin unchecked state mismatch.");var hashes=new HashSet<string>();foreach(string kind in new[]{"dashboard","checklist","reports","help","info"})using(var icon=DashboardMenuRenderer.Glyph(kind))using(var memory=new MemoryStream()){icon.Save(memory,System.Drawing.Imaging.ImageFormat.Png);using(var sha=SHA256.Create())hashes.Add(Convert.ToBase64String(sha.ComputeHash(memory.ToArray())));}if(hashes.Count!=5)throw new Exception("Semantic command icons are duplicated.");using(var guide=new DashboardHelp())guide.Verify();Console.WriteLine("Refinement checks passed: normal/small resize bounds, conditional scrollbar, no horizontal clipping, header/card alignment, pin/check states, distinct semantic icons and guide search.");}
    internal void CheckSidebarRegression(){
        for(int pass=0;pass<3;pass++){
            SetCompact(true);Application.DoEvents();
            modeBar.Controls.SetChildIndex(brandView,1);
            modeBar.Controls.SetChildIndex(brandTitle,modeBar.Controls.Count-1);
            modeButton.PerformClick();Application.DoEvents();
            if(compact||!brandView.Visible||!brandTitle.Visible||!brandSubtitle.Visible)throw new Exception("Dashboard transition lost sidebar branding.");
            foreach(var size in new[]{new Size(760,540),new Size(1000,700),new Size(1280,860)}){
                Size=size;LayoutViews();Application.DoEvents();
                if(brandView.Size!=new Size(48,48)||brandTitle.Width<100)throw new Exception("Sidebar identity/geometry changed after child reorder.");
                Invalidate(true);Update();
                using(var image=new Bitmap(Width,Height))DrawToBitmap(image,new Rectangle(Point.Empty,Size));
            }
            UpdateBrandIcon();
            foreach(var entry in entries){var icon=ImageFor(entry);if(icon==null)throw new Exception("Missing icon "+entry.id);using(var image=new Bitmap(48,48))using(var graphics=Graphics.FromImage(image))graphics.DrawImage(icon,new Rectangle(0,0,48,48));}
            modeButton.PerformClick();Application.DoEvents();
            foreach(var size in new[]{new Size(380,300),new Size(460,660)}){Size=size;Application.DoEvents();Invalidate(true);Update();using(var image=new Bitmap(Width,Height))DrawToBitmap(image,new Rectangle(Point.Empty,Size));}
        }
        Console.WriteLine("Sidebar regression passed: child-order mutation, 6 Dashboard/Current Work button transitions, full/compact resize and repaint, brand icon refresh and all catalog icon paints.");
    }
    internal void CheckViews(){var before=Size;modeButton.PerformClick();Application.DoEvents();if(!compact||body.Visible||header.Visible||!checklistView.Visible)throw new Exception("Compact view transition failed.");Size=new Size(410,430);modeButton.PerformClick();Application.DoEvents();if(compact||!body.Visible||checklistView.Visible||Size!=before)throw new Exception("Dashboard view restoration failed.");SelectSection("website");if(pageTitle.Text!=sidebarEditor.LabelFor("website"))throw new Exception("Sidebar navigation failed.");search.Text="VoiceDub";if(activeSection!="all")throw new Exception("Global search did not select all links.");search.Text="";SelectSection("software");modeButton.PerformClick();if(Size!=new Size(410,430))throw new Exception("Compact size was not retained.");HideToTray();RestoreWindow();Application.DoEvents();if(!Visible||tray.Visible)throw new Exception("Hide and restore failed.");Console.WriteLine("Native view checks passed: sidebar, compact/dashboard navigation, independent sizes and approved hide/restore.");}
    internal void RestoreWindow(){Show();WindowState=FormWindowState.Normal;Activate();tray.Visible=false;}
    internal static Image ReadIcon(string source){
        byte[] bytes=File.ReadAllBytes(source);int count=bytes.Length>=6?BitConverter.ToUInt16(bytes,4):0;int best=-1,bestLength=0,bestScore=Int32.MaxValue;
        for(int i=0;i<count;i++){int pos=6+i*16;if(pos+16>bytes.Length)break;int width=bytes[pos]==0?256:bytes[pos],length=BitConverter.ToInt32(bytes,pos+8),offset=BitConverter.ToInt32(bytes,pos+12);if(offset<0||length<8||offset>bytes.Length-length)continue;if(bytes[offset]!=137||bytes[offset+1]!=80||bytes[offset+2]!=78||bytes[offset+3]!=71)continue;int score=-width;if(score<bestScore){best=offset;bestLength=length;bestScore=score;}}
        if(best>=0){using(var stream=new MemoryStream(bytes,best,bestLength))using(var image=Image.FromStream(stream))return new Bitmap(image);}
        using(var icon=new Icon(source,32,32))return icon.ToBitmap();
    }
    internal static Image ReadBrandImage()
    {
        var image=(Bitmap)ReadIcon(AppData.IconPath);
        var color=DashboardAccent.Selected;
        if(color==DashboardAccent.Default)return image;
        for(int y=0;y<image.Height;y++)
            for(int x=0;x<image.Width;x++)
            {
                Color pixel=image.GetPixel(x,y);
                if(pixel.A==0||pixel.R<23||pixel.G<=pixel.R||pixel.B<=pixel.R)continue;
                double blend=Math.Min(1,(pixel.R-22)/132.0);
                image.SetPixel(x,y,Color.FromArgb(pixel.A,
                    ShiftChannel(pixel.R,color.R-154,blend),
                    ShiftChannel(pixel.G,color.G-217,blend),
                    ShiftChannel(pixel.B,color.B-193,blend)));
            }
        return image;
    }

    static int ShiftChannel(int original,int offset,double blend)
    {
        return Math.Max(0,Math.Min(255,original+(int)Math.Round(offset*blend)));
    }

    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    void UpdateBrandIcon()
    {
        var previousImage=brandImage;
        brandImage=ReadBrandImage();
        brandView.Image=brandImage;
        if(previousImage!=null)previousImage.Dispose();

        Icon updated;
        if(DashboardAccent.Selected==DashboardAccent.Default)
            updated=new Icon(AppData.IconPath);
        else
        {
            using(var frame=new Bitmap(brandImage,32,32))
            {
                IntPtr handle=frame.GetHicon();
                try { using(var native=Icon.FromHandle(handle))updated=(Icon)native.Clone(); }
                finally { DestroyIcon(handle); }
            }
        }
        Icon=updated;
        tray.Icon=updated;
        if(brandWindowIcon!=null)brandWindowIcon.Dispose();
        brandWindowIcon=updated;
    }

    internal Image ImageFor(Entry entry){
        string source=entry.iconPath;
        if(String.IsNullOrEmpty(source)&&entry.iconAsset=="local-dashboard.svg")
        {
            Image logo;
            if(!images.TryGetValue("dashboard-brand",out logo))images["dashboard-brand"]=logo=ReadBrandImage();
            return logo;
        }
        if(!String.IsNullOrEmpty(source)){
            if(images.ContainsKey(source))return images[source];Image image=null;
            try{if(Path.GetExtension(source).Equals(".ico",StringComparison.OrdinalIgnoreCase)){image=ReadIcon(source);}else{using(var stream=File.OpenRead(source))using(var original=Image.FromStream(stream))image=new Bitmap(original);}}catch{}
            images[source]=image;return image;
        }
        string key="symbol:"+entry.section;if(images.ContainsKey(key))return images[key];
        var bitmap=new Bitmap(32,32);using(var g=Graphics.FromImage(bitmap))using(var pen=new Pen(Accent,1.8f)){g.SmoothingMode=SmoothingMode.AntiAlias;if(entry.section=="workflows"){g.DrawRectangle(pen,11,3,10,6);g.DrawRectangle(pen,2,23,10,6);g.DrawRectangle(pen,20,23,10,6);g.DrawLine(pen,16,9,16,16);g.DrawLine(pen,7,16,25,16);g.DrawLine(pen,7,16,7,23);g.DrawLine(pen,25,16,25,23);}else{g.DrawLines(pen,new[]{new Point(8,3),new Point(19,3),new Point(25,9),new Point(25,29),new Point(8,29),new Point(8,3)});g.DrawLines(pen,new[]{new Point(19,3),new Point(19,10),new Point(25,10)});g.DrawLine(pen,12,16,21,16);g.DrawLine(pen,12,22,21,22);}}
        images[key]=bitmap;return bitmap;
    }
    internal void RenderIconAudit(string output){var catalog=AppData.Load();using(var sheet=new Bitmap(880,((catalog.Count+1)/2)*66+45)){using(var g=Graphics.FromImage(sheet)){g.Clear(Background);using(var font=new Font("Segoe UI",10))using(var brush=new SolidBrush(Ink)){g.DrawString("Actual dashboard icons at 32 pixels — original sources retained",font,brush,14,12);for(int i=0;i<catalog.Count;i++){int x=14+(i%2)*440,y=46+(i/2)*66;using(var view=new IconView{Size=new Size(32,32),BackColor=Surface,Image=ImageFor(catalog[i])})using(var tile=new Bitmap(32,32)){view.DrawToBitmap(tile,new Rectangle(0,0,32,32));g.DrawImageUnscaled(tile,x,y);}g.DrawString(catalog[i].name,font,brush,x+44,y+7);}}}sheet.Save(output,System.Drawing.Imaging.ImageFormat.Png);}}
    bool Matches(Entry entry){string query=search.Text.Trim();return query.Length==0||(entry.name+" "+entry.description+" "+entry.section+" "+String.Join(" ",entry.targets.Select(t=>t.path??t.url))).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;}
    internal static bool EntryInSection(Entry e,string section){return section=="all"||e.section==section||(section=="media"&&new[]{"dev-broadcastforge","dev-clipforge","paranormal-archive","pa-media-catalog"}.Contains(e.id));}
    List<Entry> OrderedRows(string section){var values=entries.Where(e=>EntryInSection(e,section)).ToList();var custom=sidebarEditor==null?new List<string>():sidebarEditor.OrderFor(section);return custom.Count==0?values.OrderByDescending(e=>pinned.Contains(e.id)).ThenBy(e=>e.name,StringComparer.CurrentCultureIgnoreCase).ThenBy(e=>e.id,StringComparer.Ordinal).ToList():values.OrderBy(e=>custom.Contains(e.id)?custom.IndexOf(e.id):Int32.MaxValue).ThenBy(e=>e.name,StringComparer.CurrentCultureIgnoreCase).ThenBy(e=>e.id,StringComparer.Ordinal).ToList();}
    Panel MakeCard(string section,string title,string rootId,bool collapse){
        if(sidebarEditor!=null)title=sidebarEditor.LabelFor(section);var rows=OrderedRows(section).Where(Matches).ToList();if(rows.Count==0)return null;
        int top=42;var root=entries.FirstOrDefault(e=>e.id==rootId);if(root!=null)top=64;
        if(activeSection!="all"){top=0;root=null;collapse=false;}var card=new Card {Tag=section,Height=top+rows.Sum(e=>DestinationRow.HeightFor(e))+2};
        if(collapse){var toggle=LinkButton((expanded||search.Text.Length>0?"▾ ":"▸ ")+title);toggle.SetBounds(16,10,260,30);toggle.TextAlign=ContentAlignment.MiddleLeft;toggle.Click+=(s,e)=>{expanded=!expanded;Rebuild();};card.Controls.Add(toggle);if(!expanded&&search.Text.Length==0){card.Height=42;return card;}}
        else if(activeSection=="all"){var heading=new Label {Text=title,UseMnemonic=false,Left=18,Top=9,Width=300,Height=26,Font=new Font("Segoe UI Variable Text",12,FontStyle.Bold),ForeColor=Ink};card.Controls.Add(heading);}
        if(root!=null){var open=LinkButton("Open folder");open.Image=DashboardMenuRenderer.Glyph("folder");open.Width=124;open.Height=34;open.Top=6;open.Anchor=AnchorStyles.Top|AnchorStyles.Left;card.Controls.Add(open);open.Click+=(s,e)=>Open(root,root.targets[0]);open.Left=Math.Max(12,card.ClientSize.Width-open.Width-18);tips.SetToolTip(open,root.targets[0].path);var pathLabel=new Label {Text=root.targets[0].path,Left=18,Top=39,Height=19,ForeColor=Muted,Font=new Font("Consolas",8),AutoEllipsis=true,UseMnemonic=false};card.Controls.Add(pathLabel);card.Resize+=(s,e)=>{open.Left=Math.Max(12,card.ClientSize.Width-open.Width-18);pathLabel.Width=card.Width-36;};}
        int rowTop=top;for(int i=0;i<rows.Count;i++){var row=new DestinationRow(rows[i],ImageFor(rows[i]),pinned,Open,TogglePin,tips,sidebarEditor!=null&&sidebarEditor.IsEditing,section,(id,target,after)=>{sidebarEditor.MoveProject(section,id,target,OrderedRows(section).Select(e=>e.id).ToList(),after);FocusEntry(id);},(id,direction)=>{sidebarEditor.MoveProjectBy(section,id,direction,OrderedRows(section).Select(e=>e.id).ToList());FocusEntry(id);});row.Left=1;row.Top=rowTop;rowTop+=row.Height;card.Controls.Add(row);card.Resize+=(s,e)=>row.Width=card.Width-2;}
        return card;
    }
    bool editorRows;
    void RefreshEditor(){UpdatePage();search.Enabled=!sidebarEditor.IsEditing;editorRows=sidebarEditor.IsEditing;foreach(var row in left.SelectMany(c=>c.Controls.OfType<DestinationRow>()))row.SetEditing(editorRows);foreach(var card in left){var rows=card.Controls.OfType<DestinationRow>().OrderBy(r=>r.Top).ToList();if(rows.Count==0)continue;string section=Convert.ToString(card.Tag);if(String.IsNullOrEmpty(section))continue;int top=rows.Min(r=>r.Top);foreach(var entry in OrderedRows(section)){var row=rows.FirstOrDefault(r=>r.EntryId==entry.id);if(row!=null){row.Top=top;top+=row.Height;}}foreach(var label in card.Controls.OfType<Label>())if(label.Font.Bold)label.Text=sidebarEditor.LabelFor(section);}}
    void Rebuild(){if(entries==null)return;body.SuspendLayout();foreach(Control child in body.Controls.Cast<Control>().ToArray())child.Dispose();left.Clear();right.Clear();secondary=null;body.AutoScrollPosition=Point.Empty;
        if(activeSection=="media")AddCard(left,MakeCard("media","Broadcast & Media",null,false));
        if(activeSection=="all"||activeSection=="software")AddCard(left,MakeCard("software","Software projects","software-folder",false));
        if(activeSection=="all"||activeSection=="website")AddCard(left,MakeCard("website","Website projects","websites-folder",false));
        if(activeSection=="all"||activeSection=="documents")AddCard(left,MakeCard("documents","Development documents",null,false));
        if(activeSection=="all"||activeSection=="everyday")AddCard(left,MakeCard("everyday","Websites I use",null,false));
        if(activeSection=="all"||activeSection=="workflows")AddCard(left,MakeCard("workflows","Workflows",null,false));
        if(activeSection=="all"||activeSection=="secondary")AddCard(left,MakeCard("secondary","Other workspaces",null,false));
        if(left.Count+right.Count==0&&secondary==null){var empty=new Label {Text="No matching links.",ForeColor=Muted,Location=new Point(24,12),AutoSize=true};body.Controls.Add(empty);}
        body.ResumeLayout();PositionCards();
    }
    void AddCard(List<Panel> column,Panel card){if(card!=null){column.Add(card);body.Controls.Add(card);}}
    void PositionCards(){if(layingOut||body.IsDisposed)return;layingOut=true;try{int total=left.Sum(card=>card.Height+16)+8;bool needsScroll=total>body.Height;int width=Math.Max(100,body.Width-(needsScroll?SystemInformation.VerticalScrollBarWidth:0)),y=0;Point offset=body.AutoScrollPosition;foreach(var card in left){card.SetBounds(0,y+offset.Y,width,card.Height);y+=card.Height+16;}body.AutoScrollMinSize=new Size(0,total);body.PerformLayout();int actual=Math.Min(width,body.ClientSize.Width);foreach(var card in left)card.Width=actual;ArrangeHeader();}finally{layingOut=false;}}
    void FocusEntry(string id){foreach(var card in left){var row=card.Controls.OfType<DestinationRow>().FirstOrDefault(r=>r.EntryId==id);if(row!=null){row.Focus();break;}}}
    void TogglePin(string id){if(!pinned.Add(id))pinned.Remove(id);try{Directory.CreateDirectory(Path.GetDirectoryName(pinPath));File.WriteAllText(pinPath,AppData.Json.Serialize(pinned.ToArray()));}catch{status.Text="Could not save pinned links.";}Rebuild();}
    void Open(Entry entry,LinkTarget target){try{AppData.Open(entries,entry.id,target.id);status.Text="Opened "+entry.name+".";}catch(Exception error){status.Text="Could not open "+entry.name+".";MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
    protected override void Dispose(bool disposing){if(disposing&&!disposedResources){disposedResources=true;tray.Visible=false;if(tray.ContextMenuStrip!=null)tray.ContextMenuStrip.Dispose();tray.Dispose();if(brandWindowIcon!=null)brandWindowIcon.Dispose();if(brandImage!=null)brandImage.Dispose();foreach(var image in images.Values)if(image!=null)image.Dispose();tips.Dispose();}base.Dispose(disposing);}
}
internal static class LocalDashboard {
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll")]static extern bool SetForegroundWindow(IntPtr handle);
    [DllImport("user32.dll")]static extern bool ShowWindow(IntPtr handle,int command);
    [DllImport("user32.dll")]static extern bool PostMessage(IntPtr handle,int message,IntPtr wParam,IntPtr lParam);
    [STAThread]static int Main(string[] args){bool startup=args.Length==1&&args[0]=="--startup";if(startup)args=new string[0];bool startCompact=args.Length==1&&args[0]=="--current-work";bool diagnostic=args.Length>0&&args[0].StartsWith("--",StringComparison.Ordinal)&&!startCompact;if(startCompact)args=new string[0];
        try{
            if(diagnostic)Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);var entries=AppData.Load();
            if(args.Length==1&&args[0]=="--verify"){
                int local=0,web=0,icons=0;var sizes=new List<object>();using(var form=new DashboardForm()){foreach(var entry in entries){foreach(var target in entry.targets){AppData.Validate(target);if(target.kind=="web")web++;else local++;}var image=form.ImageFor(entry);if(image==null)throw new Exception("Icon unavailable: "+entry.name);sizes.Add(new {name=entry.name,width=image.Width,height=image.Height});icons++;}}
                Console.WriteLine(AppData.Json.Serialize(new {entries=entries.Count,localTargets=local,webTargets=web,icons=icons,iconSizes=sizes}));return 0;
            }
            if(args.Length==2&&args[0]=="--render-icons-check"){string output=Path.GetFullPath(args[1]);if(!output.StartsWith(Path.Combine(AppData.Root,".dev","Review")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(output)!=".png")throw new Exception("Invalid review output.");Directory.CreateDirectory(Path.GetDirectoryName(output));using(var form=new DashboardForm())form.RenderIconAudit(output);return 0;}
            if(args.Length==1&&args[0]=="--pointer-review"){string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","pointer-layout.json");using(var form=new DashboardForm(file,Path.Combine(Path.GetDirectoryName(file),"pointer-window-preferences.json"),new MemoryStartupEntry())){form.Text="Local Dashboard — Pointer test 1.5.6 (isolated layout)";Application.Run(form);}return 0;}
            if(args.Length==1&&args[0]=="--window-self-test"){ReorderDrag.SelfTest();WindowPreferenceStore.SelfTest();string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","lifecycle.json");if(File.Exists(file))File.Delete(file);using(var form=new DashboardForm(null,file,new MemoryStartupEntry())){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();form.CheckLifecycle();}return 0;}
            if(args.Length==1&&args[0]=="--sidebar-self-test"){SidebarStore.SelfTest();SidebarEditor.SelfTest();string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","integrated-test.json");if(File.Exists(file))File.Delete(file);using(var form=new DashboardForm(file)){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();form.CheckEditorIntegration();form.ExitApplication();}return 0;}
            if(args.Length==1&&args[0]=="--refinement-self-test"){using(var form=new DashboardForm()){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();form.CheckRefinements();form.ExitApplication();}return 0;}
            if(args.Length==4&&(args[0]=="--render-help-check"||args[0]=="--render-about-check")){string output=Path.GetFullPath(args[1]);if(!output.StartsWith(Path.Combine(AppData.Root,".dev","Review")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(output)!=".png")throw new Exception("Invalid review output.");Directory.CreateDirectory(Path.GetDirectoryName(output));using(DashboardDialog dialog=args[0]=="--render-help-check"?(DashboardDialog)new DashboardHelp():new DashboardAbout()){dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(-32000,-32000);dialog.Size=new Size(Int32.Parse(args[2]),Int32.Parse(args[3]));dialog.Show();Application.DoEvents();using(var bitmap=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(bitmap,new Rectangle(Point.Empty,dialog.Size));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}Console.WriteLine("Themed titlebar attribute applied: "+dialog.TitlebarThemeApplied);dialog.Close();}return 0;}
            if(args.Length==1&&args[0]=="--work-self-test"){ChecklistStore.SelfTest(entries);using(var form=new CurrentWorkView()){form.Size=new Size(440,600);form.CheckViews(entries);}return 0;}
            if(args.Length==1&&args[0]=="--work-read"){Console.WriteLine(AppData.Json.Serialize(new ChecklistStore(ChecklistStore.FilePath,entries).Read()));return 0;}
            if(args.Length==2&&args[0]=="--work-apply"){string input=Path.GetFullPath(args[1]);if(!input.StartsWith(Path.Combine(AppData.Root,".local")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(input)!=".json"||new FileInfo(input).Length>16384)throw new Exception("Checklist patch must be a small JSON file in the dashboard .local folder.");var patch=AppData.Json.Deserialize<WorkPatch>(File.ReadAllText(input));Console.WriteLine(AppData.Json.Serialize(new ChecklistStore(ChecklistStore.FilePath,entries).Upsert(patch)));return 0;}
            if(args.Length==1&&args[0]=="--sidebar-regression"){using(var form=new DashboardForm()){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();form.CheckSidebarRegression();form.ExitApplication();}return 0;}
            if(args.Length==1&&args[0]=="--current-work-regression"){using(var form=new CurrentWorkView()){form.CheckAssistantViews(entries);}return 0;}
            if(args.Length==1&&args[0]=="--ui-self-test"){using(var form=new DashboardForm()){form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Show();Application.DoEvents();form.CheckViews();form.ExitApplication();}return 0;}
            if(args.Length==1&&args[0]=="--status-self-test"){AgentFeed.SelfTest(entries);return 0;}
            if(args.Length==1&&args[0]=="--status-check"){var feed=AgentFeed.Read(entries);Console.WriteLine(feed==null?"No status feed connected.":AppData.Json.Serialize(feed));return 0;}
            if(args.Length==2&&args[0]=="--check"){AppData.Parse(args[1],entries);return 0;}
            if(args.Length==3&&args[0]=="--open-check"){AppData.Open(entries,args[1],args[2]);Console.WriteLine("Opened catalog destination.");return 0;}
            if((args.Length==4&&args[0]=="--render-check")||(args.Length==4&&args[0]=="--render-status-check")||(args.Length==4&&args[0]=="--render-websites-check")||(args.Length==4&&args[0]=="--render-menu-check")||(args.Length==4&&args[0]=="--render-work-check")||(args.Length==4&&args[0]=="--render-work-done-check")||(args.Length==4&&args[0]=="--render-view-menu-check")||(args.Length==4&&args[0]=="--render-help-menu-check")||(args.Length==4&&args[0]=="--render-sidebar-edit-check")||(args.Length==4&&args[0]=="--render-media-check")){
                string output=Path.GetFullPath(args[1]);if(!output.StartsWith(Path.Combine(AppData.Root,".dev","Review")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetExtension(output)!=".png")throw new Exception("Review output must stay in the project review folder.");
                Directory.CreateDirectory(Path.GetDirectoryName(output));using(var form=new DashboardForm()){if(args[0]=="--render-status-check"){form.showReports=true;form.SetCompact(true);}if(args[0]=="--render-work-check")form.SetCompact(true);if(args[0]=="--render-work-done-check")form.PreviewDone();if(args[0]=="--render-websites-check")form.SelectSection("website");if(args[0]=="--render-media-check")form.SelectSection("media");form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-32000,-32000);form.Size=new Size(Int32.Parse(args[2]),Int32.Parse(args[3]));form.Show();Application.DoEvents();if(args[0]=="--render-sidebar-edit-check"){form.PreviewSidebarEditor();Application.DoEvents();}if(args[0]=="--render-menu-check")form.RenderMenu(output);else if(args[0]=="--render-view-menu-check"){form.SetPinned(true);form.RenderMenu(output,1);}else if(args[0]=="--render-help-menu-check")form.RenderMenu(output,2);else using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png);}form.ExitApplication();}return 0;
            }
            if(args.Length>1||diagnostic)throw new Exception("Unsupported dashboard command.");
            if(args.Length==1){var destination=AppData.Parse(args[0],entries);if(destination!=null){AppData.Open(entries,destination.Item1,destination.Item2);return 0;}}
            string key;using(var sha=SHA256.Create())key=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Application.ExecutablePath.ToLowerInvariant()))).Replace("-","").Substring(0,20);
            bool created;using(var mutex=new Mutex(true,"Local\\LocalDashboard-"+key,out created)){if(!created){IntPtr window=FindWindow(null,AppData.WindowTitle);if(window!=IntPtr.Zero)PostMessage(window,startCompact?0x8032:0x8031,IntPtr.Zero,IntPtr.Zero);return 0;}var form=new DashboardForm();if(startCompact)form.SetCompact(true);form.PrepareStartup();Application.Run(form);}return 0;
        }catch(Exception error){if(diagnostic)Console.Error.WriteLine(error.ToString());else MessageBox.Show(error.Message,"Local Dashboard",MessageBoxButtons.OK,MessageBoxIcon.Warning);return 1;}
    }
}































