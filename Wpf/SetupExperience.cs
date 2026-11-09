using System;
using System.IO;
using System.Text;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

internal sealed partial class WpfDashboard {
    internal static bool NeedsFirstSetup(string profile){return !Directory.Exists(Path.Combine(profile,".local"));}
    void RecordSetupVisit(){DashboardData.Atomic(StatePath("setup.json"),Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"visited\":true}"));}
    void ShowSetup(){ShowSetup(false);}
    void ShowSetup(bool firstRun){
        if(Moving)throw new IOException("Save or cancel the current move first.");
        var root=new DockPanel{Margin=new Thickness(28)};var progress=WorkText("",true);DockPanel.SetDock(progress,Dock.Top);root.Children.Add(progress);
        var actions=new WrapPanel{HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};DockPanel.SetDock(actions,Dock.Bottom);root.Children.Add(actions);
        var host=new ScrollViewer{VerticalScrollBarVisibility=ScrollBarVisibility.Auto};root.Children.Add(host);
        var welcome=new StackPanel();welcome.Children.Add(new TextBlock{Text="Make Dashboard yours",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,18)});welcome.Children.Add(WorkText("Dashboard brings your project links and three task sections together. You can skip setup and change these choices later in Settings."));welcome.Children.Add(WorkText("Agent tasks",true));welcome.Children.Add(WorkText("A saved text or Markdown list maintained by your agent. Dashboard reads it; it does not send instructions or change the original."));welcome.Children.Add(WorkText("Personal tasks",true));welcome.Children.Add(WorkText("Your separate text or Markdown document. Add or edit tasks in Dashboard, or use your usual editor. Save updates the selected file."));welcome.Children.Add(WorkText("Checklists",true));welcome.Children.Add(WorkText("The list you manage in Dashboard. Add, edit and check off tasks; changes save automatically. Its default location is inside your Dashboard data folder."));
        Action saveLists;var locations=BuildWorkSource(out saveLists);
        var finish=new StackPanel();finish.Children.Add(new TextBlock{Text="Know where the window goes",FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,8,0,18)});
        finish.Children.Add(WorkText("X button",true));finish.Children.Add(WorkText(preferenceStore.Value.closeToTray?"Currently, X hides Dashboard in the system tray near the Windows clock. It keeps running.":"Currently, X quits Dashboard."));
        finish.Children.Add(WorkText("Minimize button",true));finish.Children.Add(WorkText(preferenceStore.Value.minimizeToTray?"Currently, minimize also hides Dashboard in the system tray.":"Currently, minimize leaves Dashboard on the taskbar."));
        finish.Children.Add(WorkText("Double-click the tray icon to bring Dashboard back. File > Exit or the tray menu's Exit always quits."));finish.Children.Add(WorkText("Change these choices in Settings > Window. Settings > Appearance controls the theme, accent and task-button labels. Help > Run setup again returns here."));finish.Children.Add(WorkText("Finish saves only the list locations you changed. It does not turn on Windows startup or connect online accounts."));
        var pages=new FrameworkElement[]{welcome,locations,finish};int page=0;bool completed=false;var dialog=Dialog("Set up Local Dashboard",760,720,root);
        var skip=StandardAction(new Button{Content="Skip setup",IsCancel=true});var back=StandardAction(ActionButton("Back",()=>{}));var next=StandardAction(ActionButton("Next",()=>{}));actions.Children.Add(skip);actions.Children.Add(back);actions.Children.Add(next);
        Action render=()=>{progress.Text="Step "+(page+1)+" of 3";host.Content=pages[page];host.ScrollToHome();back.IsEnabled=page>0;next.Content=page==2?"Finish":"Next";};
        back.Click+=(s,e)=>{if(page>0){page--;render();}};
        next.Click+=(s,e)=>Protect(()=>{if(page<2){page++;render();return;}saveLists();RecordSetupVisit();completed=true;if(!nonWindowedVerification)dialog.DialogResult=true;});
        render();try{presentDialog(dialog);}finally{if(firstRun&&!completed)RecordSetupVisit();}
    }
    void ShowIconLicense(){
        string text="";foreach(string resource in new[]{"Fluent.LICENSE","Fluent.NOTICE"})using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)){if(stream==null)throw new IOException("The icon license resource is missing.");using(var reader=new StreamReader(stream))text+=reader.ReadToEnd()+"\r\n\r\n";}
        var root=new DockPanel{Margin=new Thickness(28)};var close=StandardAction(new Button{Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right});close.Margin=new Thickness(0,18,0,0);DockPanel.SetDock(close,Dock.Bottom);root.Children.Add(close);root.Children.Add(new TextBox{Text=text,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto});presentDialog(Dialog("Interface icon license",720,620,root));
    }
}
