using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
internal static class DashboardTheme {
    internal static bool Dark=true;
    internal static string Preference="Dark";
    internal static bool Valid(string value){return value==null||value=="Dark"||value=="Light"||value=="System";}
    internal static Color Resolve(string hex){var c=(Color)ColorConverter.ConvertFromString(hex);double brightness=(c.R*.2126+c.G*.7152+c.B*.0722)/255;
        if(SystemParameters.HighContrast)return brightness>.53?SystemColors.WindowTextColor:SystemColors.WindowColor;
        if(Dark)return c;
        var map=new Dictionary<string,string>{{"202020","F5F5F5"},{"252525","EFEFEF"},{"262626","EAEAEA"},{"2B2B2B","FFFFFF"},{"303030","F0F0F0"},{"383838","E0E0E0"},{"454545","D2D2D2"},{"969696","646464"},{"B9B9B9","535353"},{"C1C1C1","454545"},{"F2F2F2","202020"},{"FFFFFF","202020"},{"888888","666666"}};string mapped;if(map.TryGetValue(hex.TrimStart('#').ToUpperInvariant(),out mapped))return (Color)ColorConverter.ConvertFromString("#"+mapped);
        byte v=(byte)Math.Max(30,Math.Min(245,255-brightness*255));return Color.FromArgb(c.A,v,v,v);
    }
    internal static void Apply(string preference){
        if(!Valid(preference))throw new System.IO.IOException("Unsupported theme preference.");Preference=preference??"Dark";Dark=Preference!="Light";
        if(Preference=="System"){try{Dark=Convert.ToInt32(Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1))==0;}catch{Dark=true;}}
        var resources=Application.Current.Resources;
        resources["MenuShadowOpacity"]=SystemParameters.HighContrast?0.0:Dark?0.30:0.18;
        foreach(string key in new[]{"202020","252525","262626","2B2B2B","303030","383838","414141","444444","454545","484848","4A4A4A","5A5A5A","888888","969696","A5A5A5","B9B9B9","C1C1C1","EEEEEE","F2F2F2","FFFFFF"})resources["Theme"+key]=new SolidColorBrush(Resolve("#"+key));
        resources["DgxPublisherForeground"]=new SolidColorBrush(SystemParameters.HighContrast?SystemColors.WindowTextColor:Dark?Colors.White:Color.FromRgb(22,41,33));
        resources["Background"]=resources["Theme202020"];resources["Surface"]=resources["Theme2B2B2B"];resources["Muted"]=resources["ThemeB9B9B9"];
    }
    static double Linear(double value){value/=255;return value<=.04045?value/12.92:Math.Pow((value+.055)/1.055,2.4);}
    internal static System.Drawing.Color AccentText(System.Drawing.Color c){if(Dark)return DashboardAccent.ReadableText(c);double r=c.R,g=c.G,b=c.B;while((1.0+.05)/(.2126*Linear(r)+.7152*Linear(g)+.0722*Linear(b)+.05)<5){r*=.9;g*=.9;b*=.9;}return System.Drawing.Color.FromArgb((int)r,(int)g,(int)b);}
}
