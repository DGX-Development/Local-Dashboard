'use strict';
const fs=require('node:fs');
const path=require('node:path');
const {pathToFileURL}=require('node:url');
const root=__dirname;
function build(){
  const catalog=JSON.parse(fs.readFileSync(path.join(root,'catalog.json'),'utf8').replace(/^\uFEFF/,''));
  const entries=catalog.map(({iconPath,iconAsset,...entry})=>{
    let iconUrl=null;
    const source=iconPath||(iconAsset==='local-dashboard.svg'?path.join(root,'dist','icons',iconAsset):null);
    const types={'.png':'image/png','.ico':'image/x-icon','.jpg':'image/jpeg','.jpeg':'image/jpeg','.webp':'image/webp','.svg':'image/svg+xml'};
    if(source&&fs.existsSync(source)){
      const type=types[path.extname(source).toLowerCase()];
      if(!type||fs.statSync(source).size>4*1024*1024)throw Error('Unsupported icon: '+entry.name);
      iconUrl='data:'+type+';base64,'+fs.readFileSync(source).toString('base64');
    }
    return {...entry,iconUrl,targets:entry.targets.map(target=>({...target,localUrl:target.kind==='web'?null:pathToFileURL(target.kind==='folder'?path.resolve(target.path)+path.sep:target.path).href,available:target.kind==='web'||fs.existsSync(target.path)}))};
  });
  const data={entries};
  fs.writeFileSync(path.join(root,'dist','catalog-data.js'),'window.LOCAL_DASHBOARD = '+JSON.stringify(data).replace(/</g,'\\u003c')+';\n');
  const entryPath=path.join(root,'index.html');
  let html=fs.readFileSync(entryPath,'utf8');
  for(const file of ['catalog-data.js','main.js']){
    const hash=require('node:crypto').createHash('sha256').update(fs.readFileSync(path.join(root,'dist',file))).digest('hex').slice(0,12);
    const escaped=file.replace(/\./g,'\\.');
    html=html.replace(new RegExp('src="dist/'+escaped+'(?:\\?[^\"]*)?"','g'),'src="dist/'+file+'?v=sha256-'+hash+'"');
  }
  fs.writeFileSync(entryPath,html);
  return {entries:entries.length,url:pathToFileURL(entryPath).href};
}
if(require.main===module)console.log(JSON.stringify(build()));
module.exports={build};