using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
internal sealed class ReviewLinkSettings {public int schemaVersion{get;set;} public string destination{get;set;}}
internal static class ReviewLinks {
    internal static string Config(string profile){return Path.Combine(profile,".local","review-links.json");}
    internal static string Destination(string profile){
        string file=Config(profile);if(!File.Exists(file))return Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var value=AppData.Json.Deserialize<ReviewLinkSettings>(Encoding.UTF8.GetString(DashboardData.ReadBounded(file,16384)));
        if(value==null||value.schemaVersion!=1)throw new IOException("Review-link settings are invalid. Choose a destination in Settings.");
        return Folder(value.destination,false);
    }
    internal static string Folder(string value,bool requireExists){
        if(String.IsNullOrWhiteSpace(value)||value.Any(Char.IsControl)||value.Contains("\"")||value.StartsWith(@"\\?\")||value.StartsWith(@"\\.\")||!Path.IsPathRooted(value)||(!value.StartsWith(@"\\")&&(value.Length<3||value[1]!=':'||value[2]!='\\')))throw new IOException("Choose a full local or network destination folder.");
        string folder=Path.GetFullPath(value);
        if(requireExists&&!Directory.Exists(folder))throw new IOException("Review-link destination is unavailable. Reconnect it or choose another folder in Settings.");
        return folder;
    }
    internal static string SettingsToken(string profile){return File.Exists(Config(profile))?DashboardData.Hash(DashboardData.ReadBounded(Config(profile),16384)):null;}
    internal static void SaveDestination(string profile,string value,string expected=null){
        string destination=Folder(value,true);using(var mutex=new System.Threading.Mutex(false,ChecklistStore.LockName(Config(profile)))){bool locked=false;try{try{locked=mutex.WaitOne(0);}catch(System.Threading.AbandonedMutexException){locked=true;}if(!locked||SettingsToken(profile)!=expected)throw new IOException("Review-link destination changed in another window. Reopen Settings; existing data was preserved.");DashboardData.Atomic(Config(profile),Encoding.UTF8.GetBytes(AppData.Json.Serialize(new ReviewLinkSettings{schemaVersion=1,destination=destination})));}finally{if(locked)mutex.ReleaseMutex();}}
    }
    internal static string Target(StatusReview review){
        if(review==null||String.IsNullOrWhiteSpace(review.target)||review.target.Length>4096||review.target.Any(Char.IsControl)||review.target.Trim()!=review.target)throw new IOException("No valid exact review target was supplied.");
        DateTimeOffset stamp;
        if(String.IsNullOrWhiteSpace(review.verifiedAt)||!DateTimeOffset.TryParse(review.verifiedAt,CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out stamp)||stamp>DateTimeOffset.UtcNow.AddSeconds(30))throw new IOException("The report must supply a valid target verification time.");
        if(String.IsNullOrWhiteSpace(review.title)||review.title.Length>160||review.title.Any(Char.IsControl))throw new IOException("The report must supply a short plain-text review title.");
        string target=review.target;Uri uri;
        if(Uri.TryCreate(target,UriKind.Absolute,out uri)&&uri.Scheme=="https"){
            if(!String.IsNullOrEmpty(uri.UserInfo)||String.IsNullOrWhiteSpace(uri.Host)||target.Contains("\\")||target.Contains(" "))throw new IOException("The HTTPS review link is malformed or contains credentials.");
            return target;
        }
        if(target.StartsWith("file:",StringComparison.OrdinalIgnoreCase))throw new IOException("Supply the exact Windows path for local reviews, not a file URL.");
        Folder(target,false);
        if(!File.Exists(target)&&!Directory.Exists(target))throw new IOException("The local review target is missing or inaccessible. Its original location was preserved.");
        if(File.Exists(target)&&!new[]{".pdf",".png",".jpg",".jpeg",".webp",".txt",".md",".html",".htm",".docx",".xlsx",".pptx"}.Contains(Path.GetExtension(target).ToLowerInvariant()))throw new IOException("This review file type is unsupported. Supply a document, image, HTML review or folder; executable and shortcut targets are not accepted.");
        return new Uri(Path.GetFullPath(target)).AbsoluteUri;
    }
    static string Label(string text){var invalid=Path.GetInvalidFileNameChars();string value=new String((text??"Review").Select(c=>invalid.Contains(c)||Char.IsControl(c)?' ':c).ToArray()).Trim().TrimEnd('.');if(value.Length>70)value=value.Substring(0,70).TrimEnd();return String.IsNullOrEmpty(value)?"Review":value;}
    internal static string Create(string profile,StatusTask task){
        if(task==null||task.review==null)throw new IOException("This report has no verified review link.");
        string target=Target(task.review),destination=Folder(Destination(profile),true);
        string identity=DashboardData.Hash(Encoding.UTF8.GetBytes((task.projectPath??"")+"\n"+(task.id??"")+"\n"+target)).Substring(0,12);
        string title=Label(AgentFeed.Project(task.projectPath)+" - "+task.review.title);
        string prefix=Path.Combine(destination,title+" - "+identity);
        string body="[InternetShortcut]\r\nURL="+target+"\r\n";byte[] bytes=new UTF8Encoding(false).GetBytes(body);
        for(int n=0;n<100;n++){
            string path=prefix+(n==0?"":" ("+(n+1)+")")+".url";
            if(File.Exists(path)){
                if((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0&&new FileInfo(path).Length==bytes.Length&&DashboardData.ReadBounded(path,16384).SequenceEqual(bytes))return path;
                continue;
            }
            string temporary=Path.Combine(destination,".dashboard-review-"+Guid.NewGuid().ToString("N")+".tmp");
            try{
                using(var output=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){output.Write(bytes,0,bytes.Length);output.Flush(true);}
                if(!DashboardData.ReadBounded(temporary,16384).SequenceEqual(bytes))throw new IOException("Shortcut content verification failed.");
                File.Move(temporary,path);
            }catch(IOException){if(File.Exists(path))continue;throw new IOException("Could not create the review shortcut. Check destination availability and write permission.");}
            finally{if(File.Exists(temporary))File.Delete(temporary);}
            if(!DashboardData.ReadBounded(path,16384).SequenceEqual(bytes))throw new IOException("Shortcut readback did not match the supplied target; review it before use.");
            return path;
        }
        throw new IOException("Too many conflicting shortcut names. Choose another destination; existing files were preserved.");
    }
}
