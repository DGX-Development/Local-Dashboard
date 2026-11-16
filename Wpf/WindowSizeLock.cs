using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

internal sealed partial class WpfDashboard {
    readonly Dictionary<Window,ResizeMode> sizeLockedDialogs=new Dictionary<Window,ResizeMode>();
    void ApplyWindowSizeLock(){
        bool locked=preferenceStore.Value.lockWindowSizes==true;
        if(locked&&window.WindowState==WindowState.Maximized)window.WindowState=WindowState.Normal;
        window.ResizeMode=locked?ResizeMode.CanMinimize:ResizeMode.CanResize;
        foreach(var pair in sizeLockedDialogs)pair.Key.ResizeMode=locked?ResizeMode.NoResize:pair.Value;
    }
    void RegisterDialogSize(Window dialog){
        if(dialog.SizeToContent!=SizeToContent.Manual||dialog.ResizeMode==ResizeMode.NoResize||dialog.Title=="Local Dashboard settings"||dialog.Title.IndexOf("setup",StringComparison.OrdinalIgnoreCase)>=0)return;
        sizeLockedDialogs[dialog]=dialog.ResizeMode;
        double[] size;var sizes=preferenceStore.Value.dialogSizes;
        if(sizes!=null&&sizes.TryGetValue(dialog.Title,out size)&&size!=null&&size.Length==2&&size.All(double.IsFinite)){
            dialog.Width=Math.Clamp(size[0],dialog.MinWidth,Math.Max(dialog.MinWidth,SystemParameters.WorkArea.Width));
            dialog.Height=Math.Clamp(size[1],dialog.MinHeight,Math.Max(dialog.MinHeight,SystemParameters.WorkArea.Height));
        }
        if(preferenceStore.Value.lockWindowSizes==true)dialog.ResizeMode=ResizeMode.NoResize;
    }
    void RememberDialogSize(Window dialog){
        dialog.Loaded+=(s,e)=>{RegisterDialogSize(dialog);CenterDialog(dialog);};
        dialog.Closed+=(s,e)=>{
            if(!sizeLockedDialogs.Remove(dialog)||preferenceStore.Value.lockWindowSizes==true)return;
            var bounds=dialog.WindowState==WindowState.Normal?new[]{dialog.Width,dialog.Height}:new[]{dialog.RestoreBounds.Width,dialog.RestoreBounds.Height};
            if(!bounds.All(double.IsFinite))return;
            Protect(()=>{var preferences=preferenceStore.Copy();preferences.dialogSizes=preferences.dialogSizes??new Dictionary<string,double[]>();preferences.dialogSizes[dialog.Title]=bounds;preferenceStore.Save(preferences,false);});
        };
    }
}
