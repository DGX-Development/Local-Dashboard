using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
internal sealed class DashboardButton:Button {
    bool hover,pressed,isChecked;internal bool ShowGlyph=true;internal bool IsChecked{get{return isChecked;}set{isChecked=value;Invalidate();}}
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}
    protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
    protected override void OnPaint(PaintEventArgs e){e.Graphics.Clear(Parent==null?DashboardForm.Background:Parent.BackColor);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;Color fill=IsChecked?Color.FromArgb(69,69,69):pressed?ControlPaint.Dark(BackColor,.12f):hover?ControlPaint.Light(BackColor,.08f):BackColor;using(var path=DashboardMenuRenderer.Round(new RectangleF(.5f,.5f,Width-1,Height-1),7))using(var brush=new SolidBrush(fill))e.Graphics.FillPath(brush,path);int left=!ShowGlyph||Image==null?(Text.Length==1?0:8):36;if(ShowGlyph&&Image!=null){e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(Image,new Rectangle(10,(Height-16)/2,16,16));}var text=new Rectangle(left,0,Math.Max(1,Width-left-(Text.Length==1?0:8)-(IsChecked?20:0)),Height);TextRenderer.DrawText(e.Graphics,Text,Font,text,Enabled?ForeColor:DashboardForm.Muted,TextFormatFlags.NoPrefix|TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine|TextFormatFlags.EndEllipsis|(TextAlign==ContentAlignment.MiddleLeft?TextFormatFlags.Left:TextFormatFlags.HorizontalCenter));if(IsChecked){using(var check=new Pen(DashboardForm.Accent,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round})e.Graphics.DrawLines(check,new[]{new PointF(Width-19,Height/2f),new PointF(Width-15,Height/2f+4),new PointF(Width-9,Height/2f-4)});}if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,new Rectangle(3,3,Math.Max(1,Width-7),Math.Max(1,Height-7)),DashboardForm.Accent,fill);}
}
internal sealed class DashboardMenuRenderer:ToolStripProfessionalRenderer {
    static readonly Dictionary<string,Bitmap> glyphs=new Dictionary<string,Bitmap>();
    internal static GraphicsPath Round(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
    protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e){e.Graphics.Clear(e.ToolStrip.BackColor);}
    protected override void OnRenderImageMargin(ToolStripRenderEventArgs e){}
    protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e){if(e.ToolStrip is ToolStripDropDown)using(var pen=new Pen(DashboardForm.Border))e.Graphics.DrawRectangle(pen,0,0,e.ToolStrip.Width-1,e.ToolStrip.Height-1);}
    protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e){if(e.Item.Selected||e.Item.Pressed){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using(var brush=new SolidBrush(SystemInformation.HighContrast?SystemColors.Highlight:Color.FromArgb(56,56,56)))using(var shape=Round(new RectangleF(2,1,e.Item.Width-4,e.Item.Height-2),6))e.Graphics.FillPath(brush,shape);}}
    protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e){using(var pen=new Pen(DashboardForm.Border))e.Graphics.DrawLine(pen,10,e.Item.Height/2,e.Item.Width-10,e.Item.Height/2);}
    protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e){e.TextColor=SystemInformation.HighContrast&&e.Item.Selected?SystemColors.HighlightText:DashboardForm.Ink;if(e.ToolStrip is ToolStripDropDown)e.TextRectangle=new Rectangle(46,0,Math.Max(1,e.Item.Width-66),e.Item.Height);else e.TextRectangle=new Rectangle(e.TextRectangle.X,0,e.TextRectangle.Width,e.Item.Height);e.TextFormat|=TextFormatFlags.VerticalCenter|TextFormatFlags.SingleLine;base.OnRenderItemText(e);}
    protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e){float y=e.Item.Height/2f;using(var pen=new Pen(SystemInformation.HighContrast?(e.Item.Selected?SystemColors.HighlightText:SystemColors.MenuText):DashboardForm.Accent,1.7f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;e.Graphics.DrawLines(pen,new[]{new PointF(16,y),new PointF(20,y+4),new PointF(27,y-4)});}}
    protected override void OnRenderItemImage(ToolStripItemImageRenderEventArgs e){if(e.Image==null||(e.Item is ToolStripMenuItem&&((ToolStripMenuItem)e.Item).Checked))return;e.Graphics.InterpolationMode=InterpolationMode.HighQualityBicubic;e.Graphics.DrawImage(e.Image,new Rectangle(14,(e.Item.Height-16)/2,16,16));}
    internal static Bitmap ExitGlyph(){return Glyph("exit");}
    internal static Bitmap Glyph(string kind){if(glyphs.ContainsKey(kind))return new Bitmap(glyphs[kind]);var image=new Bitmap(64,64);using(var g=Graphics.FromImage(image))using(var pen=new Pen(Color.FromArgb(210,210,210),1.35f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round}){g.ScaleTransform(4,4);g.SmoothingMode=SmoothingMode.AntiAlias;switch(kind){
        case "exit":g.DrawLines(pen,new[]{new PointF(6,1),new PointF(1,1),new PointF(1,15),new PointF(6,15)});g.DrawLine(pen,6,8,15,8);g.DrawLines(pen,new[]{new PointF(11,4),new PointF(15,8),new PointF(11,12)});break;
        case "settings":
            var gear=new[]{new PointF(6,1),new PointF(10,1),new PointF(10,3),new PointF(12,4),
                new PointF(14,3),new PointF(16,6),new PointF(14,8),new PointF(14,10),
                new PointF(16,12),new PointF(14,15),new PointF(12,14),new PointF(10,15),
                new PointF(10,17),new PointF(6,17),new PointF(6,15),new PointF(4,14),
                new PointF(2,15),new PointF(0,12),new PointF(2,10),new PointF(2,8),
                new PointF(0,6),new PointF(2,3),new PointF(4,4),new PointF(6,3)};
            for(int i=0;i<gear.Length;i++)gear[i]=new PointF(1+gear[i].X*.875f,.125f+gear[i].Y*.875f);
            g.DrawPolygon(pen,gear);
            g.DrawEllipse(pen,5.375f,5.375f,5.25f,5.25f);
            break;
        case "hide":
        case "restore":
            g.DrawLines(pen,new[]{new PointF(2,9),new PointF(2,15),new PointF(14,15),new PointF(14,9)});
            g.DrawLine(pen,8,1,8,11);
            g.DrawLines(pen,kind=="hide"
                ?new[]{new PointF(4,7),new PointF(8,11),new PointF(12,7)}
                :new[]{new PointF(4,5),new PointF(8,1),new PointF(12,5)});
            break;
        case "folder":g.DrawLines(pen,new[]{new PointF(1,4),new PointF(1,2),new PointF(6,2),new PointF(8,4),new PointF(15,4),new PointF(15,14),new PointF(1,14),new PointF(1,4)});break;
        case "dashboard":g.DrawRectangle(pen,1,2,14,12);g.DrawLine(pen,1,6,15,6);g.DrawLine(pen,7,6,7,14);g.DrawLine(pen,7,10,15,10);break;
        case "checklist":g.DrawRectangle(pen,2,1,12,14);g.DrawLines(pen,new[]{new PointF(4,5),new PointF(5,6),new PointF(7,4)});g.DrawLine(pen,10,5,12,5);g.DrawLines(pen,new[]{new PointF(4,11),new PointF(5,12),new PointF(7,10)});g.DrawLine(pen,10,11,12,11);break;
        case "reports":g.DrawLines(pen,new[]{new PointF(1,1),new PointF(1,15),new PointF(15,15)});g.DrawLine(pen,5,11,5,8);g.DrawLine(pen,9,11,9,3);g.DrawLine(pen,13,11,13,6);break;
        case "help":g.DrawEllipse(pen,1,1,14,14);g.DrawBezier(pen,new PointF(5,5),new PointF(5,1),new PointF(13,3),new PointF(9,7));g.DrawLine(pen,9,7,8,9);g.DrawLine(pen,8,12,8,12.2f);break;
        case "info":g.DrawEllipse(pen,1,1,14,14);g.DrawLine(pen,8,7,8,12);g.DrawLine(pen,8,4,8,4.2f);break;
        case "categories":foreach(var p in new[]{new Point(1,1),new Point(10,1),new Point(1,10),new Point(10,10)})g.DrawRectangle(pen,p.X,p.Y,5,5);break;
        case "dev-guide":
            pen.Color=Color.FromArgb(242,242,242);
            g.DrawLines(pen,new[]{new PointF(8,3),new PointF(2,2),new PointF(2,13),new PointF(8,14),new PointF(14,13),new PointF(14,2),new PointF(8,3),new PointF(8,14)});
            g.DrawLine(pen,4,6,6,6);
            g.DrawLine(pen,10,6,12,6);
            g.DrawLine(pen,4,9,6,9);
            g.DrawLine(pen,10,9,12,9);
            break;
        case "copy":g.DrawRectangle(pen,5,1,9,11);g.DrawLines(pen,new[]{new PointF(2,4),new PointF(2,15),new PointF(11,15)});break;
        case "globe":
            g.DrawEllipse(pen,1,1,14,14);
            g.DrawEllipse(pen,5,1,6,14);
            g.DrawLine(pen,1,8,15,8);
            break;
        case "workflow":
            g.DrawRectangle(pen,5,1,6,4);
            g.DrawLine(pen,8,5,8,8);
            g.DrawLine(pen,4,8,12,8);
            g.DrawLine(pen,4,8,4,11);
            g.DrawLine(pen,12,8,12,11);
            g.DrawRectangle(pen,1,11,6,4);
            g.DrawRectangle(pen,9,11,6,4);
            break;
        case "switch":g.DrawLines(pen,new[]{new PointF(2,4),new PointF(14,4),new PointF(11,1)});g.DrawLine(pen,14,4,11,7);g.DrawLines(pen,new[]{new PointF(14,12),new PointF(2,12),new PointF(5,9)});g.DrawLine(pen,2,12,5,15);break;
        case "schedule":g.DrawRectangle(pen,2,3,12,12);g.DrawLine(pen,2,6,14,6);g.DrawLine(pen,5,1,5,5);g.DrawLine(pen,11,1,11,5);g.DrawLine(pen,5,9,7,9);g.DrawLine(pen,9,9,11,9);break;
        case "updates":g.DrawArc(pen,2,1,12,12,200,270);g.DrawLines(pen,new[]{new PointF(1,1),new PointF(1,5),new PointF(5,5)});break;
        case "pin":g.DrawLines(pen,new[]{new PointF(5,1),new PointF(11,1),new PointF(10,7),new PointF(13,10),new PointF(3,10),new PointF(6,7),new PointF(5,1)});g.DrawLine(pen,8,10,8,15);break;
        default:g.DrawEllipse(pen,1,1,14,14);if(kind=="recording")g.DrawPolygon(pen,new[]{new PointF(6,3),new PointF(13,8),new PointF(6,13)});else{g.DrawLine(pen,8,7,8,12);g.DrawLine(pen,8,4,8,4.2f);}break;
    }}glyphs[kind]=image;return new Bitmap(image);}
}




