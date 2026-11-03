'use strict';
let entries=[];
let pinned=new Set();
try{const saved=JSON.parse(localStorage.getItem('local-dashboard-pins')||'[]');if(Array.isArray(saved))pinned=new Set(saved.filter(x=>typeof x==='string'));}catch{}
const $=id=>document.getElementById(id);
const groups=[{id:'software',name:'Software Projects',root:'software-folder'},{id:'website',name:'Website Projects',root:'websites-folder'},{id:'documents',name:'Development Documents'},{id:'everyday',name:'Websites I Use'},{id:'workflows',name:'Workflows'},{id:'secondary',name:'Other Workspaces'}];
function el(tag,cls,text){const node=document.createElement(tag);if(cls)node.className=cls;if(text)node.textContent=text;return node;}
function announce(text){$('feedback').textContent=text;}
function action(entry,target,root=false){
  const label=root?'Open folder':target.kind==='folder'?'Folder':target.kind==='web'?(entry.section==='website'?'Website':'Open'):/\\\.dev\\/i.test(target.path)?'Notes':'Open';
  const node=el('a','action'+(root?' root-action':''),target.available===false?'Missing':label);
  node.setAttribute('aria-label',(target.kind==='folder'?'Open folder':target.kind==='web'?'Open website':'Open document')+' for '+entry.name+(target.kind==='web'?' (new tab)':''));
  node.title=target.path||target.url;
  if(target.available===false){node.setAttribute('aria-disabled','true');return node;}
  if(target.kind==='web'){node.href=target.url;node.target='_blank';node.rel='noopener noreferrer';}
  else {node.href=target.localUrl;node.target='_blank';node.rel='noopener noreferrer';node.setAttribute('aria-label',(target.kind==='folder'?'Open folder':'Open document')+' for '+entry.name+' (new tab)');}
  return node;
}function fallbackIcon(entry){
  const symbols={software:'<rect x="4" y="4" width="6" height="6" rx="1"/><rect x="14" y="4" width="6" height="6" rx="1"/><rect x="4" y="14" width="6" height="6" rx="1"/><rect x="14" y="14" width="6" height="6" rx="1"/>',website:'<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c-5 5-5 13 0 18 5-5 5-13 0-18z"/>',everyday:'<circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3c-5 5-5 13 0 18 5-5 5-13 0-18z"/>',documents:'<path d="M6 3h8l4 4v14H6zM14 3v5h4M9 12h6M9 16h6"/>',workflows:'<rect x="8" y="2" width="8" height="5" rx="1"/><rect x="2" y="17" width="8" height="5" rx="1"/><rect x="14" y="17" width="8" height="5" rx="1"/><path d="M12 7v5M6 17v-5h12v5"/>',secondary:'<path d="M3 6h7l2 3h9v12H3z"/>'};
  return 'data:image/svg+xml,'+encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="none" stroke="#b5dccf" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round">'+(symbols[entry.section]||symbols.secondary)+'</svg>');
}
function row(entry){
  const item=el('li','row'),name=el('span','name'),actions=el('div','actions');
  if(entry.iconUrl){const icon=el('img','entry-icon');icon.alt='';icon.width=24;icon.height=24;icon.src=entry.iconUrl;icon.onerror=()=>{icon.remove();};name.append(icon);}else if(entry.section==='documents'||entry.section==='workflows'){const icon=el('img','entry-icon');icon.alt='';icon.width=24;icon.height=24;icon.src=fallbackIcon(entry);name.append(icon);}name.append(el('span','entry-title',entry.name));
  name.title=entry.targets.map(t=>t.path||t.url).join('\n');
  for(const target of entry.targets)actions.append(action(entry,target));
  const pin=el('button','pin',pinned.has(entry.id)?'★':'☆');pin.type='button';pin.setAttribute('aria-label','Pin '+entry.name);pin.setAttribute('aria-pressed',String(pinned.has(entry.id)));
  pin.onclick=()=>{pinned.has(entry.id)?pinned.delete(entry.id):pinned.add(entry.id);try{localStorage.setItem('local-dashboard-pins',JSON.stringify([...pinned]));}catch{announce('Pins cannot be saved in this browser.');}render();};
  actions.append(pin);item.append(name,actions);return item;
}
function render(){
  const query=$('search').value.trim().toLocaleLowerCase();
  $('directory').replaceChildren();
  const left=el('div','column'),right=el('div','column');$('directory').append(left,right);let visible=0;
  for(const group of groups){
    const matching=entries.filter(e=>e.section===group.id&&[e.name,e.description,e.section,e.category,...e.targets.map(t=>t.path||t.url)].join(' ').toLocaleLowerCase().includes(query));
    if(!matching.length)continue;
    matching.sort((a,b)=>Number(pinned.has(b.id))-Number(pinned.has(a.id))||a.name.localeCompare(b.name));
    const section=el(group.id==='secondary'?'details':'section',group.id==='secondary'?'secondary':'group');
    if(group.id==='secondary'){section.append(el('summary','',group.name));section.open=Boolean(query);}else{
      const heading=el('div','group-header');heading.append(el('h2','',group.name));
      if(group.root){const root=entries.find(e=>e.id===group.root);if(root){heading.append(action(root,root.targets[0],true));section.append(heading,el('p','root',root.targets[0].path));}else section.append(heading);}else section.append(heading);
    }
    if(group.note)section.append(el('p','group-note',group.note));
    const list=el('ul','rows');list.append(...matching.map(row));section.append(list);visible++;if(group.id==='secondary')$('directory').append(section);else if(group.id==='software'||group.id==='documents'||group.id==='workflows')left.append(section);else if(group.id==='everyday')right.prepend(section);else right.append(section);
  }
  if(!visible)$('directory').append(el('p','empty search-empty','No matching links. Try another search.'));
}
$('search').addEventListener('input',()=>{announce('');render();});
if(window.LOCAL_DASHBOARD?.entries){entries=window.LOCAL_DASHBOARD.entries;render();}
else announce('The local directory data is missing. Run Start Local Dashboard.vbs to rebuild it.');