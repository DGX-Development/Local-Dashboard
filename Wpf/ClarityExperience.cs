using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

internal sealed class CompactPlacement { public double left{get;set;} public double top{get;set;} public double width{get;set;} public double height{get;set;} }
internal sealed class FullPlacement { public double left{get;set;} public double top{get;set;} public double width{get;set;} public double height{get;set;} public bool maximized{get;set;} }
internal sealed partial class WpfDashboard {
    Rect? compactBounds;
    bool loadedFullPlacement;
    void LoadFullBounds(){
        try{
            string path=StatePath("full-window.json");if(!File.Exists(path))return;
            var value=AppData.Json.Deserialize<FullPlacement>(Encoding.UTF8.GetString(DashboardData.ReadBounded(path,4096)));
            if(value==null||new[]{value.left,value.top,value.width,value.height}.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v))||value.width<100||value.height<100)return;
            fullBounds=new Rect(value.left,value.top,Math.Max(760,Math.Min(6000,value.width)),Math.Max(540,Math.Min(4000,value.height)));
            fullState=value.maximized?WindowState.Maximized:WindowState.Normal;loadedFullPlacement=true;
            window.WindowStartupLocation=WindowStartupLocation.Manual;window.Left=fullBounds.Left;window.Top=fullBounds.Top;window.Width=fullBounds.Width;window.Height=fullBounds.Height;
        }catch(IOException){}catch(ArgumentException){}
    }
    void SaveFullBounds(){
        if(!Compact){
            var bounds=window.WindowState==WindowState.Normal?new Rect(window.Left,window.Top,window.Width,window.Height):window.RestoreBounds;
            if(bounds.IsEmpty||new[]{bounds.Left,bounds.Top,bounds.Width,bounds.Height}.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v)))return;
            fullBounds=bounds;if(window.WindowState!=WindowState.Minimized)fullState=window.WindowState;
        }
        if(nonWindowedVerification||fullBounds.Width<100||fullBounds.Height<100)return;
        DashboardData.Atomic(StatePath("full-window.json"),Encoding.UTF8.GetBytes(AppData.Json.Serialize(new FullPlacement{left=fullBounds.Left,top=fullBounds.Top,width=fullBounds.Width,height=fullBounds.Height,maximized=fullState==WindowState.Maximized})));
    }
    void SaveWindowBounds(){SaveCompactBounds();SaveFullBounds();}
    void ResetWindowSizes(){
        var screen=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(window).Handle).WorkingArea;
        var source=PresentationSource.FromVisual(window);var transform=source==null?Matrix.Identity:source.CompositionTarget.TransformFromDevice;
        var start=transform.Transform(new Point(screen.Left,screen.Top));var end=transform.Transform(new Point(screen.Right,screen.Bottom));
        double width=Math.Max(760,Math.Min(1280,end.X-start.X)),height=Math.Max(540,Math.Min(860,end.Y-start.Y));
        fullBounds=new Rect(start.X+(end.X-start.X-width)/2,start.Y+(end.Y-start.Y-height)/2,width,height);fullState=WindowState.Normal;
        compactSize=new Size(Math.Max(CompactMinimumWidth(),Math.Min(DefaultCompactWidth,end.X-start.X)),Math.Max(420,Math.Min(760,end.Y-start.Y)));
        compactBounds=new Rect(start.X+(end.X-start.X-compactSize.Width)/2,start.Y+(end.Y-start.Y-compactSize.Height)/2,compactSize.Width,compactSize.Height);
        var current=Compact?compactBounds.Value:fullBounds;window.WindowState=WindowState.Normal;window.Width=current.Width;window.Height=current.Height;window.Left=current.Left;window.Top=current.Top;
        if(!nonWindowedVerification){
            var compact=compactBounds.Value;
            DashboardData.Atomic(StatePath("compact-window.json"),Encoding.UTF8.GetBytes(AppData.Json.Serialize(new CompactPlacement{left=compact.Left,top=compact.Top,width=compact.Width,height=compact.Height})));
            SaveFullBounds();
        }
    }
    double CompactMinimumWidth(){
        UpdateHeaderActions();var toolbar=Find<WrapPanel>("CompactToolbar");double width=0;
        foreach(Button button in toolbar.Children){if(button.Visibility!=Visibility.Visible)continue;button.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));width+=button.DesiredSize.Width;}
        return Math.Max(520,Math.Ceiling(width+92));
    }
    double CompactOpeningWidth(){
        var tabs=Find<WrapPanel>("WorkNavigation");
        double tabWidth=0;
        foreach(Button tab in tabs.Children.OfType<Button>()){
            if(tab.Visibility!=Visibility.Visible)continue;
            tab.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
            tabWidth+=tab.DesiredSize.Width;
        }
        var edit=Find<StackPanel>("CompactEditActions");
        edit.Measure(new Size(Double.PositiveInfinity,Double.PositiveInfinity));
        var inset=Find<DockPanel>("MainContent").Margin;
        // Match the reserved lane and row inset used by AlignHeaderGutter, plus native frame allowance.
        return Math.Max(DefaultCompactWidth,Math.Ceiling(tabWidth+edit.DesiredSize.Width+inset.Left+inset.Right+rows.Margin.Right+17+16));
    }
    void LoadCompactBounds(){
        try{string path=StatePath("compact-window.json");if(!File.Exists(path))return;var value=AppData.Json.Deserialize<CompactPlacement>(Encoding.UTF8.GetString(DashboardData.ReadBounded(path,4096)));
            if(value==null||new[]{value.left,value.top,value.width,value.height}.Any(v=>Double.IsNaN(v)||Double.IsInfinity(v))||value.width<100||value.height<100)return;
            compactSize=new Size(Math.Max(520,Math.Min(3000,value.width)),Math.Max(420,Math.Min(2000,value.height)));compactBounds=new Rect(value.left,value.top,compactSize.Width,compactSize.Height);
        }catch(IOException){}catch(ArgumentException){}
    }
    void SaveCompactBounds(){
        if(!Compact||window.WindowState!=WindowState.Normal)return;
        compactBounds=new Rect(window.Left,window.Top,window.Width,window.Height);compactSize=compactBounds.Value.Size;
        if(nonWindowedVerification)return;
        DashboardData.Atomic(StatePath("compact-window.json"),Encoding.UTF8.GetBytes(AppData.Json.Serialize(new CompactPlacement{left=window.Left,top=window.Top,width=window.Width,height=window.Height})));
    }
    void VerifyClarityBehavior(){
        var blue=System.Drawing.Color.FromArgb(36,95,173);var mint=System.Drawing.Color.FromArgb(154,217,193);
        if(DashboardBrand.InnerColor(blue).R!=242||DashboardBrand.InnerColor(mint).R!=22)throw new Exception("Blue/mint contrast polarity is wrong.");
        foreach(var color in new[]{blue,mint,System.Drawing.Color.Black,System.Drawing.Color.White})using(var bitmap=DashboardBrand.Render(color,128)){
            var actual=bitmap.GetPixel(28,60);var expected=DashboardBrand.InnerColor(color);if(Math.Abs(actual.R-expected.R)>2||Math.Abs(actual.G-expected.G)>2||Math.Abs(actual.B-expected.B)>2)throw new Exception("Rendered brand did not use the contrast choice: "+actual+" / "+expected);
        }
        if(!(BrandVector(blue) is DrawingImage))throw new Exception("Appearance brand preview is not vector.");
        string scope=Path.Combine(profile,"checklist-location-test");Directory.CreateDirectory(scope);string local=Path.Combine(scope,".local");Directory.CreateDirectory(local);if(File.Exists(ChecklistLocation.Config(scope)))File.Delete(ChecklistLocation.Config(scope));File.Copy(Path.Combine(profile,"catalog.json"),Path.Combine(scope,"catalog.json"),true);
        string original=ChecklistLocation.PathFor(scope),destination=Path.Combine(profile,"separate-checklist.json");
        var store=new ChecklistStore(original,entries);var data=store.Read();if(data.items.Count==0)store.Upsert(new WorkPatch{expectedRevision=data.revision,id="preserved-task",title="Preserve my task"});
        string initial=ChecklistLocation.Token(original);if(File.Exists(destination))File.Delete(destination);
        ChecklistLocation.Choose(scope,destination,ChecklistLocation.Token(ChecklistLocation.Config(scope)),initial,null,entries);
        if(ChecklistLocation.Token(original)!=initial||ChecklistLocation.Token(destination)!=initial||ChecklistLocation.PathFor(scope)!=destination)throw new Exception("Checklist relocation lost or changed the original.");
        var relocated=new ChecklistStore(ChecklistLocation.PathFor(scope),entries);var current=relocated.Read();relocated.Complete(current.revision,"preserved-task",true);
        if(ChecklistLocation.Token(original)!=initial||!relocated.Read().items.Any(i=>i.state=="done"))throw new Exception("Checklist edits did not use the selected file.");
        if(DashboardData.FileFor(scope,".local/current-work.json")!=destination)throw new Exception("Backup cannot find the selected checklist.");
        string backup=Path.Combine(profile,"relocated-checklist-backup.json");DashboardData.Export(scope,backup);current=relocated.Read();relocated.Upsert(new WorkPatch{expectedRevision=current.revision,id="preserved-task",title="Changed after backup"});DashboardData.Restore(scope,DashboardData.Load(backup));if(relocated.Read().items.Single(i=>i.id=="preserved-task").title!="Preserve my task"||ChecklistLocation.Token(original)!=initial)throw new Exception("External checklist backup/restore failed or changed the retained original.");
        string existing=Path.Combine(profile,"existing-checklist.json");if(File.Exists(existing))File.Delete(existing);var other=new ChecklistStore(existing,entries);other.Upsert(new WorkPatch{expectedRevision=0,id="existing-task",title="Existing task"});string existingHash=ChecklistLocation.Token(existing);
        ChecklistLocation.Choose(scope,existing,ChecklistLocation.Token(ChecklistLocation.Config(scope)),ChecklistLocation.Token(destination),existingHash,entries);
        if(ChecklistLocation.Token(existing)!=existingHash||ChecklistLocation.Token(original)!=initial)throw new Exception("Selecting an existing checklist overwrote tasks.");
        bool blocked=false;try{ChecklistLocation.Choose(scope,destination,"stale",existingHash,ChecklistLocation.Token(destination),entries);}catch(IOException){blocked=true;}if(!blocked||ChecklistLocation.PathFor(scope)!=existing)throw new Exception("Stale checklist-location change was not blocked.");
        SetCompact(true);double minimum=window.MinWidth;CaptureShell(profile,"clarity-compact-minimum.png",(int)minimum,760);var toolbar=Find<WrapPanel>("CompactToolbar");var ys=toolbar.Children.OfType<Button>().Where(b=>b.Visibility==Visibility.Visible).Select(b=>b.TranslatePoint(new Point(),toolbar).Y).ToArray();if(ys.Length!=2||ys.Max()-ys.Min()>1)throw new Exception("Compact actions wrap at minimum width.");SetCompact(false);
        Console.WriteLine("Clarity behavior passed: rendered contrast choice, vector preview, lossless checklist copy, existing-checklist selection, automatic saves at selected location, backup routing, stale-choice rejection and nonwrapping compact toolbar.");
    }
}
