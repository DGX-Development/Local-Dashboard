using System;
using System.Drawing;
using System.Windows.Forms;
internal static class ReorderDrag {
    static Action pending; static bool active; static Form preview; static Panel marker; static int lastScroll;
    sealed class Preview:Form {
        protected override bool ShowWithoutActivation{get{return true;}}
        protected override CreateParams CreateParams{get{var p=base.CreateParams;p.ExStyle|=0x08000000|0x20|0x80;return p;}}
        internal Preview(string text){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;TopMost=true;BackColor=DashboardForm.Accent;Opacity=.88;Size=new Size(270,38);var label=new Label{Dock=DockStyle.Fill,Text="  ⋮⋮  "+text,ForeColor=Color.FromArgb(20,35,30),TextAlign=ContentAlignment.MiddleLeft,Font=new Font("Segoe UI",10),AutoEllipsis=true,UseMnemonic=false};Controls.Add(label);}
    }
    internal static void Commit(Action action){if(active)pending=action;else action();}
    static void Clear(){if(marker!=null){marker.Visible=false;marker=null;}}
    static void Follow(){if(preview!=null)preview.Location=new Point(Cursor.Position.X+18,Cursor.Position.Y+18);}
    internal static void Source(Control grip,string format,Func<string> value,Func<bool> enabled,Func<string> label){
        Point start=Point.Empty;bool armed=false;
        grip.MouseDown+=(s,e)=>{armed=enabled()&&e.Button==MouseButtons.Left;start=grip.PointToScreen(e.Location);};
        grip.MouseUp+=(s,e)=>armed=false;
        grip.GiveFeedback+=(s,e)=>{e.UseDefaultCursors=true;Follow();};
        grip.MouseMove+=(s,e)=>{if(!armed||!enabled()||e.Button!=MouseButtons.Left||active)return;Point point=grip.PointToScreen(e.Location);Size size=SystemInformation.DragSize;if(new Rectangle(start.X-size.Width/2,start.Y-size.Height/2,size.Width,size.Height).Contains(point))return;
            armed=false;active=true;pending=null;var owner=grip.FindForm();
            try{preview=new Preview(label());Follow();preview.Show();grip.DoDragDrop(new DataObject(format,value()),DragDropEffects.Move);}
            finally{Clear();if(preview!=null){preview.Close();preview.Dispose();preview=null;}active=false;}
            var action=pending;pending=null;if(action!=null&&owner!=null&&!owner.IsDisposed)owner.BeginInvoke(action);
        };
    }
    internal static void Target(Control row,string format,Func<string,bool> valid,Action<string,bool> drop){
        var line=new Panel{BackColor=DashboardForm.Accent,Height=3,Visible=false,Enabled=false};row.Controls.Add(line);
        DragEventHandler over=(s,e)=>{string value=e.Data.GetDataPresent(format)?Convert.ToString(e.Data.GetData(format)):null;if(value==null||!valid(value)){e.Effect=DragDropEffects.None;Clear();return;}e.Effect=DragDropEffects.Move;Point p=row.PointToClient(new Point(e.X,e.Y));bool after=p.Y>=row.Height/2;if(marker!=line)Clear();marker=line;line.BackColor=DashboardForm.Accent;line.SetBounds(0,after?row.Height-3:0,row.Width,3);line.Visible=true;line.BringToFront();Follow();ScrollEdge(row,new Point(e.X,e.Y));};
        DragEventHandler finish=(s,e)=>{string value=e.Data.GetDataPresent(format)?Convert.ToString(e.Data.GetData(format)):null;bool after=row.PointToClient(new Point(e.X,e.Y)).Y>=row.Height/2;Clear();if(value!=null&&valid(value))Commit(()=>drop(value,after));};
        Bind(row,over,finish);row.DragLeave+=(s,e)=>Clear();
    }
    sealed class Probe:Panel {internal void Over(DragEventArgs e){OnDragOver(e);}internal void Drop(DragEventArgs e){OnDragDrop(e);}}
    internal static void SelfTest(){using(var row=new Probe{Size=new Size(240,44)}){row.CreateControl();int calls=0;bool lower=false;Target(row,"probe",value=>value=="item",(value,after)=>{calls++;lower=after;});Point p=row.PointToScreen(new Point(10,38));var e=new DragEventArgs(new DataObject("probe","item"),1,p.X,p.Y,DragDropEffects.Move,DragDropEffects.None);row.Over(e);if(e.Effect!=DragDropEffects.Move||marker==null||!marker.Visible)throw new Exception("Target feedback did not appear.");active=true;try{row.Drop(e);if(calls!=0||pending==null)throw new Exception("Drop committed during native drag.");}finally{active=false;}var action=pending;pending=null;action();if(calls!=1||!lower)throw new Exception("Lower half drop failed.");p=row.PointToScreen(new Point(10,2));e=new DragEventArgs(new DataObject("probe","item"),1,p.X,p.Y,DragDropEffects.Move,DragDropEffects.None);row.Drop(e);if(calls!=2||lower)throw new Exception("Upper half drop failed.");e=new DragEventArgs(new DataObject("probe","unknown"),1,p.X,p.Y,DragDropEffects.Move,DragDropEffects.Move);row.Over(e);row.Drop(e);if(e.Effect!=DragDropEffects.None||calls!=2)throw new Exception("Invalid source accepted.");}Console.WriteLine("Drag target event checks passed: insertion feedback, upper/before and lower/after placement, invalid-source rejection and deferred single commit. Physical pointer behavior remains unverified.");}
    static void Bind(Control control,DragEventHandler over,DragEventHandler drop){control.AllowDrop=true;control.DragEnter+=over;control.DragOver+=over;control.DragDrop+=drop;control.DragLeave+=(s,e)=>Clear();foreach(Control child in control.Controls)Bind(child,over,drop);}
    static void ScrollEdge(Control row,Point screen){for(Control c=row.Parent;c!=null;c=c.Parent){var scroll=c as ScrollableControl;if(scroll==null||!scroll.AutoScroll||!scroll.VerticalScroll.Visible)continue;Point p=scroll.PointToClient(screen);int delta=p.Y<28?-20:p.Y>scroll.ClientSize.Height-28?20:0;int now=Environment.TickCount;if(delta!=0&&unchecked(now-lastScroll)>70){lastScroll=now;scroll.AutoScrollPosition=new Point(0,Math.Max(0,-scroll.AutoScrollPosition.Y+delta));}break;}}
}
