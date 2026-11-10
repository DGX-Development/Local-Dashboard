using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

internal static class TaskbarShortcutIcons {
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct ShellFileInfo {
        public IntPtr icon;public int index;public uint attributes;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string name;
        [MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string type;
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attributes,out ShellFileInfo info,uint size,uint flags);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHUpdateImage(string path,int resource,uint flags,int image);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHChangeNotify(uint change,uint flags,string item,IntPtr extra);
    static object Get(object value,string name){return value.GetType().InvokeMember(name,BindingFlags.GetProperty,null,value,null);}
    static object Call(object value,string name,params object[] args){return value.GetType().InvokeMember(name,BindingFlags.InvokeMethod,null,value,args);}
    static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
    static bool SamePath(string a,string b){try{return String.Equals(Path.GetFullPath(a),Path.GetFullPath(b),StringComparison.OrdinalIgnoreCase);}catch{return false;}}

    internal static bool Matches(string target,string arguments,string identity,string expectedTarget,string expectedArguments,string expectedIdentity){
        return SamePath(target,expectedTarget)&&String.Equals((arguments??"").Trim(),(expectedArguments??"").Trim(),StringComparison.OrdinalIgnoreCase)
            &&(String.IsNullOrEmpty(identity)||String.Equals(identity,expectedIdentity,StringComparison.Ordinal));
    }
    internal static bool ManagedIcon(string value,string executable,string icon){
        if(String.IsNullOrWhiteSpace(value))return true;
        int comma=value.LastIndexOf(',');string path=Environment.ExpandEnvironmentVariables((comma<0?value:value.Substring(0,comma)).Trim().Trim('"'));
        if(String.IsNullOrWhiteSpace(path))return true;
        if(!Path.IsPathRooted(path))return false;
        return SamePath(path,executable)||SamePath(path,Path.Combine(Path.GetDirectoryName(executable),"local-dashboard.ico"))
            ||(SamePath(Path.GetDirectoryName(path),Path.GetDirectoryName(icon))&&Path.GetFileName(path).StartsWith("dashboard-",StringComparison.OrdinalIgnoreCase));
    }
    static IEnumerable<string> Links(string root){
        if(!String.IsNullOrEmpty(root)&&File.Exists(root)&&String.Equals(Path.GetExtension(root),".lnk",StringComparison.OrdinalIgnoreCase)){yield return root;yield break;}
        if(String.IsNullOrEmpty(root)||!Directory.Exists(root))yield break;
        var pending=new Stack<string>();pending.Push(root);int visited=0,foldersVisited=0;
        while(pending.Count>0&&++foldersVisited<=4096){
            string folder=pending.Pop();string[] files,folders;
            try{files=Directory.GetFiles(folder,"*.lnk");folders=Directory.GetDirectories(folder);}catch(UnauthorizedAccessException){continue;}catch(IOException){continue;}
            foreach(string file in files){if(++visited>4096)yield break;yield return file;}
            foreach(string child in folders){bool regular=false;try{regular=(File.GetAttributes(child)&FileAttributes.ReparsePoint)==0;}catch(IOException){}catch(UnauthorizedAccessException){}if(regular)pending.Push(child);}
        }
    }
    internal static void RefreshImage(string file,string expectedIcon){
        ShellFileInfo location,index;uint size=(uint)Marshal.SizeOf(typeof(ShellFileInfo));
        if(file.Length<260&&SHGetFileInfo(file,0,out location,size,0x1000)!=IntPtr.Zero
            &&SamePath(location.name,expectedIcon)&&SHGetFileInfo(file,0,out index,size,0x4000)!=IntPtr.Zero)
            SHUpdateImage(location.name,location.index,0,index.index);
        SHChangeNotify(0x2000,0x1005,file,IntPtr.Zero);
    }
    internal static void Refresh(string executable,string arguments,string appId,string icon){
        string pinned=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Microsoft","Internet Explorer","Quick Launch","User Pinned");
        var roots=new[]{Path.Combine(pinned,"TaskBar"),Path.Combine(pinned,"ImplicitAppShortcuts"),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(icon)),"relaunch.lnk")};
        Refresh(executable,arguments,appId,icon,roots);
    }
    internal static void Refresh(string executable,string arguments,string appId,string icon,IEnumerable<string> roots){
        RefreshImage(icon,icon);
        object shell=null,explorer=null;
        try{
            shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));explorer=Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application",true));
            var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Exception failure=null;
            foreach(string root in roots)foreach(string file in Links(root)){
                if(!seen.Add(file))continue;
                object link=null,folder=null,item=null;
                try{
                    if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)continue;
                    byte[] original=DashboardData.ReadBounded(file,1048576);
                    link=Call(shell,"CreateShortcut",file);
                    string target=Convert.ToString(Get(link,"TargetPath")),args=Convert.ToString(Get(link,"Arguments"));
                    if(!SamePath(target,executable)||!String.Equals(args.Trim(),arguments.Trim(),StringComparison.OrdinalIgnoreCase))continue;
                    folder=Call(explorer,"NameSpace",Path.GetDirectoryName(file));if(folder==null)continue;
                    item=Call(folder,"ParseName",Path.GetFileName(file));if(item==null)continue;
                    string identity=Convert.ToString(Call(item,"ExtendedProperty","System.AppUserModel.ID"));
                    if(!Matches(target,args,identity,executable,arguments,appId))continue;
                    string oldIcon=Convert.ToString(Get(link,"IconLocation"));if(!ManagedIcon(oldIcon,executable,icon))continue;
                    if(!String.Equals(oldIcon,icon+",0",StringComparison.OrdinalIgnoreCase)){
                        if(DashboardData.Hash(DashboardData.ReadBounded(file,1048576))!=DashboardData.Hash(original))throw new IOException("Shortcut changed during icon refresh.");
                        try{
                            link.GetType().InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{icon+",0"});Call(link,"Save");
                            Release(link);link=null;link=Call(shell,"CreateShortcut",file);Release(item);item=null;item=Call(folder,"ParseName",Path.GetFileName(file));
                            if(Convert.ToString(Get(link,"TargetPath"))!=target||Convert.ToString(Get(link,"Arguments"))!=args||Convert.ToString(Call(item,"ExtendedProperty","System.AppUserModel.ID"))!=identity||!String.Equals(Convert.ToString(Get(link,"IconLocation")),icon+",0",StringComparison.OrdinalIgnoreCase))throw new IOException("Taskbar shortcut changed unexpectedly.");
                        }catch{DashboardData.Atomic(file,original);throw;}
                    }
                    RefreshImage(file,icon);
                }catch(Exception error){failure=error;}finally{Release(item);Release(folder);Release(link);}
            }
            if(failure!=null)throw new IOException("A matching Dashboard shortcut could not be refreshed.",failure);
        }finally{Release(explorer);Release(shell);}
    }
}
