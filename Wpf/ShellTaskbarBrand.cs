using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Reflection;

// Stable profile identity and icon resource, with a targeted refresh of matching shortcut images.
internal sealed class ShellTaskbarBrand {
    [StructLayout(LayoutKind.Sequential)] struct PropertyKey { public Guid format;public uint id;internal PropertyKey(uint id){format=new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3");this.id=id;} }
    [StructLayout(LayoutKind.Explicit,Size=24)] struct PropVariant { [FieldOffset(0)] public ushort type;[FieldOffset(8)] public IntPtr pointer;
        internal static PropVariant Text(string value){return new PropVariant{type=31,pointer=Marshal.StringToCoTaskMemUni(value)};}
        internal string StringValue {get{return type==31?Marshal.PtrToStringUni(pointer):null;}}
    }
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IPropertyStore {
        [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out PropertyKey key);
        [PreserveSig]int GetValue(ref PropertyKey key,out PropVariant value);
        [PreserveSig]int SetValue(ref PropertyKey key,ref PropVariant value);[PreserveSig]int Commit();
    }
    [DllImport("shell32.dll",PreserveSig=true)] static extern int SHGetPropertyStoreForWindow(IntPtr window,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IPropertyStore store);
    [DllImport("ole32.dll")] static extern int PropVariantClear(ref PropVariant value);
    readonly string profile,command,displayName,executable,arguments;
    string relaunchCommand;
    internal readonly string AppId;
    internal string IconResource {get;private set;}
    internal string RefreshKey {get;private set;}
    string nativeIconPath;
    [DllImport("shell32.dll",CharSet=CharSet.Unicode,PreserveSig=true)] static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
    [DllImport("shell32.dll",PreserveSig=true)] static extern int GetCurrentProcessExplicitAppUserModelID(out IntPtr appId);
    static string Identity(string profile,bool review){using(var sha=SHA256.Create())return "DGX.LocalDashboard."+(review?"Review.":"App.")+BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Path.GetFullPath(profile).ToUpperInvariant()))).Replace("-","").Substring(0,24);}
    internal static void InitializeProcess(string profile,bool review){Marshal.ThrowExceptionForHR(SetCurrentProcessExplicitAppUserModelID(Identity(profile,review)));}
    internal static string ProcessIdentity(){IntPtr text;Marshal.ThrowExceptionForHR(GetCurrentProcessExplicitAppUserModelID(out text));try{return Marshal.PtrToStringUni(text);}finally{Marshal.FreeCoTaskMem(text);}}
    internal ShellTaskbarBrand(string profile,bool review,string executable){
        this.profile=profile;displayName="Local Dashboard";
        this.executable=executable;arguments=review?"--fixture \""+profile+"\"":"";
        AppId=Identity(profile,review);
        command="\""+executable+"\""+(arguments.Length==0?"":" "+arguments);
    }
    // Shell relaunch strings have a tighter practical limit than process command lines.
    // A profile-local shortcut retains full paths and arguments without shortening user paths.
    internal string RelaunchCommand {
        get{
            if(relaunchCommand!=null)return relaunchCommand;
            if(command.Length<240)return relaunchCommand=command;
            string path=Path.Combine(profile,".local","relaunch.lnk");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            object shell=null,link=null;
            try{
                shell=Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell",true));
                link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
                var type=link.GetType();
                type.InvokeMember("TargetPath",BindingFlags.SetProperty,null,link,new object[]{executable});
                type.InvokeMember("Arguments",BindingFlags.SetProperty,null,link,new object[]{arguments});
                type.InvokeMember("WorkingDirectory",BindingFlags.SetProperty,null,link,new object[]{Path.GetDirectoryName(executable)});
                type.InvokeMember("Description",BindingFlags.SetProperty,null,link,new object[]{"Local Dashboard profile"});
                if(IconResource!=null)type.InvokeMember("IconLocation",BindingFlags.SetProperty,null,link,new object[]{IconResource});
                type.InvokeMember("Save",BindingFlags.InvokeMethod,null,link,null);
                return relaunchCommand="\""+path+"\"";
            }finally{if(link!=null)Marshal.ReleaseComObject(link);if(shell!=null)Marshal.ReleaseComObject(shell);}
        }
    }
    internal void Prepare(System.Drawing.Image image,System.Drawing.Color color){
        string directory=Path.Combine(profile,".local","appearance");Directory.CreateDirectory(directory);
        string path=Path.Combine(directory,"dashboard-v3-"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2")+".ico");
        byte[] bytes=DashboardBrand.IconBytes(color);
        if(!File.Exists(path)||DashboardData.Hash(File.ReadAllBytes(path))!=DashboardData.Hash(bytes))DashboardData.Atomic(path,bytes);
        nativeIconPath=path;RefreshKey=path;
        string taskbarPath=Path.Combine(directory,"dashboard-taskbar.ico");
        if(!File.Exists(taskbarPath)||DashboardData.Hash(File.ReadAllBytes(taskbarPath))!=DashboardData.Hash(bytes))DashboardData.Atomic(taskbarPath,bytes);
        IconResource=taskbarPath+",0";
    }
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,uint message,IntPtr wParam,IntPtr lParam);
    [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr hwnd);
    System.Drawing.Icon nativeSmall,nativeLarge;string nativeKey;
    void ApplyNative(IntPtr handle){
        uint dpi=96;try{dpi=GetDpiForWindow(handle);if(dpi==0)dpi=96;}catch(EntryPointNotFoundException){}
        int small=(int)Math.Round(16*dpi/96.0),large=(int)Math.Round(32*dpi/96.0);string key=RefreshKey+"@"+dpi;
        if(nativeKey!=key){
            string path=nativeIconPath;
            var nextSmall=new System.Drawing.Icon(path,small,small);var nextLarge=new System.Drawing.Icon(path,large,large);
            SendMessage(handle,0x0080,IntPtr.Zero,nextSmall.Handle);SendMessage(handle,0x0080,new IntPtr(1),nextLarge.Handle);
            if(nativeSmall!=null)nativeSmall.Dispose();if(nativeLarge!=null)nativeLarge.Dispose();
            nativeSmall=nextSmall;nativeLarge=nextLarge;nativeKey=key;
        }else{SendMessage(handle,0x0080,IntPtr.Zero,nativeSmall.Handle);SendMessage(handle,0x0080,new IntPtr(1),nativeLarge.Handle);}
    }
    static IPropertyStore Open(IntPtr handle){var iid=typeof(IPropertyStore).GUID;IPropertyStore store;Marshal.ThrowExceptionForHR(SHGetPropertyStoreForWindow(handle,ref iid,out store));return store;}
    static void Set(IPropertyStore store,uint id,string value){var key=new PropertyKey(id);var variant=value==null?new PropVariant():PropVariant.Text(value);try{int result=store.SetValue(ref key,ref variant);if(result<0)throw new InvalidOperationException("Taskbar property "+id+" could not be set (0x"+result.ToString("X8")+").",Marshal.GetExceptionForHR(result));}finally{PropVariantClear(ref variant);}}
    internal void Apply(IntPtr handle){
        if(handle==IntPtr.Zero||IconResource==null)return;
        ApplyNative(handle);var store=Open(handle);try{
            // Set the relaunch properties before ID; setting ID notifies the taskbar to refresh.
            Set(store,2,RelaunchCommand);Set(store,4,displayName);Set(store,3,IconResource);Set(store,5,AppId);
        }finally{Marshal.ReleaseComObject(store);}
    }
    internal void RefreshTaskbar(IntPtr handle){
        if(handle==IntPtr.Zero||IconResource==null)return;
        Apply(handle);RefreshShortcuts();
    }
    internal void RefreshShortcuts(){if(IconResource!=null)TaskbarShortcutIcons.Refresh(executable,arguments,AppId,IconResource.Substring(0,IconResource.Length-2));}
    internal string Read(IntPtr handle,uint id){var store=Open(handle);try{var key=new PropertyKey(id);PropVariant value;Marshal.ThrowExceptionForHR(store.GetValue(ref key,out value));try{return value.StringValue;}finally{PropVariantClear(ref value);}}finally{Marshal.ReleaseComObject(store);}}
    internal void Dispose(){if(nativeSmall!=null)nativeSmall.Dispose();if(nativeLarge!=null)nativeLarge.Dispose();nativeSmall=nativeLarge=null;nativeKey=null;}
    internal void Clear(IntPtr handle){if(handle==IntPtr.Zero)return;var store=Open(handle);try{foreach(uint id in new uint[]{5,3,4,2})Set(store,id,null);}finally{Marshal.ReleaseComObject(store);}}
}
