using System;
using System.IO;
using System.Drawing;
internal static class ExportDashboardBrand {
    static int Main(string[] args){
        if(args.Length!=1)throw new ArgumentException("Supply the generated ICO output path.");
        File.WriteAllBytes(args[0],DashboardBrand.IconBytes(Color.FromArgb(154,217,193)));
        return 0;
    }
}