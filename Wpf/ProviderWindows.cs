using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
internal sealed partial class WpfDashboard {
    void ShowProviderSetup(string selectedKind=null){
        ShowProviderHelp();
    }
    void ShowProviderSetupDevelopmentOnly(string selectedKind=null){
        if(selectedKind==null){try{selectedKind=WorkSources.Read(profile).kind;}catch(IOException){}}
        if(selectedKind=="google-docs")selectedKind="google-drive";
        string[] kinds={"google-drive","dropbox","onedrive"};
        var cancellation=new System.Threading.CancellationTokenSource();
        var body=new StackPanel{Margin=new Thickness(0,0,14,0)};
        body.Children.Add(new TextBlock{Text="Connect an online account",FontSize=22,FontWeight=FontWeights.SemiBold,Margin=new Thickness(0,0,0,12)});
        body.Children.Add(WorkText("Read a file directly from your online account. A file already synced to this computer needs no extra sign-in."));
        body.Children.Add(WorkText("Service",true));
        var provider=new ComboBox{ItemsSource=new[]{"Google Drive","Dropbox","OneDrive"},SelectedIndex=Math.Max(0,Array.IndexOf(kinds,selectedKind))};body.Children.Add(provider);
        System.Windows.Automation.AutomationProperties.SetName(provider,"Online service");
        var status=WorkText("",true);body.Children.Add(status);
        var instructions=WorkText("");body.Children.Add(instructions);
        var actions=new WrapPanel{Margin=new Thickness(0,12,0,8)};body.Children.Add(actions);
        var connect=SourceAction("Sign in…",()=>{});actions.Children.Add(connect);
        var disconnect=SourceAction("Sign out",()=>{});disconnect.Click+=(s,e)=>{ProviderAuth.Disconnect(profile,kinds[provider.SelectedIndex]);status.Text="Signed out of Dashboard.";disconnect.IsEnabled=false;};actions.Children.Add(disconnect);
        body.Children.Add(WorkText("Sign-in lasts for this app session. Closing Dashboard or an expired session requires sign-in again. Your source file is never changed."));
        var setupBody=new StackPanel();
        var setupHelp=WorkText("");setupBody.Children.Add(setupHelp);
        setupBody.Children.Add(WorkText("Application ID"));var clientId=new TextBox{MaxLength=512};setupBody.Children.Add(clientId);
        System.Windows.Automation.AutomationProperties.SetName(clientId,"Application ID");
        setupBody.Children.Add(WorkText("Callback port"));var port=new TextBox{MaxLength=5,Width=120,HorizontalAlignment=HorizontalAlignment.Left};setupBody.Children.Add(port);
        System.Windows.Automation.AutomationProperties.SetName(port,"Callback port");
        var redirect=WorkText("");setupBody.Children.Add(redirect);
        port.TextChanged+=(s,e)=>{int number;redirect.Text=Int32.TryParse(port.Text,out number)&&number>=1024&&number<=65535?"Redirect address to register: "+ProviderAuth.Redirect(new ProviderRegistration{port=number}):"Choose a port from 1024 to 65535.";};
        var secretLabel=WorkText("Google client secret, if required by your registration");setupBody.Children.Add(secretLabel);
        var secret=new PasswordBox{Height=36,MaxLength=512,Padding=new Thickness(10,4,10,4),Margin=new Thickness(0,4,0,12)};
        secret.SetResourceReference(Control.BackgroundProperty,"Theme252525");secret.SetResourceReference(Control.ForegroundProperty,"ThemeF2F2F2");secret.SetResourceReference(Control.BorderBrushProperty,"Theme4A4A4A");setupBody.Children.Add(secret);
        System.Windows.Automation.AutomationProperties.SetName(secret,"Google client secret, session only");
        var secretHint=WorkText("Used for this sign-in only; never saved.");setupBody.Children.Add(secretHint);
        string expected=ProviderAuth.SettingsToken(profile);
        var save=SourceAction("Save application setup",()=>{
            try{int number;if(!Int32.TryParse(port.Text,out number))throw new IOException("Enter a valid callback port.");
                ProviderAuth.Save(profile,kinds[provider.SelectedIndex],new ProviderRegistration{clientId=clientId.Text.Trim(),port=number},expected);
                expected=ProviderAuth.SettingsToken(profile);status.Text="Application setup saved. You can sign in now.";
            }catch(Exception error){status.Text=error.Message;}
        });setupBody.Children.Add(save);
        var advanced=new Expander{Header="Application setup (first time only)",Content=setupBody,Margin=new Thickness(0,16,0,0)};body.Children.Add(advanced);
        Action load=()=>{
            string kind=kinds[provider.SelectedIndex];secret.Password="";
            secret.Visibility=secretLabel.Visibility=secretHint.Visibility=kind=="google-drive"?Visibility.Visible:Visibility.Collapsed;
            try{
                var registration=ProviderAuth.Registration(profile,kind);clientId.Text=registration.clientId;port.Text=registration.port.ToString();
                bool configured=!String.IsNullOrWhiteSpace(registration.clientId);advanced.IsExpanded=false;
                instructions.Text=configured?"Sign in and approve read access in your browser. Then return to Agent Work and choose Refresh.":"Open Application setup below to enter your application's details. Save them, then choose Sign in. Using a synced local file avoids this setup.";
                setupHelp.Text=kind=="google-drive"?"Register a Google Desktop OAuth client. Enable Drive and Docs APIs and add your account as a test user if required. Read permission: drive.readonly.":kind=="dropbox"?"Register a Dropbox scoped app with files.content.read and files.metadata.read. Enable public clients for PKCE and register the redirect address below. Use the app key as the application ID.":"Register a Microsoft public desktop app for your account type, with delegated Files.Read permission and the redirect address below.";
                status.Text=ProviderAuth.SessionStatus(profile,kind);
                disconnect.IsEnabled=status.Text.StartsWith("Signed in");
            }catch(Exception error){status.Text=error.Message;advanced.IsExpanded=true;}
        };
        provider.SelectionChanged+=(s,e)=>load();load();
        connect.Click+=async(s,e)=>{
            string kind=kinds[provider.SelectedIndex];int number;
            if(!Int32.TryParse(port.Text,out number)){status.Text="Enter a valid callback port under Application setup.";advanced.IsExpanded=true;return;}
            var registration=new ProviderRegistration{clientId=clientId.Text.Trim(),port=number};string desktopSecret=secret.Password;secret.Password="";
            try{
                ProviderAuth.Validate(kind,registration);
                connect.IsEnabled=save.IsEnabled=disconnect.IsEnabled=provider.IsEnabled=clientId.IsEnabled=port.IsEnabled=secret.IsEnabled=false;
                status.Text="Waiting for you to finish signing in in your browser…";
                await Task.Run(()=>ProviderAuth.Connect(profile,kind,registration,desktopSecret,cancellation.Token));
                status.Text=ProviderAuth.SessionStatus(profile,kind);
            }catch(Exception error){status.Text="Sign-in did not finish. "+error.Message;}
            finally{connect.IsEnabled=save.IsEnabled=provider.IsEnabled=clientId.IsEnabled=port.IsEnabled=secret.IsEnabled=true;disconnect.IsEnabled=ProviderAuth.SessionStatus(profile,kind).StartsWith("Signed in");}
        };
        var content=new DockPanel{Margin=new Thickness(26)};
        var footer=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,18,0,0)};
        footer.Children.Add(CompactAction(new Button{Content="Close",IsCancel=true}));DockPanel.SetDock(footer,Dock.Bottom);content.Children.Add(footer);
        content.Children.Add(new ScrollViewer{Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
        try{presentDialog(Dialog("Account connection",720,700,content));}finally{cancellation.Cancel();}
    }
}
