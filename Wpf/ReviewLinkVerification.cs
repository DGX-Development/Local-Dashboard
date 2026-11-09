using System;
using System.IO;
using System.Linq;
using System.Text;
internal static class ReviewLinkVerification {
    static int checks;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks++;Console.WriteLine("PASS "+text);}
    static void Reject(Action action,string text){bool failed=false;try{action();}catch(Exception e){failed=!String.IsNullOrWhiteSpace(e.Message);}Check(failed,text);}
    internal static void Run(string profile){
        string folder=Path.Combine(profile,"Shortcuts"),source=Path.Combine(profile,"review.txt");Directory.CreateDirectory(folder);File.WriteAllText(source,"Isolated review");
        ReviewLinks.SaveDestination(profile,folder);
        Reject(()=>ReviewLinks.SaveDestination(profile,folder),"stale destination edit refused");
        var task=new StatusTask{id="review-1",projectPath=profile,task="Review",state="Waiting for review",review=new StatusReview{title="Layout review",target=source,verifiedAt=DateTimeOffset.UtcNow.ToString("o")}};
        byte[] original=File.ReadAllBytes(source);string first=ReviewLinks.Create(profile,task);
        Check(File.ReadAllText(first).Contains("URL="+new Uri(source).AbsoluteUri+"\r\n"),"local exact target readback");
        Check(ReviewLinks.Create(profile,task)==first&&Directory.GetFiles(folder).Length==1,"repeat click reuses identical shortcut");
        File.WriteAllText(first,"Unrelated existing file");string second=ReviewLinks.Create(profile,task);
        Check(second!=first&&File.ReadAllText(first)=="Unrelated existing file","collision preserved; new numbered shortcut");
        Check(original.SequenceEqual(File.ReadAllBytes(source)),"original review asset unchanged");
        task.review.target="https://example.com/review?id=17#actual";string cloud=ReviewLinks.Create(profile,task);
        Check(File.ReadAllText(cloud).Contains("URL="+task.review.target+"\r\n"),"HTTPS exact target and fragment retained; no network claim");
        foreach(string bad in new[]{"javascript:alert(1)","http://example.com/review","https://user:password@example.com/review","https://example.com/\r\nIconFile=evil","file:///C:/review.txt",Path.Combine(profile,"missing.pdf"),"relative.txt"}){task.review.target=bad;Reject(()=>ReviewLinks.Create(profile,task),"reject unsafe/missing target: "+bad.Split('\r')[0]);}
        string exe=Path.Combine(profile,"review.exe");File.WriteAllText(exe,"not executable");task.review.target=exe;Reject(()=>ReviewLinks.Create(profile,task),"executable target refused");
        task.review.target=source;task.review.verifiedAt=null;Reject(()=>ReviewLinks.Create(profile,task),"missing verification timestamp refused");
        task.review.verifiedAt=DateTimeOffset.UtcNow.AddDays(1).ToString("o");Reject(()=>ReviewLinks.Create(profile,task),"future verification refused");
        task.review.verifiedAt=DateTimeOffset.UtcNow.ToString("o");Directory.Move(folder,folder+"-offline");Reject(()=>ReviewLinks.Create(profile,task),"unavailable destination gives actionable error");
        Directory.Move(folder+"-offline",folder);Check(ReviewLinks.Destination(profile)==folder,"destination persists and is discoverable from profile");
        Console.WriteLine(checks+" review-link checks passed. No desktop shortcut, network request or live report changed.");
    }
}
