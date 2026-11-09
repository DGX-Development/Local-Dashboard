using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
internal sealed class SourceTaskRecord {public string id{get;set;} public string title{get;set;} public string status{get;set;} public string tabId{get;set;} public string tabTitle{get;set;} public string headingId{get;set;}}
internal static class CloudSources {
    static CloudSources(){ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;}
    internal static readonly string[] Kinds={"google-docs","google-drive","dropbox","onedrive"};
    internal static bool IsCloud(string kind){return Kinds.Contains(kind);}
    internal static void Validate(WorkSourceChoice c){
        if(!IsCloud(c.kind)||String.IsNullOrWhiteSpace(c.path)||!Regex.IsMatch(c.path,@"^[A-Za-z0-9_!:\-]{1,256}$"))throw new IOException("Supply the exact provider document or file ID, not a sharing URL or local path.");
    }
    internal static string GoogleFileId(string input){
        string value=(input??"").Trim();Uri uri;
        if(Uri.TryCreate(value,UriKind.Absolute,out uri)){
            if(uri.Scheme!="https"||!String.IsNullOrEmpty(uri.UserInfo)||(uri.Host!="docs.google.com"&&uri.Host!="drive.google.com"))throw new IOException("Paste a Google Docs or Drive file link, or its file ID.");
            var match=Regex.Match(uri.AbsolutePath,@"^/(?:document|file)/d/([A-Za-z0-9_-]+)(?:/|$)");
            if(match.Success)value=match.Groups[1].Value;
            else{match=Regex.Match(uri.Query,@"(?:\?|&)id=([A-Za-z0-9_-]+)(?:&|$)");if(!match.Success)throw new IOException("This Google link does not identify a supported file.");value=match.Groups[1].Value;}
        }
        if(!Regex.IsMatch(value,@"^[A-Za-z0-9_-]{1,256}$"))throw new IOException("Paste a Google Docs or Drive file link, or its file ID.");
        return value;
    }
    internal static string GoogleReader(string metadata){
        var item=Obj(AppData.Json.DeserializeObject(metadata));
        if(Str(item,"mimeType")=="application/vnd.google-apps.document")return "google-docs";
        if(Str(item,"mimeType").StartsWith("application/vnd.google-apps.")||!new[]{".txt",".md"}.Contains(Path.GetExtension(Str(item,"name")).ToLowerInvariant()))throw new IOException("Choose a Google document, plain text (.txt) or Markdown (.md) file.");
        return "google-drive";
    }
    internal static string RequestUrl(WorkSourceChoice c){Validate(c);string id=Uri.EscapeDataString(c.path);
        switch(c.kind){case "google-docs":return "https://docs.googleapis.com/v1/documents/"+id+"?includeTabsContent=true&suggestionsViewMode=PREVIEW_WITHOUT_SUGGESTIONS";
        case "google-drive":return "https://www.googleapis.com/drive/v3/files/"+id+"?alt=media";
        case "dropbox":return "https://content.dropboxapi.com/2/files/download";
        case "onedrive":return "https://graph.microsoft.com/v1.0/me/drive/items/"+id+"/content";
        default:throw new IOException("Unsupported provider.");}
    }
    internal static byte[] ResponseBytes(Stream stream,int maximum){using(var result=new MemoryStream()){byte[] buffer=new byte[8192];int n;while((n=stream.Read(buffer,0,buffer.Length))>0){if(result.Length+n>maximum)throw new IOException("Provider response is too large; previous snapshot retained.");result.Write(buffer,0,n);}return result.ToArray();}}
    internal static string ReadHttp(string url,string accessToken,string method,string body,string contentType,string dropboxId=null){
        Uri address=new Uri(url);if(address.Scheme!="https")throw new IOException("Provider requests require HTTPS.");
        var request=(HttpWebRequest)WebRequest.Create(address);request.Method=method;request.Timeout=15000;request.ReadWriteTimeout=15000;request.AllowAutoRedirect=false;
        if(accessToken!=null)request.Headers[HttpRequestHeader.Authorization]="Bearer "+accessToken;
        if(dropboxId!=null)request.Headers["Dropbox-API-Arg"]=AppData.Json.Serialize(new Dictionary<string,string>{{"path",dropboxId}});
        if(body!=null){request.ContentType=contentType;byte[] b=Encoding.UTF8.GetBytes(body);request.ContentLength=b.Length;using(var output=request.GetRequestStream())output.Write(b,0,b.Length);}
        try{using(var response=(HttpWebResponse)request.GetResponse()){
            if((int)response.StatusCode==302&&accessToken!=null&&method=="GET"){
                Uri target;if(!Uri.TryCreate(response.Headers["Location"],UriKind.Absolute,out target)||target.Scheme!="https"||target.IsLoopback||!String.IsNullOrEmpty(target.UserInfo)||target.HostNameType!=UriHostNameType.Dns)throw new IOException("Provider returned an unsupported download location.");
                return ReadHttp(target.AbsoluteUri,null,"GET",null,null);
            }
            if((int)response.StatusCode<200||(int)response.StatusCode>299)throw new IOException("Provider returned an unsupported response.");
            return new UTF8Encoding(false,true).GetString(ResponseBytes(response.GetResponseStream(),4194304));
        }}catch(WebException e){var response=e.Response as HttpWebResponse;int status=response==null?0:(int)response.StatusCode;if(response!=null)response.Dispose();if(status==401||status==403)throw new IOException("Provider access was denied or expired. Reconnect with the required read permission; cached data is retained.");if(status==404)throw new IOException("The selected provider file was not found or is inaccessible.");if(status==429)throw new IOException("Provider rate limit reached. Wait before refreshing again.");throw new IOException("Provider request failed"+(status==0?" (offline or timed out)":" (HTTP "+status+")")+". Cached data is retained.");}
    }
    internal static string FileRevision(WorkSourceChoice c,string access){
        string id=Uri.EscapeDataString(c.path),url,method="GET",body=null;
        switch(c.kind){
            case "google-drive":url="https://www.googleapis.com/drive/v3/files/"+id+"?fields=name,mimeType,version";break;
            case "dropbox":url="https://api.dropboxapi.com/2/files/get_metadata";method="POST";body=AppData.Json.Serialize(new Dictionary<string,string>{{"path",c.path}});break;
            case "onedrive":url="https://graph.microsoft.com/v1.0/me/drive/items/"+id+"?$select=name,file,cTag,eTag";break;
            default:throw new IOException("Unsupported file provider.");
        }
        var metadata=Obj(AppData.Json.DeserializeObject(ReadHttp(url,access,method,body,body==null?null:"application/json")));
        string name=Str(metadata,"name");if(!new[]{".txt",".md"}.Contains(Path.GetExtension(name).ToLowerInvariant()))throw new IOException("Choose a plain text (.txt) or Markdown (.md) file. Google Drive also supports Google documents.");
        string revision=Str(metadata,c.kind=="google-drive"?"version":c.kind=="dropbox"?"rev":c.kind=="onedrive"?"cTag":"etag");
        if(String.IsNullOrEmpty(revision)&&c.kind=="onedrive")revision=Str(metadata,"eTag");
        if(String.IsNullOrEmpty(revision))throw new IOException("Provider omitted the file revision; no unverifiable snapshot was accepted.");return revision;
    }
    internal static WorkSourceCache Fetch(string profile,WorkSourceChoice c){
        Validate(c);string access=ProviderAuth.Access(profile,c.kind);
        return FetchConnected(c,(url,method,body,dropboxId)=>ReadHttp(url,access,method,body,null,dropboxId),()=>FileRevision(c,access));
    }
    internal static WorkSourceCache FetchConnected(WorkSourceChoice c,Func<string,string,string,string,string> read,Func<string> fileRevision){
        Validate(c);
        var reader=new WorkSourceChoice{kind=c.kind,path=c.path};
        if(c.kind=="google-drive")reader.kind=GoogleReader(read("https://www.googleapis.com/drive/v3/files/"+Uri.EscapeDataString(c.path)+"?fields=name,mimeType","GET",null,null));
        string before=reader.kind=="google-docs"?null:fileRevision();
        string raw=read(RequestUrl(reader),reader.kind=="dropbox"?"POST":"GET",reader.kind=="dropbox"?"":null,reader.kind=="dropbox"?reader.path:null);
        WorkSourceCache cache;
        if(reader.kind=="google-docs")cache=ParseGoogle(raw);
        else{if(raw.IndexOf('\0')>=0||Encoding.UTF8.GetByteCount(raw)>1048576)throw new IOException("Choose a UTF-8 TXT or Markdown file. Native or binary documents require a specific adapter.");if(fileRevision()!=before)throw new IOException("Source changed during download. Refresh again; cached data was not replaced.");cache=new WorkSourceCache{text=raw,revision=before};}
        cache.schemaVersion=1;cache.source=WorkSources.Identity(c);cache.hash=DashboardData.Hash(Encoding.UTF8.GetBytes(cache.text));cache.refreshedAt=DateTimeOffset.UtcNow.ToString("o");return cache;
    }
    static Dictionary<string,object> Obj(object value){return value as Dictionary<string,object>;}
    static object Get(Dictionary<string,object> obj,string key){object value;return obj!=null&&obj.TryGetValue(key,out value)?value:null;}
    static string Str(Dictionary<string,object> obj,string key){return Get(obj,key) as string??"";}
    static IEnumerable<object> Items(object value){return value as IEnumerable<object>??new object[0];}
    internal static WorkSourceCache ParseGoogle(string json){
        var document=Obj(AppData.Json.DeserializeObject(json));if(document==null||String.IsNullOrEmpty(Str(document,"documentId")))throw new IOException("Invalid Google document response.");
        var records=new List<SourceTaskRecord>();var text=new StringBuilder();int tabs=0;
        Action<IEnumerable<object>> walk=null;walk=list=>{foreach(var value in list){var tab=Obj(value);var properties=Obj(Get(tab,"tabProperties"));string tabId=Str(properties,"tabId"),tabTitle=Str(properties,"title");var docTab=Obj(Get(tab,"documentTab"));
            if(docTab!=null){tabs++;text.AppendLine("["+tabTitle+"]");SourceTaskRecord current=null;bool status=false;
                Action<IEnumerable<object>> content=null;content=elements=>{foreach(var element in elements){var item=Obj(element);var paragraph=Obj(Get(item,"paragraph"));if(paragraph!=null){var line=new StringBuilder();foreach(var run in Items(Get(paragraph,"elements"))){var o=Obj(run);var tr=Obj(Get(o,"textRun"));if(tr!=null)line.Append(Str(tr,"content"));else if(Get(o,"inlineObjectElement")!=null)line.Append("[embedded artwork]");}
                    string plain=line.ToString();text.Append(plain);var style=Obj(Get(paragraph,"paragraphStyle"));var match=Regex.Match(plain.Trim(),@"^#(\d+)\s+(.+)$");
                    if(match.Success){current=new SourceTaskRecord{id=match.Groups[1].Value,title=match.Groups[2].Value,tabId=tabId,tabTitle=tabTitle,headingId=Str(style,"headingId"),status=""};records.Add(current);status=true;}
                    else if(status&&!String.IsNullOrWhiteSpace(plain)){current.status=plain.Trim();status=false;}
                }var table=Obj(Get(item,"table"));if(table!=null){text.AppendLine("[Table]");foreach(var row in Items(Get(table,"tableRows")))foreach(var cell in Items(Get(Obj(row),"tableCells")))content(Items(Get(Obj(cell),"content")));}}};
                content(Items(Get(Obj(Get(docTab,"body")),"content")));text.AppendLine();
            }walk(Items(Get(tab,"childTabs")));}};walk(Items(Get(document,"tabs")));
        if(tabs==0)throw new IOException("Google response omitted document tabs; no flattened replacement was accepted.");
        return new WorkSourceCache{text=text.ToString(),rawSnapshot=json,rawHash=DashboardData.Hash(Encoding.UTF8.GetBytes(json)),records=records,revision=Str(document,"revisionId")};
    }
}
