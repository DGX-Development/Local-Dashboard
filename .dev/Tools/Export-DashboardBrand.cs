using System;
using System.IO;
using System.Drawing;
internal static class ExportDashboardBrand {
    static int Main(string[] args){
        if(args.Length<1||args.Length>2)throw new ArgumentException("Supply the ICO output path and optional wizard PNG path.");
        var accent=Color.FromArgb(154,217,193);
        File.WriteAllBytes(args[0],DashboardBrand.IconBytes(accent));
        if(args.Length==2)
            using(var image=DashboardBrand.Render(accent,116))
            using(var canvas=new Bitmap(147,147,System.Drawing.Imaging.PixelFormat.Format32bppArgb)){
                using(var graphics=Graphics.FromImage(canvas))
                    graphics.DrawImageUnscaled(image,8,23);
                canvas.Save(args[1],System.Drawing.Imaging.ImageFormat.Png);
            }
        return 0;
    }
}