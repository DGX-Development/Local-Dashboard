from pathlib import Path
import json,re
import argparse
parser=argparse.ArgumentParser();parser.add_argument('--reminder',type=Path,required=True);parser.add_argument('--watchdog',type=Path,required=True);args=parser.parse_args();root=Path.cwd()
def decode_topics(source,pattern):
 return [(json.loads('"'+a+'"'),json.loads('"'+b+'"')) for a,b in re.findall(pattern,source)]
def guide(name,version,topics):
 return '# '+name+' user guide\n\nVersion '+version+'. Prepared locally; publication pending.\n\n'+''.join('## '+k+'\n\n'+v.replace('## ','### ')+'\n\n' for k,v in topics)
br=args.reminder;wd=args.watchdog
topics=list(json.loads((br/'src/BackupReminder.Desktop/ReviewHelp.json').read_text(encoding='utf-8-sig')).items());(br/'docs/user-guide.md').write_text(guide('Backup Reminder','1.0.2',topics),encoding='utf-8')
(br/'docs/Portable-Readme.txt').write_text('Backup Reminder 1.0.2\n\nExtract the entire portable ZIP and run Backup-Reminder.exe.\n\n'+''.join(k+'\n\n'+v+'\n\n' for k,v in topics),encoding='utf-8')
s=(wd/'src/ChatGPTWatchdog.Desktop/Pages.cs').read_text(encoding='utf-8-sig');block=s.split('static readonly Dictionary<string,string> Topics=new(){',1)[1].split('\n };',1)[0];topics=decode_topics(block,r'\["((?:[^"\\]|\\.)*)"\]="((?:[^"\\]|\\.)*)"')
p=wd/'docs/user-guide.md';old=p.read_text(encoding='utf-8-sig');historical=old[old.index('## Historical development material'):];p.write_text(guide('ChatGPT Watchdog','1.0.3',topics)+historical,encoding='utf-8')
(wd/'docs/Portable-Readme.txt').write_text('ChatGPT Watchdog 1.0.3\n\nExtract the entire portable ZIP and run ChatGPT-Watchdog.exe.\n\n'+''.join(k+'\n\n'+v+'\n\n' for k,v in topics),encoding='utf-8')
print('Synchronized all current Help topics into online guides and sibling offline instructions.')

topics=decode_topics((root/'DashboardInformation.cs').read_text(encoding='utf-8-sig'),r'new Topic\("((?:[^"\\]|\\.)*)","((?:[^"\\]|\\.)*)"\)')
assert len(topics)>10
(root/'docs/user-guide.md').write_text(guide('Local Dashboard','3.0.3',topics),encoding='utf-8')
