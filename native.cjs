'use strict';
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const {spawn,execFile}=require('node:child_process');
const {promisify}=require('node:util');
const run=promisify(execFile);
const root=__dirname;
function readCatalog(){return JSON.parse(fs.readFileSync(path.join(root,'catalog.json'),'utf8').replace(/^\uFEFF/,''));}
function resolveRequest(request,catalog=readCatalog()){
  if(!request||/^localdashboard:\/\/open\/?$/i.test(request))return {home:true,path:path.join(root,'index.html'),browser:true};
  const uri=new URL(request);
  if(uri.protocol!=='localdashboard:'||uri.hostname!=='item'||uri.username||uri.password||uri.port||uri.search||uri.hash)throw Error('Unsupported dashboard link.');
  const parts=uri.pathname.split('/').filter(Boolean);
  if(parts.length!==2)throw Error('Unsupported dashboard link.');
  const [id,targetId]=parts.map(part=>decodeURIComponent(part));
  const target=catalog.find(entry=>entry.id===id)?.targets.find(item=>item.id===targetId);
  if(!target||!['folder','document'].includes(target.kind)||!target.path)throw Error('This destination is not in the dashboard catalog.');
  if(!fs.existsSync(target.path))throw Error('The saved destination is missing: '+target.path);
  const stat=fs.statSync(target.path);
  if(target.kind==='folder'&&!stat.isDirectory())throw Error('The saved folder is no longer a folder.');
  if(target.kind==='document'&&(!stat.isFile()||!/\.(md|txt|pdf|docx|xlsx|pptx|html)$/i.test(target.path)))throw Error('Unsupported document type.');
  return {path:target.path,folder:stat.isDirectory(),browser:target.openIn==='browser'||/\.html$/i.test(target.path)};
}
function startApp(executable,args){return new Promise((resolve,reject)=>{
  const child=spawn(executable,args,{detached:true,windowsHide:false,stdio:'ignore'});
  child.once('error',reject);child.once('spawn',()=>{child.unref();resolve();});
});}
async function openDestination(destination){
  if(destination.folder)return startApp(path.join(process.env.SystemRoot||'C:\\Windows','explorer.exe'),[destination.path]);
  if(destination.browser){
    const candidates=[path.join(process.env.ProgramFiles||'C:\\Program Files','Mozilla Firefox','firefox.exe'),path.join(process.env['ProgramFiles(x86)']||'C:\\Program Files (x86)','Mozilla Firefox','firefox.exe'),path.join(process.env.LOCALAPPDATA||'','Mozilla Firefox','firefox.exe')];
    const firefox=candidates.find(file=>fs.existsSync(file));
    if(!firefox)throw Error('Firefox could not be found. Open index.html from a Firefox bookmark.');
    return startApp(firefox,['-new-tab',pathToFileURL(destination.path).href]);
  }
  return run('powershell.exe',['-NoProfile','-NonInteractive','-Command','Start-Process -FilePath $env:LOCAL_DASHBOARD_TARGET -ErrorAction Stop'],{windowsHide:true,env:{...process.env,LOCAL_DASHBOARD_TARGET:destination.path}});
}
function recordStatus(status){try{fs.mkdirSync(path.join(root,'.local'),{recursive:true});fs.writeFileSync(path.join(root,'.local','native-status.json'),JSON.stringify({at:new Date().toISOString(),...status}));}catch{}}
async function main(args=process.argv.slice(2),opener=openDestination){
  if(args.length>1)throw Error('Unsupported dashboard link.');
  const destination=resolveRequest(args[0]);
  if(destination.home)require('./build-local.cjs').build();
  if(opener===openDestination)recordStatus({state:'opening',kind:destination.home?'home':destination.folder?'folder':destination.browser?'browser-document':'document'});
  await opener(destination);
  if(opener===openDestination)recordStatus({state:'launched',kind:destination.home?'home':destination.folder?'folder':destination.browser?'browser-document':'document'});
  return destination;
}
if(require.main===module)main().catch(error=>{fs.writeFileSync(path.join(root,'launch-error.txt'),error.message+'\n');process.exitCode=1;});
module.exports={resolveRequest,main};