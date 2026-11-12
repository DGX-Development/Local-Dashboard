using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Formatting is optional. Every source line belongs to one ordered block.
internal sealed class ReadableTaskBlock {
    internal string Kind,Raw,Number,Title,Summary,Status,Next,Details;
    internal bool Deferred,Ambiguous;
    internal int Start,Length;
}
internal sealed class ReadableTaskDocument {
    internal string Original;
    internal List<ReadableTaskBlock> Blocks=new List<ReadableTaskBlock>();
}
internal static class ReadableTasks {
    static readonly Regex Numbered=new Regex(@"^(\d+(?:\.\d+)*)[.)]\s+(.+)$");
    static readonly Regex Field=new Regex(@"^([\w /-]+):\s*(.*)$");
    internal static bool IsDeferred(string text){return Regex.IsMatch(text??"",@"\b(on hold|deferred|do not (?:start|resume)|not (?:to be )?start(?:ed)? now)\b",RegexOptions.IgnoreCase);}
    static bool Heading(string raw){string line=raw.Trim();return Regex.IsMatch(line,@"^#{1,3}\s+\S")||(raw.Length>0&&!Char.IsWhiteSpace(raw[0])&&line.Any(Char.IsLetter)&&line==line.ToUpperInvariant());}
    internal static ReadableTaskDocument Parse(string text){
        var document=new ReadableTaskDocument{Original=text??""};var lines=document.Original.Replace("\r\n","\n").Replace('\r','\n').Split('\n');bool deferred=false,sectionDeferred=false;
        var starts=new List<int>{0};foreach(Match newline in Regex.Matches(document.Original,@"\r\n|\r|\n"))starts.Add(newline.Index+newline.Length);
        for(int start=0;start<lines.Length;){
            string line=lines[start].Trim();var title=Numbered.Match(line);bool heading=!title.Success&&Heading(lines[start]);int end=start+1;
            if(!heading)while(end<lines.Length&&!Numbered.IsMatch(lines[end].Trim())&&!Heading(lines[end]))end++;
            var block=new ReadableTaskBlock{Kind=title.Success?"task":heading?"heading":"text",Raw=String.Join("\n",lines.Skip(start).Take(end-start)),Deferred=deferred};
            block.Start=starts[start];block.Length=(end<starts.Count?starts[end]:document.Original.Length)-block.Start;
            if(heading){block.Title=Regex.Replace(line,@"^#{1,3}\s+","");bool held=IsDeferred(block.Title)||Regex.IsMatch(block.Title,@"^Later(?:\s|$)",RegexOptions.IgnoreCase);if(line.StartsWith("### "))deferred=sectionDeferred||held;else sectionDeferred=deferred=held;block.Deferred=deferred;}
            else if(title.Success){block.Number=title.Groups[1].Value;block.Title=title.Groups[2].Value;ReadBody(block,lines.Skip(start+1).Take(end-start-1).ToArray());}
            document.Blocks.Add(block);start=end;
        }
        return document;
    }
    static void ReadBody(ReadableTaskBlock block,string[] lines){
        var paragraphs=new List<string>();var current=new List<string>();
        foreach(string raw in lines){
            string line=raw.Trim();
            if(line.Length==0){if(current.Count>0){paragraphs.Add(String.Join("\n",current));current.Clear();}continue;}
            if(Field.IsMatch(line)&&current.Count>0){paragraphs.Add(String.Join("\n",current));current.Clear();}
            current.Add(line);
        }
        if(current.Count>0)paragraphs.Add(String.Join("\n",current));
        var visible=paragraphs.TakeWhile(p=>!Regex.IsMatch(p,@"^(Notes|Details):",RegexOptions.IgnoreCase)).ToList();
        block.Ambiguous=visible.Count(p=>Regex.IsMatch(p,@"^Status:",RegexOptions.IgnoreCase))>1||visible.Count(p=>Regex.IsMatch(p,@"^Next:",RegexOptions.IgnoreCase))>1;
        var detail=new List<string>();bool notes=false;
        foreach(string paragraph in paragraphs){
            var field=Field.Match(paragraph.Split('\n')[0]);string name=field.Success?field.Groups[1].Value.ToLowerInvariant():"";
            string value=field.Success?paragraph.Substring(paragraph.IndexOf(':')+1).Trim():paragraph;
            if(name=="notes"||name=="details"){notes=true;detail.Add(paragraph);continue;}
            if(!notes&&!block.Ambiguous&&(name=="status"||name=="next")){
                if(name=="status"){block.Status=value;block.Deferred|=IsDeferred(value);}else block.Next=value;continue;
            }
            if(!notes&&block.Summary==null&&(name==""||name=="task"||name=="description")){
                block.Summary=value;continue;
            }
            detail.Add(paragraph);
        }
        if(block.Ambiguous){
            // Several jobs in one legacy item: retain their actual labels/context.
            // Do not attach a nested job's last Status/Next to the parent project.
            block.Status=null;block.Next=null;block.Details=String.Join("\n",lines).Trim();
        }else block.Details=String.Join("\n\n",detail);
    }
}
