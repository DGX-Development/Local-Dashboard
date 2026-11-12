using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Collections.Generic;

internal sealed class PersonalTextSnapshot {
    internal WorkSourceChoice Source;
    internal int List;
    internal string Selection,Text,Hash;
    internal byte[] Bytes,Preamble;
    internal Encoding Encoding;
}
internal sealed class PersonalEditRecovery {
    public string path {get;set;}
    public string before {get;set;}
    public string after {get;set;}
}
internal static class PersonalTextStore {
    static readonly Encoding Utf8=new UTF8Encoding(false,true);
    internal static string RecoveryPath(string profile,int list=1){return Path.Combine(profile,".local",list==1?"personal-edit-recovery.json":"task-list-"+(list+1)+"-edit-recovery.json");}
    internal static PersonalTextSnapshot Open(string profile,int list=1){
        string label="Task list "+(list+1);
        string token=WorkSources.Token(profile,list);var source=WorkSources.Read(profile,list);
        if(CloudSources.IsCloud(source.kind))throw new IOException("Choose a local text file in Settings > Tasks before editing this task list.");
        byte[] bytes=DashboardData.ReadBounded(source.path,1048576);Encoding encoding=Utf8;int bom=0;
        if(bytes.Take(4).SequenceEqual(new byte[]{255,254,0,0})){encoding=new UTF32Encoding(false,true,true);bom=4;}
        else if(bytes.Take(4).SequenceEqual(new byte[]{0,0,254,255})){encoding=new UTF32Encoding(true,true,true);bom=4;}
        else if(bytes.Take(3).SequenceEqual(new byte[]{239,187,191})){encoding=Utf8;bom=3;}
        else if(bytes.Take(2).SequenceEqual(new byte[]{255,254})){encoding=new UnicodeEncoding(false,true,true);bom=2;}
        else if(bytes.Take(2).SequenceEqual(new byte[]{254,255})){encoding=new UnicodeEncoding(true,true,true);bom=2;}
        string text=encoding.GetString(bytes,bom,bytes.Length-bom);
        if(text.Contains('\0'))throw new IOException("This is not a supported text document.");
        if(WorkSources.Token(profile,list)!=token||WorkSources.Identity(WorkSources.Read(profile,list))!=WorkSources.Identity(source))throw new IOException("The "+label+" location changed. Reopen the editor.");
        return new PersonalTextSnapshot{Source=source,List=list,Selection=token,Text=text,Hash=DashboardData.Hash(bytes),Bytes=bytes,Preamble=bytes.Take(bom).ToArray(),Encoding=encoding};
    }
    internal static string WithTimestamp(string before,string after){
        if(before==after)return before;
        int first=ReadableTasks.Parse(after).Blocks.Where(b=>b.Kind=="task").Select(b=>b.Start).DefaultIfEmpty(after.Length).First();
        var matches=Regex.Matches(after.Substring(0,first),@"(?m)^Updated:[^\r\n]*");
        if(matches.Count==1){var m=matches[0];after=after.Remove(m.Index,m.Length).Insert(m.Index,"Updated: "+DateTime.UtcNow.ToString("MMMM dd, yyyy, HH:mm:ss 'UTC'",System.Globalization.CultureInfo.InvariantCulture));}
        return after;
    }
    internal static string Save(string profile,PersonalTextSnapshot snapshot,string text,Action afterWrite=null){
        if(text==snapshot.Text)return "No changes to save.";
        text=WithTimestamp(snapshot.Text,text);
        if(text.Contains('\0'))throw new IOException("The document cannot contain NUL characters.");
        byte[] next=snapshot.Preamble.Concat(snapshot.Encoding.GetBytes(text)).ToArray();
        if(next.Length>1048576)throw new IOException("Keep this text document within 1 MiB. Nothing was saved.");
        int list=snapshot.List;
        string label="Task list "+(list+1);
        string journal=RecoveryPath(profile,list);bool locked=false;
        using(var mutex=new Mutex(false,ChecklistStore.LockName(WorkSources.Config(profile,list))))try{
            try{locked=mutex.WaitOne(0);}catch(AbandonedMutexException){locked=true;}
            if(!locked)throw new IOException(label+" is busy. Try saving again.");
            if(WorkSources.Token(profile,list)!=snapshot.Selection||WorkSources.Identity(WorkSources.Read(profile,list))!=WorkSources.Identity(snapshot.Source))throw new IOException("The "+label+" location changed. Your draft is still here; copy it before reopening the editor.");
            // Deny writes and replacement while checking and saving this exact file.
            using(var stream=new FileStream(snapshot.Source.path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){
                if(stream.Length>1048576)throw new IOException("The file changed outside Dashboard. Your draft is still here.");
                byte[] current;using(var memory=new MemoryStream()){stream.CopyTo(memory);current=memory.ToArray();}
                if(DashboardData.Hash(current)!=snapshot.Hash)throw new IOException("The file changed outside Dashboard. Nothing was overwritten. Copy your draft, cancel, then reopen the latest file.");
                if(File.Exists(journal)){
                    var previous=AppData.Json.Deserialize<PersonalEditRecovery>(Utf8.GetString(DashboardData.ReadBounded(journal,4194304)));
                    if(previous==null||!String.Equals(previous.path,snapshot.Source.path,StringComparison.OrdinalIgnoreCase)||!new[]{previous.before,previous.after}.Contains(Convert.ToBase64String(current)))throw new IOException("An interrupted edit needs review. Recovery copy: "+journal);
                    File.Delete(journal);
                }
                // Keep a durable original until the exclusive write is flushed and verified.
                DashboardData.Atomic(journal,Utf8.GetBytes(AppData.Json.Serialize(new PersonalEditRecovery{path=snapshot.Source.path,before=Convert.ToBase64String(current),after=Convert.ToBase64String(next)})));
                try{
                    stream.Position=0;stream.Write(next,0,next.Length);stream.SetLength(next.Length);stream.Flush(true);
                    if(afterWrite!=null)afterWrite();
                    stream.Position=0;using(var check=new MemoryStream()){stream.CopyTo(check);if(!check.ToArray().SequenceEqual(next))throw new IOException("Saved text could not be verified.");}
                }catch{
                    try{stream.Position=0;stream.Write(current,0,current.Length);stream.SetLength(current.Length);stream.Flush(true);File.Delete(journal);}catch{throw new IOException("The save was interrupted. Keep this recovery copy for repair: "+journal);}
                    throw;
                }
            }
            string warning="";try{File.Delete(journal);}catch(IOException){warning=" A recovery record was retained.";}
            var cache=new WorkSourceCache{schemaVersion=1,source=WorkSources.Identity(snapshot.Source),text=text,hash=DashboardData.Hash(Utf8.GetBytes(text)),refreshedAt=DateTimeOffset.UtcNow.ToString("o")};
            try{WorkSources.Store(profile,snapshot.Source,snapshot.Selection,cache,list);}catch(Exception){return "Saved to the task file. Select Refresh to update the display."+warning;}
            return label+" saved."+warning;
        }finally{if(locked)mutex.ReleaseMutex();}
    }
}

// Structured editing patches only recognized fields; unfamiliar layouts use exact text.
internal sealed class PersonalTaskDraft {
    internal string Title,Summary,Notes,Body,Number;
    internal bool Simple;
    readonly string original,newline,indent;
    readonly ReadableTaskBlock block;
    readonly Match title;
    int summaryStart=-1,summaryLength,notesStart=-1,notesLength;
    string summaryRaw,notesRaw;
    internal PersonalTaskDraft(string document,ReadableTaskBlock selected){
        original=document;block=selected;Number=block.Number;Title=block.Title;
        string raw=document.Substring(block.Start,block.Length);
        newline=Regex.Match(raw,@"\r\n|\r|\n").Value;if(newline.Length==0)newline=Environment.NewLine;
        title=Regex.Match(raw,@"\A[ \t]*\d+(?:\.\d+)*[.)][ \t]+(?<title>[^\r\n]*)(?<eol>\r\n|\r|\n|$)");
        Body=raw.Substring(title.Length);indent=Regex.Match(Body,@"(?m)^[ \t]+(?=\S)").Value;if(indent.Length==0)indent="   ";
        Simple=!block.Ambiguous;
        int position=0;var used=new HashSet<string>();
        foreach(Match part in Regex.Matches(Body,@"[^\r\n]*(?:\r\n|\r|\n|$)")){
            if(part.Length==0)continue;string line=part.Value.Trim();position=part.Index;
            if(line.Length==0)continue;
            var field=Regex.Match(line,@"^([\w /-]+):\s*(.*)$");string name=field.Success?field.Groups[1].Value.ToLowerInvariant():"";
            if(name=="notes"||name=="details"){
                int colon=Body.IndexOf(':',part.Index);notesStart=colon+1;
                while(notesStart<Body.Length&&(Body[notesStart]==' '||Body[notesStart]=='\t'))notesStart++;
                if(notesStart<Body.Length&&(Body[notesStart]=='\r'||Body[notesStart]=='\n'))notesStart=part.Index+part.Length;
                notesLength=Body.Length-notesStart;while(notesLength>0&&Char.IsWhiteSpace(Body[notesStart+notesLength-1]))notesLength--;
                notesRaw=Body.Substring(notesStart,notesLength);Notes=Display(notesRaw);break;
            }
            if(field.Success){if(!new[]{"status","next","task","description"}.Contains(name)||!used.Add(name))Simple=false;}
            if(name=="status"||name=="next")continue;
            if(summaryStart<0&&(name==""||name=="task"||name=="description")){
                summaryStart=part.Index+(name==""?part.Value.Length-part.Value.TrimStart(' ','\t').Length:part.Value.IndexOf(':')+1);
                while(summaryStart<Body.Length&&(Body[summaryStart]==' '||Body[summaryStart]=='\t'))summaryStart++;
                summaryLength=part.Index+part.Length-summaryStart;
            }else if(summaryStart>=0&&!field.Success&&part.Index==summaryStart+summaryLength){summaryLength+=part.Length;}
            else Simple=false;
        }
        if(summaryStart>=0){while(summaryLength>0&&Char.IsWhiteSpace(Body[summaryStart+summaryLength-1]))summaryLength--;summaryRaw=Body.Substring(summaryStart,summaryLength);Summary=Display(summaryRaw);}else Summary="";
        Notes=Notes??"";
        // Multi-line Status/Next or unusual field order stays in the text editor.
        if(Simple&&ReadableTasks.Parse(raw).Blocks.First().Summary!=null&&Display(ReadableTasks.Parse(raw).Blocks.First().Summary)!=Summary)Simple=false;
    }
    static string Display(string value){return String.Join("\n",(value??"").Replace("\r\n","\n").Replace('\r','\n').Split('\n').Select(l=>l.TrimStart(' ','\t'))).TrimEnd();}
    string Lines(string value){return Display(value).Replace("\n",newline+indent);}
    internal string Apply(string newTitle,string summary,string notes,string rawBody){
        newTitle=(newTitle??"").Trim();if(newTitle.Length==0||newTitle.Contains('\r')||newTitle.Contains('\n'))throw new IOException("Enter a short title on one line.");
        string body=Body;
        if(Simple){
            var edits=new List<Tuple<int,int,string>>();
            if(summary!=Summary){if(summaryStart>=0)edits.Add(Tuple.Create(summaryStart,summaryLength,Lines(summary)));else edits.Add(Tuple.Create(0,0,indent+Lines(summary)+newline));}
            if(notes!=Notes){if(notesStart>=0)edits.Add(Tuple.Create(notesStart,notesLength,(notesRaw.Length>0&&Char.IsWhiteSpace(notesRaw[0])?indent:"")+Lines(notes)));else {int at=body.TrimEnd().Length;edits.Add(Tuple.Create(at,0,newline+newline+indent+"Notes:"+newline+indent+Lines(notes)));}}
            foreach(var edit in edits.AsEnumerable().Reverse().OrderByDescending(e=>e.Item1))body=body.Remove(edit.Item1,edit.Item2).Insert(edit.Item1,edit.Item3);
        }else body=rawBody;
        string line=title.Value;line=line.Remove(title.Groups["title"].Index,title.Groups["title"].Length).Insert(title.Groups["title"].Index,newTitle);
        if(body.Length>0&&title.Groups["eol"].Length==0)line+=newline;
        string replacement=line+body;
        if(newTitle==Title&&body==Body)return original;
        if(block.Start+block.Length<original.Length&&!replacement.EndsWith("\n")&&!replacement.EndsWith("\r"))replacement+=newline;
        if(Simple&&(ReadableTasks.Parse(replacement).Blocks.Count(b=>b.Kind=="task")!=1||ReadableTasks.Parse(replacement).Blocks.Any(b=>b.Kind=="heading")))throw new IOException("Use Edit document for additional numbered tasks or section headings.");
        return original.Remove(block.Start,block.Length).Insert(block.Start,replacement);
    }
    internal static string Move(string document,int from,int to){
        var blocks=ReadableTasks.Parse(document).Blocks;
        var tasks=blocks.Where(block=>block.Kind=="task").ToList();
        int first=Math.Min(from,to),last=Math.Max(from,to);
        int start=tasks[first].Start,end=tasks[last].Start+tasks[last].Length;
        if(blocks.Any(block=>block.Kind=="heading"&&block.Start>start&&block.Start<end))
            throw new IOException("Use Edit document to move a task across headings.");

        var slots=tasks.Skip(first).Take(last-first+1)
            .Select(block=>document.Substring(block.Start,block.Length)).ToArray();
        var contents=slots.Select(slot=>slot.TrimEnd('\r','\n')).ToList();
        string moved=contents[from-first];
        contents.RemoveAt(from-first);
        contents.Insert(to-first,moved);
        for(int index=0;index<slots.Length;index++)
            contents[index]+=slots[index].Substring(slots[index].TrimEnd('\r','\n').Length);
        return document.Substring(0,start)+String.Concat(contents)+document.Substring(end);
    }
    internal static string Add(string document,string title,string summary,string notes){
        if(String.IsNullOrWhiteSpace(title)||title.Contains('\n')||title.Contains('\r'))throw new IOException("Enter a short title on one line.");
        var parsed=ReadableTasks.Parse(document);int number=1;
        var numbers=new HashSet<string>(parsed.Blocks.Where(b=>b.Kind=="task").Select(b=>b.Number));while(numbers.Contains(number.ToString()))number++;
        string nl=Regex.Match(document,@"\r\n|\r|\n").Value;if(nl.Length==0)nl=Environment.NewLine;
        string task=number+". "+title.Trim()+nl+(String.IsNullOrWhiteSpace(summary)?"":"   "+Display(summary).Replace("\n",nl+"   ")+nl)+(String.IsNullOrWhiteSpace(notes)?"":nl+"   Notes:"+nl+"   "+Display(notes).Replace("\n",nl+"   ")+nl)+nl;
        if(ReadableTasks.Parse(task).Blocks.Count(b=>b.Kind=="task")!=1||ReadableTasks.Parse(task).Blocks.Any(b=>b.Kind=="heading"))throw new IOException("Use Edit document for additional numbered tasks or section headings.");
        var last=parsed.Blocks.LastOrDefault(block=>block.Kind=="task");
        int at=last==null?document.Length:last.Start+last.Length;
        if(at>0&&!document.Substring(0,at).EndsWith(nl+nl,StringComparison.Ordinal))task=(document.Substring(0,at).EndsWith(nl,StringComparison.Ordinal)?nl:nl+nl)+task;
        return document.Insert(at,task);
    }
}
