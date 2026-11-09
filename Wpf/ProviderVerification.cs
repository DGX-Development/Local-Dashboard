using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
internal static class ProviderVerification {
    static int count;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);count++;Console.WriteLine("PASS "+text);}
    static void Reject(Action work,string text){bool failed=false;try{work();}catch{failed=true;}Check(failed,text);}
    internal static void Run(string profile){
        Check(ProviderAuth.Challenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk")=="E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM","RFC PKCE challenge vector");
        var registration=new ProviderRegistration{clientId="isolated-client",port=53682};
        foreach(string kind in new[]{"google-docs","google-drive","dropbox","onedrive"}){string url=ProviderAuth.Authorization(kind,registration,"test-state","test-verifier");Check(url.StartsWith("https://")&&url.Contains("code_challenge_method=S256")&&url.Contains("state=test-state")&&!url.Contains("offline_access"),kind+" PKCE/state/read scope; no persistent access requested");Reject(()=>ProviderAuth.Access(profile,kind),kind+" unauthenticated read fails explicitly");}
        Check(ProviderAuth.Callback("GET /callback?code=exact%2Bcode&state=test HTTP/1.1","test")=="exact+code","callback exact code decoding");
        Reject(()=>ProviderAuth.Callback("GET /callback?code=x&state=wrong HTTP/1.1","test"),"wrong state refused");
        Reject(()=>ProviderAuth.Callback("GET /callback?code=x&state=test&state=test HTTP/1.1","test"),"duplicate callback field refused");
        Reject(()=>ProviderAuth.Callback("GET /callback?error=access_denied&state=test HTTP/1.1","test"),"denied consent refused");
        Reject(()=>ProviderAuth.Authorization("box",registration,"test","test"),"unsupported Box auth blocked");
        Check(ProviderAuth.Authorization("dropbox",registration,"test","test").Contains("token_access_type=online"),"Dropbox explicitly requests a short-lived online token");
        Check(ProviderAuth.SessionStatus(profile,"dropbox")=="Not signed in.","registration does not imply an authenticated session");
        Check(!CloudSources.Kinds.Contains("box"),"Box removed from supported sources");
        Reject(()=>CloudSources.RequestUrl(new WorkSourceChoice{kind="box",path="123"}),"removed Box source cannot issue a request");
        Check(ProviderAuth.Scope("google-docs")==ProviderAuth.Scope("google-drive"),"legacy Google source shares the unified read permission");
        Check(CloudSources.GoogleFileId("https://docs.google.com/document/d/Abc_123/edit#heading=h.one")=="Abc_123","Google document link resolves to exact file ID");
        Check(CloudSources.GoogleFileId("https://drive.google.com/file/d/File_456/view?usp=sharing")=="File_456","Drive file link resolves to exact file ID");
        Check(CloudSources.GoogleFileId("https://drive.google.com/open?id=File_456")=="File_456","Drive open link resolves to exact file ID");
        Check(CloudSources.GoogleFileId("CaseSensitive-ID")=="CaseSensitive-ID","raw Google ID remains case sensitive");
        Reject(()=>CloudSources.GoogleFileId("https://docs.google.com.evil.example/document/d/id/edit"),"lookalike Google host rejected");
        Reject(()=>CloudSources.GoogleFileId("https://docs.google.com/spreadsheets/d/id/edit"),"unsupported Google spreadsheet link rejected");
        Check(CloudSources.GoogleReader("{\"name\":\"Work list\",\"mimeType\":\"application/vnd.google-apps.document\"}")=="google-docs","Google native document chooses structured reader");
        Check(CloudSources.GoogleReader("{\"name\":\"work.md\",\"mimeType\":\"text/plain\"}")=="google-drive","Google Markdown file chooses text reader");
        Reject(()=>CloudSources.GoogleReader("{\"name\":\"wrong.md\",\"mimeType\":\"application/vnd.google-apps.spreadsheet\"}"),"misnamed native spreadsheet rejected");
        Reject(()=>CloudSources.GoogleReader("{\"name\":\"work.pdf\",\"mimeType\":\"application/pdf\"}"),"unsupported Google PDF rejected");
        var a=new WorkSourceChoice{schemaVersion=1,kind="google-docs",path="DocCase"};var b=new WorkSourceChoice{schemaVersion=1,kind="google-docs",path="doccase"};Check(WorkSources.Identity(a)!=WorkSources.Identity(b),"case-sensitive provider IDs retained");
        b.kind="google-drive";b.path=a.path;Check(WorkSources.Identity(a)!=WorkSources.Identity(b),"providers have distinct cache identities");
        Reject(()=>CloudSources.Validate(new WorkSourceChoice{kind="google-docs",path="https://example.com"}),"sharing URL not silently treated as ID");
        string json="{\"documentId\":\"fixture\",\"revisionId\":\"rev-one\",\"tabs\":[{\"tabProperties\":{\"tabId\":\"open\",\"title\":\"Open Tasks\"},\"documentTab\":{\"body\":{\"content\":[{\"paragraph\":{\"paragraphStyle\":{\"headingId\":\"h.one\"},\"elements\":[{\"textRun\":{\"content\":\" #121  Dashboard\\n\"}}]}},{\"paragraph\":{\"elements\":[{\"textRun\":{\"content\":\"Open · review pending\\n\"}}]}}]}},\"childTabs\":[{\"tabProperties\":{\"tabId\":\"closed\",\"title\":\"Closed Tasks\"},\"documentTab\":{\"body\":{\"content\":[{\"paragraph\":{\"paragraphStyle\":{\"headingId\":\"h.two\"},\"elements\":[{\"textRun\":{\"content\":\"#121 Another occurrence\\n\"}}]}},{\"paragraph\":{\"elements\":[{\"textRun\":{\"content\":\"Done · accepted\\n\"}}]}}]}}}]}]}";
        var cache=CloudSources.ParseGoogle(json);Check(cache.records.Count==2&&cache.records[0].id=="121"&&cache.records[1].id=="121","duplicate task IDs across tabs preserved without merge");Check(cache.records[0].status=="Open · review pending"&&cache.records[1].status=="Done · accepted","literal statuses preserved");Check(cache.records[1].tabId=="closed"&&cache.records[1].headingId=="h.two"&&cache.rawSnapshot==json&&cache.revision=="rev-one","nested tabs, heading anchors, raw response and revision retained");
        var requests=new List<string>();
        var routed=CloudSources.FetchConnected(b,(url,method,body,dropboxId)=>{requests.Add(url);return url.Contains("fields=name,mimeType")?"{\"name\":\"Work\",\"mimeType\":\"application/vnd.google-apps.document\"}":json;},()=>{throw new Exception("Native document used text revision reader.");});
        Check(requests.Count==2&&requests[1].StartsWith("https://docs.googleapis.com/")&&routed.records.Count==2&&routed.source==WorkSources.Identity(b),"unified Google refresh detects native document and preserves source identity");
        int revisions=0;
        routed=CloudSources.FetchConnected(b,(url,method,body,dropboxId)=>url.Contains("fields=name,mimeType")?"{\"name\":\"work.txt\",\"mimeType\":\"text/plain\"}":"Sample work",()=>{revisions++;return "one";});
        Check(routed.text=="Sample work"&&routed.rawSnapshot==null&&revisions==2,"unified Google text refresh checks both file revisions");
        revisions=0;Reject(()=>CloudSources.FetchConnected(b,(url,method,body,dropboxId)=>url.Contains("fields=name,mimeType")?"{\"name\":\"work.txt\",\"mimeType\":\"text/plain\"}":"Changed work",()=> (++revisions).ToString()),"unified Google text refresh rejects a mid-read revision change");
        Reject(()=>CloudSources.ParseGoogle("{\"documentId\":\"fixture\",\"body\":{}}"),"flattened/missing tabs refused");
        Check(CloudSources.RequestUrl(a).Contains("includeTabsContent=true"),"Google requests all document tabs");
        ProviderAuth.Save(profile,"google-docs",registration,ProviderAuth.SettingsToken(profile));Check(ProviderAuth.Registration(profile,"google-docs").clientId==registration.clientId,"app registration roundtrip without credentials");Reject(()=>ProviderAuth.Save(profile,"google-docs",registration,null),"stale provider setup refused");
        Check(ProviderAuth.Registration(profile,"google-drive").clientId==registration.clientId,"unified Google choice reads legacy registration without migration");
        WorkSources.Save(profile,a,WorkSources.Token(profile));cache.schemaVersion=1;cache.source=WorkSources.Identity(a);cache.hash=DashboardData.Hash(System.Text.Encoding.UTF8.GetBytes(cache.text));cache.refreshedAt=DateTimeOffset.UtcNow.ToString("o");WorkSources.Store(profile,a,WorkSources.Token(profile),cache);Check(WorkSources.Cached(profile,a).records.Count==2,"structured provider snapshot roundtrip");
        cache.source=WorkSources.Identity(b);WorkSources.Save(profile,b,WorkSources.Token(profile));WorkSources.Store(profile,b,WorkSources.Token(profile),cache);Check(WorkSources.Cached(profile,b).records.Count==2,"unified Google source retains structured tabs and snapshot");
        cache.rawHash="invalid";DashboardData.Atomic(WorkSources.CachePath(profile,b),System.Text.Encoding.UTF8.GetBytes(AppData.Json.Serialize(cache)));Reject(()=>WorkSources.Cached(profile,b),"structured cache corruption refused");
        Console.WriteLine(count+" provider fixture checks passed. No browser, listener, token, credential, account access or network request was created.");
    }
}
