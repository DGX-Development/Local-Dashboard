using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
public sealed class WindowPreferences {public bool closeToTray{get;set;}public bool minimizeToTray{get;set;}public bool startWithWindows{get;set;}public bool startMinimized{get;set;}public string accentColor{get;set;}public string theme{get;set;}public string darkAccent{get;set;}public string lightAccent{get;set;}public string agentTasksLabel{get;set;}public string checklistLabel{get;set;}public string personalTasksLabel{get;set;}}
internal interface IStartupEntry {string Read();void Write(string value);}
internal sealed class MemoryStartupEntry:IStartupEntry {string value;public string Read(){return value;}public void Write(string text){value=text;}}
internal sealed class DashboardStartup:IStartupEntry {
    readonly string name="LocalDashboard"+(AppData.IsReview?".Review.1.5.4":"");
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public string Read(){using(var key=Registry.CurrentUser.OpenSubKey(Key)){object value=key==null?null:key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value!=null&&!(value is string))throw new IOException("Unexpected Windows startup entry. Nothing was changed.");return (string)value;}}
    public void Write(string value){using(var key=Registry.CurrentUser.CreateSubKey(Key)){if(value==null)key.DeleteValue(name,false);else key.SetValue(name,value,RegistryValueKind.String);}}
}
internal sealed class WindowPreferenceStore {
    internal static readonly string FilePath=Path.Combine(AppData.Root,".local",AppData.IsReview?"window-preferences-review-1.5.4.json":"window-preferences.json");
    readonly string file;readonly IStartupEntry startup;string token;internal WindowPreferences Value;
    internal WindowPreferenceStore(string path,IStartupEntry backend){file=path;startup=backend;Reload();}
    internal void Reload(){token=File.Exists(file)?File.ReadAllText(file):null;Value=token==null?new WindowPreferences{closeToTray=true,minimizeToTray=true}:AppData.Json.Deserialize<WindowPreferences>(token);if(Value==null)throw new IOException("Window settings are invalid; no settings were changed.");}
    internal static string StartupCommand(string executable){return "\""+executable+"\" --startup";}
    internal void Save(WindowPreferences value){bool locked=false;using(var mutex=new Mutex(false,ChecklistStore.LockName(file))){try{try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new IOException("Window settings are busy. Try again.");string current=File.Exists(file)?File.ReadAllText(file):null;if(current!=token)throw new IOException("Window settings changed elsewhere. Reopen Settings and try again.");string command=StartupCommand(Application.ExecutablePath),previous=startup.Read();if(previous!=null&&previous!=command)throw new IOException("The startup entry points elsewhere. It was left unchanged.");string desired=value.startWithWindows?command:null;bool changed=desired!=previous;var raw=current==null?new Dictionary<string,object>():AppData.Json.DeserializeObject(current) as Dictionary<string,object>;if(raw==null)throw new IOException("Invalid window settings.");raw["closeToTray"]=value.closeToTray;raw["minimizeToTray"]=value.minimizeToTray;raw["startWithWindows"]=value.startWithWindows;raw["startMinimized"]=value.startMinimized;if(value.accentColor!=null)raw["accentColor"]=value.accentColor;if(value.theme!=null)raw["theme"]=value.theme;if(value.darkAccent!=null)raw["darkAccent"]=value.darkAccent;if(value.lightAccent!=null)raw["lightAccent"]=value.lightAccent;if(value.agentTasksLabel!=null)raw["agentTasksLabel"]=value.agentTasksLabel;if(value.checklistLabel!=null)raw["checklistLabel"]=value.checklistLabel;if(value.personalTasksLabel!=null)raw["personalTasksLabel"]=value.personalTasksLabel;
        string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{if(changed){startup.Write(desired);if(startup.Read()!=desired)throw new IOException("Windows did not retain the startup setting.");}Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(temporary,AppData.Json.Serialize(raw));if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}catch{if(changed&&startup.Read()==desired)startup.Write(previous);throw;}finally{if(File.Exists(temporary))File.Delete(temporary);}Reload();
    }finally{if(locked)mutex.ReleaseMutex();}}}
    sealed class MemoryStartup:IStartupEntry {internal string value;public string Read(){return value;}public void Write(string text){value=text;}}
    internal static void SelfTest(){string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","window-preferences-test.json");Directory.CreateDirectory(Path.GetDirectoryName(file));if(File.Exists(file))File.Delete(file);var backend=new MemoryStartup();var store=new WindowPreferenceStore(file,backend);if(!store.Value.closeToTray||!store.Value.minimizeToTray||store.Value.startWithWindows||store.Value.startMinimized||File.Exists(file))throw new Exception("Defaults changed or created settings.");var stale=new WindowPreferenceStore(file,backend);store.Save(new WindowPreferences{closeToTray=true,minimizeToTray=true,startWithWindows=true,startMinimized=true});if(backend.value!=StartupCommand(Application.ExecutablePath)||!new WindowPreferenceStore(file,backend).Value.startMinimized)throw new Exception("Settings did not persist.");bool conflict=false;try{stale.Save(new WindowPreferences());}catch(IOException){conflict=true;}if(!conflict)throw new Exception("Stale settings overwrote changes.");store.Save(new WindowPreferences());if(backend.value!=null)throw new Exception("Startup not removed.");backend.value="External startup command";bool refused=false;try{store.Save(new WindowPreferences());}catch(IOException){refused=true;}if(!refused||backend.value!="External startup command")throw new Exception("Unowned startup entry overwritten.");Console.WriteLine("Window preference checks passed: tray defaults and opt-in startup, persistence, quoted startup command, removal, stale-write and foreign-entry protection. Memory backend only; Windows startup untouched.");}
}
internal sealed class WindowSettings:DashboardDialog {
    internal WindowSettings(WindowPreferenceStore store,Action<Color> previewAccent):base("Local Dashboard settings",new Size(660,570)){
        var title=Label("Window & startup",new Font("Segoe UI Variable Text",20,FontStyle.Bold),DashboardForm.Ink);title.SetBounds(26,20,570,45);Controls.Add(title);
        string[] labels={"Close to the system tray","Minimize to the system tray","Start Local Dashboard when I sign in to Windows","Start minimized in the system tray"};bool[] values={store.Value.closeToTray,store.Value.minimizeToTray,store.Value.startWithWindows,store.Value.startMinimized};var checks=new CheckBox[4];for(int i=0;i<4;i++){checks[i]=new SettingsCheckBox{Text=labels[i],Checked=values[i],ForeColor=DashboardForm.Ink,UseMnemonic=false};checks[i].SetBounds(28,86+i*45,590,35);Controls.Add(checks[i]);}
        var accentLabel=Label("Accent color",Font,DashboardForm.Ink);
        accentLabel.SetBounds(28,270,135,30);
        Controls.Add(accentLabel);
        var accentInput=new TextBox{Text=DashboardAccent.Format(DashboardAccent.Read(store.Value.accentColor)),
            AccessibleName="Accent color hex",MaxLength=7};
        var accentField=new DashboardField(accentInput);
        accentField.SetBounds(167,265,105,34);
        Controls.Add(accentField);
        var preview=DashboardForm.LinkButton("Preview");
        preview.SetBounds(282,265,100,34);
        Controls.Add(preview);
        var feedback=Label("",Font,DashboardForm.Muted);
        feedback.SetBounds(355,310,250,28);
        Controls.Add(feedback);
        preview.Click+=(s,e)=>{
            Color color;
            if(!DashboardAccent.TryParse(accentInput.Text,out color))return;
            previewAccent(color);
            feedback.ForeColor=color;
            feedback.Text="Previewing "+DashboardAccent.Format(color);
            Invalidate(true);
        };
        var reset=DashboardForm.LinkButton("Reset");
        reset.SetBounds(510,265,100,34);
        Controls.Add(reset);
        reset.Click+=(s,e)=>accentInput.Text=DashboardAccent.DefaultHex;
        string[] presets={"#9AD9C1","#A8CEFF","#F5CC89","#DBB9FF"};
        for(int i=0;i<presets.Length;i++)
        {
            string preset=presets[i];
            var swatch=DashboardForm.LinkButton("");
            swatch.BackColor=DashboardAccent.Read(preset);
            swatch.AccessibleName="Use accent "+preset;
            swatch.SetBounds(167+i*45,307,38,26);
            Controls.Add(swatch);
            swatch.Click+=(s,e)=>accentInput.Text=preset;
        }
        accentInput.TextChanged+=(s,e)=>{
            Color color;
            bool valid=DashboardAccent.TryParse(accentInput.Text,out color);
            preview.Enabled=valid;
            feedback.ForeColor=DashboardForm.Muted;
            feedback.Text=valid?"":"Too dark or invalid";
        };
        var note=Label("Close to tray keeps the app running. Use File > Exit or tray > Exit to quit. Start minimized applies to both manual launches and Windows sign-in."+(AppData.IsReview?"\nWindows startup, if enabled, opens this review executable:\n":"\nStartup executable:\n")+Application.ExecutablePath,new Font("Segoe UI",9),DashboardForm.Muted);note.SetBounds(28,354,590,94);Controls.Add(note);
        var save=DashboardForm.LinkButton("Save");save.SetBounds(420,465,90,36);Controls.Add(save);var cancel=DashboardForm.LinkButton("Cancel");cancel.SetBounds(520,465,90,36);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
        save.Click+=(s,e)=>{try{Color accent;if(!DashboardAccent.TryParse(accentInput.Text,out accent))throw new IOException("Enter a six-digit hex color with enough contrast, or choose a preset.");store.Save(new WindowPreferences{accentColor=DashboardAccent.Format(accent),closeToTray=checks[0].Checked,minimizeToTray=checks[1].Checked,startWithWindows=checks[2].Checked,startMinimized=checks[3].Checked});DialogResult=DialogResult.OK;Close();}catch(Exception error){MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
    }
}

internal sealed class SettingsCheckBox : CheckBox
{
    protected override void OnPaint(PaintEventArgs e)
    {
        if(SystemInformation.HighContrast)
        {
            base.OnPaint(e);
            return;
        }
        e.Graphics.Clear(Parent==null?DashboardForm.Background:Parent.BackColor);
        float scale=e.Graphics.DpiX/96f;
        int size=(int)Math.Round(13*scale);
        int top=(Height-size)/2;
        var box=new Rectangle(0,top,size-1,size-1);
        e.Graphics.FillRectangle(Brushes.White,box);
        using(var border=new Pen(Color.FromArgb(112,112,112)))
            e.Graphics.DrawRectangle(border,box);
        if(Checked)
        {
            using(var mark=new Pen(Color.FromArgb(24,24,24),2.3f*scale){LineJoin=System.Drawing.Drawing2D.LineJoin.Round})
                e.Graphics.DrawLines(mark,new[]{
                    new PointF(2.5f*scale,top+6*scale),
                    new PointF(5.5f*scale,top+9*scale),
                    new PointF(10*scale,top+3*scale)});
        }
        int left=(int)Math.Round(18*scale);
        var label=new Rectangle(left,0,Width-left,Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,label,Enabled?ForeColor:DashboardForm.Muted,
            TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(left,top,Math.Min(Width-left,TextRenderer.MeasureText(Text,Font).Width),size),ForeColor,Parent.BackColor);
    }
}

internal static class DashboardAccent
{
    internal const string DefaultHex="#9AD9C1";
    internal static readonly Color Default=Color.FromArgb(154,217,193);
    internal static Color Selected=Default;
    internal static Color Read(string text)
    {
        Color result;
        return TryParse(text,out result)?result:Default;
    }

    internal static string Format(Color color)
    {
        return String.Format("#{0:X2}{1:X2}{2:X2}",color.R,color.G,color.B);
    }

    internal static bool TryParse(string text,out Color color)
    {
        color=Default;
        int rgb;
        if(String.IsNullOrEmpty(text)||text.Length!=7||text[0]!='#'||
           !Int32.TryParse(text.Substring(1),System.Globalization.NumberStyles.AllowHexSpecifier,
               System.Globalization.CultureInfo.InvariantCulture,out rgb))return false;
        Color candidate=Color.FromArgb((rgb>>16)&255,(rgb>>8)&255,rgb&255);
        double luminance=.2126*Linear(candidate.R)+.7152*Linear(candidate.G)+.0722*Linear(candidate.B);
#if !WPF_REBUILD
        if((luminance+.05)/(Linear(69)+.05)<4.5)return false;
#endif
        color=candidate;
        return true;
    }

    internal static Color ReadableText(Color color)
    {
        while((.2126*Linear(color.R)+.7152*Linear(color.G)+.0722*Linear(color.B)+.05)/(Linear(69)+.05)<4.5)
            color=Color.FromArgb(Math.Min(255,color.R+1),Math.Min(255,color.G+1),Math.Min(255,color.B+1));
        return color;
    }

    static double Linear(int component)
    {
        double value=component/255.0;
        return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);
    }
}
