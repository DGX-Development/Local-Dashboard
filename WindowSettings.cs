using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
public sealed class WindowPreferences {public bool closeToTray{get;set;}public bool minimizeToTray{get;set;}public bool startWithWindows{get;set;}public bool startMinimized{get;set;}}
internal interface IStartupEntry {string Read();void Write(string value);}
internal sealed class MemoryStartupEntry:IStartupEntry {string value;public string Read(){return value;}public void Write(string text){value=text;}}
internal sealed class DashboardStartup:IStartupEntry {
    readonly string name="LocalDashboard"+(AppData.IsReview?".Review.1.4.3":"");
    const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
    public string Read(){using(var key=Registry.CurrentUser.OpenSubKey(Key)){object value=key==null?null:key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);if(value!=null&&!(value is string))throw new IOException("Unexpected Windows startup entry. Nothing was changed.");return (string)value;}}
    public void Write(string value){using(var key=Registry.CurrentUser.CreateSubKey(Key)){if(value==null)key.DeleteValue(name,false);else key.SetValue(name,value,RegistryValueKind.String);}}
}
internal sealed class WindowPreferenceStore {
    internal static readonly string FilePath=Path.Combine(AppData.Root,".local",AppData.IsReview?"window-preferences-review-1.4.3.json":"window-preferences.json");
    readonly string file;readonly IStartupEntry startup;string token;internal WindowPreferences Value;
    internal WindowPreferenceStore(string path,IStartupEntry backend){file=path;startup=backend;Reload();}
    internal void Reload(){token=File.Exists(file)?File.ReadAllText(file):null;Value=token==null?new WindowPreferences{closeToTray=true,minimizeToTray=true}:AppData.Json.Deserialize<WindowPreferences>(token);if(Value==null)throw new IOException("Window settings are invalid; no settings were changed.");}
    internal static string StartupCommand(string executable){return "\""+executable+"\" --startup";}
    internal void Save(WindowPreferences value){bool locked=false;using(var mutex=new Mutex(false,ChecklistStore.LockName(file))){try{try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}if(!locked)throw new IOException("Window settings are busy. Try again.");string current=File.Exists(file)?File.ReadAllText(file):null;if(current!=token)throw new IOException("Window settings changed elsewhere. Reopen Settings and try again.");string command=StartupCommand(Application.ExecutablePath),previous=startup.Read();if(previous!=null&&previous!=command)throw new IOException("The startup entry points elsewhere. It was left unchanged.");string desired=value.startWithWindows?command:null;bool changed=desired!=previous;var raw=current==null?new Dictionary<string,object>():AppData.Json.DeserializeObject(current) as Dictionary<string,object>;if(raw==null)throw new IOException("Invalid window settings.");raw["closeToTray"]=value.closeToTray;raw["minimizeToTray"]=value.minimizeToTray;raw["startWithWindows"]=value.startWithWindows;raw["startMinimized"]=value.startMinimized;
        string temporary=file+"."+Guid.NewGuid().ToString("N")+".tmp";try{if(changed){startup.Write(desired);if(startup.Read()!=desired)throw new IOException("Windows did not retain the startup setting.");}Directory.CreateDirectory(Path.GetDirectoryName(file));File.WriteAllText(temporary,AppData.Json.Serialize(raw));if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);}catch{if(changed&&startup.Read()==desired)startup.Write(previous);throw;}finally{if(File.Exists(temporary))File.Delete(temporary);}Reload();
    }finally{if(locked)mutex.ReleaseMutex();}}}
    sealed class MemoryStartup:IStartupEntry {internal string value;public string Read(){return value;}public void Write(string text){value=text;}}
    internal static void SelfTest(){string file=Path.Combine(AppData.Root,".dev","Review","SidebarTests","window-preferences-test.json");Directory.CreateDirectory(Path.GetDirectoryName(file));if(File.Exists(file))File.Delete(file);var backend=new MemoryStartup();var store=new WindowPreferenceStore(file,backend);if(!store.Value.closeToTray||!store.Value.minimizeToTray||store.Value.startWithWindows||store.Value.startMinimized||File.Exists(file))throw new Exception("Defaults changed or created settings.");var stale=new WindowPreferenceStore(file,backend);store.Save(new WindowPreferences{closeToTray=true,minimizeToTray=true,startWithWindows=true,startMinimized=true});if(backend.value!=StartupCommand(Application.ExecutablePath)||!new WindowPreferenceStore(file,backend).Value.startMinimized)throw new Exception("Settings did not persist.");bool conflict=false;try{stale.Save(new WindowPreferences());}catch(IOException){conflict=true;}if(!conflict)throw new Exception("Stale settings overwrote changes.");store.Save(new WindowPreferences());if(backend.value!=null)throw new Exception("Startup not removed.");backend.value="External startup command";bool refused=false;try{store.Save(new WindowPreferences());}catch(IOException){refused=true;}if(!refused||backend.value!="External startup command")throw new Exception("Unowned startup entry overwritten.");Console.WriteLine("Window preference checks passed: tray defaults and opt-in startup, persistence, quoted startup command, removal, stale-write and foreign-entry protection. Memory backend only; Windows startup untouched.");}
}
internal sealed class WindowSettings:DashboardDialog {
    internal WindowSettings(WindowPreferenceStore store):base("Local Dashboard settings",new Size(660,490)){
        var title=Label("Window & startup",new Font("Segoe UI Variable Text",20,FontStyle.Bold),DashboardForm.Ink);title.SetBounds(26,20,570,45);Controls.Add(title);
        string[] labels={"Close to the system tray","Minimize to the system tray","Start Local Dashboard when I sign in to Windows","Start minimized in the system tray"};bool[] values={store.Value.closeToTray,store.Value.minimizeToTray,store.Value.startWithWindows,store.Value.startMinimized};var checks=new CheckBox[4];for(int i=0;i<4;i++){checks[i]=new CheckBox{Text=labels[i],Checked=values[i],ForeColor=DashboardForm.Ink,UseMnemonic=false};checks[i].SetBounds(28,86+i*45,590,35);Controls.Add(checks[i]);}
        var note=Label("Close to tray keeps the app running. Use File > Exit or tray > Exit to quit. Start minimized applies to both manual launches and Windows sign-in."+(AppData.IsReview?"\nWindows startup, if enabled, opens this review executable:\n":"\nStartup executable:\n")+Application.ExecutablePath,new Font("Segoe UI",9),DashboardForm.Muted);note.SetBounds(28,275,590,94);Controls.Add(note);
        var save=DashboardForm.LinkButton("Save");save.SetBounds(420,382,90,36);Controls.Add(save);var cancel=DashboardForm.LinkButton("Cancel");cancel.SetBounds(520,382,90,36);cancel.DialogResult=DialogResult.Cancel;Controls.Add(cancel);CancelButton=cancel;
        save.Click+=(s,e)=>{try{store.Save(new WindowPreferences{closeToTray=checks[0].Checked,minimizeToTray=checks[1].Checked,startWithWindows=checks[2].Checked,startMinimized=checks[3].Checked});DialogResult=DialogResult.OK;Close();}catch(Exception error){MessageBox.Show(this,error.Message,Text,MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
    }
}
