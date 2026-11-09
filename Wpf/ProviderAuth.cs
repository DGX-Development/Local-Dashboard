using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
internal sealed class ProviderRegistration {public string clientId{get;set;} public int port{get;set;}}
internal sealed class ProviderSession {internal string accessToken;internal DateTimeOffset expires;}
internal static class ProviderAuth {
    static readonly Dictionary<string,ProviderSession> sessions=new Dictionary<string,ProviderSession>();
    internal static string Config(string profile){return Path.Combine(profile,".local","provider-apps.json");}
    static string Key(string profile,string kind){return Path.GetFullPath(profile).ToUpperInvariant()+"|"+(kind=="google-docs"?"google-drive":kind);}
    internal static ProviderRegistration Registration(string profile,string kind){
        if(!File.Exists(Config(profile)))return new ProviderRegistration{clientId="",port=53682};
        var all=AppData.Json.Deserialize<Dictionary<string,ProviderRegistration>>(Encoding.UTF8.GetString(DashboardData.ReadBounded(Config(profile),16384)));
        ProviderRegistration result;if(all==null)throw new IOException("Provider registration settings are invalid.");return all.TryGetValue(kind=="google-docs"?"google-drive":kind,out result)||((kind=="google-drive"||kind=="google-docs")&&all.TryGetValue("google-docs",out result))?result:new ProviderRegistration{clientId="",port=53682};
    }
    internal static string SettingsToken(string profile){return File.Exists(Config(profile))?DashboardData.Hash(DashboardData.ReadBounded(Config(profile),16384)):null;}
    internal static void Save(string profile,string kind,ProviderRegistration registration,string expected){
        Validate(kind,registration);using(var mutex=new System.Threading.Mutex(false,ChecklistStore.LockName(Config(profile)))){bool locked=false;try{
            try{locked=mutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){locked=true;}if(!locked||SettingsToken(profile)!=expected)throw new IOException("Provider setup changed in another window. Reopen account setup; existing setup was preserved.");
            var all=File.Exists(Config(profile))?AppData.Json.Deserialize<Dictionary<string,ProviderRegistration>>(Encoding.UTF8.GetString(DashboardData.ReadBounded(Config(profile),16384))):new Dictionary<string,ProviderRegistration>();
            if(all==null)throw new IOException("Provider registration settings are invalid; existing data was preserved.");all[kind]=registration;DashboardData.Atomic(Config(profile),Encoding.UTF8.GetBytes(AppData.Json.Serialize(all)));
        }finally{if(locked)mutex.ReleaseMutex();}}
    }    internal static void Validate(string kind,ProviderRegistration r){if(!CloudSources.IsCloud(kind)||r==null||String.IsNullOrWhiteSpace(r.clientId)||r.clientId.Length>512||r.clientId.Any(c=>Char.IsWhiteSpace(c)||Char.IsControl(c))||r.port<1024||r.port>65535)throw new IOException("Supply a registered application/client ID and a loopback port between 1024 and 65535.");}
    internal static string Scope(string kind){switch(kind){case "google-docs":case "google-drive":return "https://www.googleapis.com/auth/drive.readonly";case "dropbox":return "files.content.read files.metadata.read";case "onedrive":return "https://graph.microsoft.com/Files.Read";default:throw new IOException("Provider sign-in is not available.");}}
    internal static string TokenUrl(string kind){return kind.StartsWith("google-")?"https://oauth2.googleapis.com/token":kind=="dropbox"?"https://api.dropboxapi.com/oauth2/token":kind=="onedrive"?"https://login.microsoftonline.com/common/oauth2/v2.0/token":null;}
    static string Base64Url(byte[] b){return Convert.ToBase64String(b).TrimEnd('=').Replace('+','-').Replace('/','_');}
    internal static string Challenge(string verifier){using(var hash=SHA256.Create())return Base64Url(hash.ComputeHash(Encoding.ASCII.GetBytes(verifier)));}
    static string Random(){byte[] data=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(data);return Base64Url(data);}
    internal static string Form(Dictionary<string,string> fields){return String.Join("&",fields.Select(p=>Uri.EscapeDataString(p.Key)+"="+Uri.EscapeDataString(p.Value)));}
    internal static string Redirect(ProviderRegistration r){return "http://127.0.0.1:"+r.port+"/callback";}
    internal static string Authorization(string kind,ProviderRegistration r,string state,string verifier){Validate(kind,r);var values=new Dictionary<string,string>{{"response_type","code"},{"client_id",r.clientId},{"redirect_uri",Redirect(r)},{"state",state},{"code_challenge",Challenge(verifier)},{"code_challenge_method","S256"},{"scope",Scope(kind)}};
        string host=kind.StartsWith("google-")?"https://accounts.google.com/o/oauth2/v2/auth":kind=="dropbox"?"https://www.dropbox.com/oauth2/authorize":"https://login.microsoftonline.com/common/oauth2/v2.0/authorize";
        if(kind.StartsWith("google-"))values["access_type"]="online";
        if(kind=="dropbox")values["token_access_type"]="online";
        return host+"?"+Form(values);
    }
    internal static string Callback(string request,string expectedState){
        string[] line=request.Split(' ');if(line.Length!=3||line[0]!="GET"||!line[1].StartsWith("/callback?",StringComparison.Ordinal)||!line[2].StartsWith("HTTP/1."))throw new IOException("Unexpected sign-in callback.");
        var fields=new Dictionary<string,string>();foreach(string part in line[1].Substring(10).Split('&')){var pair=part.Split(new[]{'='},2);string key=Uri.UnescapeDataString(pair[0]);if(fields.ContainsKey(key))throw new IOException("Duplicate sign-in response field.");fields[key]=pair.Length==2?Uri.UnescapeDataString(pair[1].Replace('+',' ')):"";}
        string state,code;if(!fields.TryGetValue("state",out state)||state!=expectedState)throw new IOException("Sign-in state did not match. No token was accepted.");if(fields.ContainsKey("error"))throw new IOException("Sign-in was denied or cancelled.");if(!fields.TryGetValue("code",out code)||String.IsNullOrWhiteSpace(code)||code.Length>8192||code.Any(Char.IsControl))throw new IOException("Sign-in returned no valid authorization code.");return code;
    }
    internal static async Task Connect(string profile,string kind,ProviderRegistration registration,string googleDesktopSecret,CancellationToken cancellation){
        Validate(kind,registration);string state=Random(),verifier=Random(),authorize=Authorization(kind,registration,state,verifier);var listener=new TcpListener(IPAddress.Loopback,registration.port);
        try{listener.Start(1);}catch(SocketException){throw new IOException("The registered loopback port is busy or unavailable. Choose another registered port; no network settings were changed.");}
        try{
            cancellation.ThrowIfCancellationRequested();Process.Start(new ProcessStartInfo(authorize){UseShellExecute=true});
            var accept=listener.AcceptTcpClientAsync();if(await Task.WhenAny(accept,Task.Delay(120000,cancellation))!=accept){ObserveAccept(accept);throw new IOException("Sign-in timed out; no access was saved.");}
            string code;
            using(var client=await accept){client.ReceiveTimeout=5000;client.SendTimeout=5000;using(var stream=client.GetStream()){
                var bytes=new List<byte>();int value;while(bytes.Count<16384&&(value=stream.ReadByte())>=0){bytes.Add((byte)value);if(bytes.Count>=4&&bytes[bytes.Count-4]==13&&bytes[bytes.Count-3]==10&&bytes[bytes.Count-2]==13&&bytes[bytes.Count-1]==10)break;}
                string request=Encoding.ASCII.GetString(bytes.ToArray());int end=request.IndexOf("\r\n");if(end<0)throw new IOException("Incomplete sign-in callback.");code=Callback(request.Substring(0,end),state);
                byte[] reply=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nReturn to Local Dashboard to check the sign-in result.");stream.Write(reply,0,reply.Length);
            }}
            var payload=new Dictionary<string,string>{{"grant_type","authorization_code"},{"client_id",registration.clientId},{"code",code},{"redirect_uri",Redirect(registration)},{"code_verifier",verifier}};
            if(kind.StartsWith("google-")&&!String.IsNullOrEmpty(googleDesktopSecret))payload["client_secret"]=googleDesktopSecret;
            string json=await Task.Run(()=>CloudSources.ReadHttp(TokenUrl(kind),null,"POST",Form(payload),"application/x-www-form-urlencoded"));
            var token=AppData.Json.Deserialize<Dictionary<string,object>>(json);object access,expiry;
            if(token==null||!token.TryGetValue("access_token",out access)||!(access is string)||String.IsNullOrWhiteSpace((string)access)||!token.TryGetValue("expires_in",out expiry))throw new IOException("Provider did not return a usable short-lived token.");
            double seconds=Convert.ToDouble(expiry);if(seconds<=0||seconds>86400)throw new IOException("Provider token lifetime is unsupported.");
            cancellation.ThrowIfCancellationRequested();lock(sessions)sessions[Key(profile,kind)]=new ProviderSession{accessToken=(string)access,expires=DateTimeOffset.UtcNow.AddSeconds(seconds)};
        }finally{listener.Stop();}
    }
    static void ObserveAccept(Task<TcpClient> accept){accept.ContinueWith(t=>{if(t.IsFaulted){var ignored=t.Exception;}else if(t.Status==TaskStatus.RanToCompletion)t.Result.Dispose();});}
    internal static string Access(string profile,string kind){lock(sessions){ProviderSession session;if(sessions.TryGetValue(Key(profile,kind),out session)&&session.expires>DateTimeOffset.UtcNow.AddSeconds(30))return session.accessToken;}throw new IOException("No active "+kind+" session. Open Settings, then Work lists & connections, to sign in. Tokens are kept only in memory; reopening requires sign-in.");}
    internal static void Disconnect(string profile,string kind){lock(sessions)sessions.Remove(Key(profile,kind));}
    internal static string SessionStatus(string profile,string kind)
    {
        lock(sessions)
        {
            ProviderSession session;
            if(!sessions.TryGetValue(Key(profile,kind),out session))return "Not signed in.";
            if(session.expires<=DateTimeOffset.UtcNow.AddSeconds(30))return "Session expired. Sign in again.";
            return "Signed in for this session until "+session.expires.ToLocalTime().ToString("g")+". Refresh the selected source to check file access.";
        }
    }
}
