using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Globalization;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Reflection;

internal static class DashboardBrand {
    internal static readonly int[] Sizes={16,20,24,32,40,48,64,128,256};
    internal static double Luminance(Color color){Func<byte,double> linear=value=>{double c=value/255.0;return c<=.04045?c/12.92:Math.Pow((c+.055)/1.055,2.4);};return .2126*linear(color.R)+.7152*linear(color.G)+.0722*linear(color.B);}
    internal static double Contrast(Color a,Color b){double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
    internal static Color InnerColor(Color accent){var dark=Color.FromArgb(22,41,33);var light=Color.FromArgb(242,242,242);return Contrast(accent,dark)>=Contrast(accent,light)?dark:light;}
    internal static Bitmap Render(Color accent,int size){
        var xml=new XmlDocument{XmlResolver=null};
        using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("DashboardBrand.svg"))xml.Load(source);
        using(var large=new Bitmap(size*8,size*8,PixelFormat.Format32bppPArgb)){
            using(var g=Graphics.FromImage(large)){
                g.Clear(Color.Transparent);g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size*8/64f,size*8/64f);
                foreach(XmlElement rect in xml.SelectNodes("//*[local-name()='rect']")){
                    Func<string,float> number=name=>String.IsNullOrEmpty(rect.GetAttribute(name))?0:Single.Parse(rect.GetAttribute(name),CultureInfo.InvariantCulture);
                    string fill=rect.GetAttribute("fill");if(fill=="")fill=((XmlElement)rect.ParentNode).GetAttribute("fill");
                    Color color=fill.Equals("#9ad9c1",StringComparison.OrdinalIgnoreCase)?accent:fill.Equals("#162921",StringComparison.OrdinalIgnoreCase)?InnerColor(accent):ColorTranslator.FromHtml(fill);
                    float x=number("x"),y=number("y"),w=number("width"),h=number("height"),d=number("rx")*2;
                    using(var path=new GraphicsPath())using(var brush=new SolidBrush(color)){
                        if(d==0)path.AddRectangle(new RectangleF(x,y,w,h));
                        else{path.AddArc(x,y,d,d,180,90);path.AddArc(x+w-d,y,d,d,270,90);path.AddArc(x+w-d,y+h-d,d,d,0,90);path.AddArc(x,y+h-d,d,d,90,90);path.CloseFigure();}
                        g.FillPath(brush,path);
                    }
                }
            }
            var result=new Bitmap(size,size,PixelFormat.Format32bppPArgb);
            using(var g=Graphics.FromImage(result))using(var attributes=new ImageAttributes()){
                g.CompositingMode=CompositingMode.SourceCopy;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                attributes.SetWrapMode(WrapMode.TileFlipXY);
                g.DrawImage(large,new Rectangle(0,0,size,size),0,0,large.Width,large.Height,GraphicsUnit.Pixel,attributes);
            }
            return result;
        }
    }
    internal static byte[] IconBytes(Color accent){
        var frames=new List<byte[]>();
        foreach(int size in Sizes)using(var image=Render(accent,size))using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);frames.Add(stream.ToArray());}
        using(var stream=new MemoryStream())using(var writer=new BinaryWriter(stream)){
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)Sizes.Length);int offset=6+16*Sizes.Length;
            for(int i=0;i<Sizes.Length;i++){writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));writer.Write((byte)(Sizes[i]==256?0:Sizes[i]));writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(frames[i].Length);writer.Write(offset);offset+=frames[i].Length;}
            foreach(byte[] frame in frames)writer.Write(frame);return stream.ToArray();
        }
    }
}
