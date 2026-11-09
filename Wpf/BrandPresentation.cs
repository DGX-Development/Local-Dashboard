using System;
using System.Globalization;
using System.Reflection;
using System.Xml;
using System.Windows;
using System.Windows.Media;

internal sealed partial class WpfDashboard {
    static DrawingImage BrandVector(System.Drawing.Color accent){
        var xml=new XmlDocument{XmlResolver=null};using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("DashboardBrand.svg"))xml.Load(source);
        var group=new DrawingGroup();var inner=DashboardBrand.InnerColor(accent);
        foreach(XmlElement rect in xml.SelectNodes("//*[local-name()='rect']")){
            Func<string,double> number=name=>String.IsNullOrEmpty(rect.GetAttribute(name))?0:Double.Parse(rect.GetAttribute(name),CultureInfo.InvariantCulture);
            string fill=rect.GetAttribute("fill");if(fill=="")fill=((XmlElement)rect.ParentNode).GetAttribute("fill");
            var color=fill.Equals("#9ad9c1",StringComparison.OrdinalIgnoreCase)?accent:inner;
            group.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(color.R,color.G,color.B)),null,new RectangleGeometry(new Rect(number("x"),number("y"),number("width"),number("height")),number("rx"),number("rx"))));
        }
        var image=new DrawingImage(group);image.Freeze();return image;
    }
}
