param([switch]$Review,[string]$OutputPath)
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=$PSScriptRoot
if(!$Review -and !$OutputPath){
[xml]$svg=Get-Content -LiteralPath (Join-Path $root 'dist\icons\local-dashboard.svg') -Raw
$frames=[System.Collections.Generic.List[byte[]]]::new()
$sizes=@(16,24,32,48,64,128,256)
foreach($size in $sizes){
    $bitmap=[System.Drawing.Bitmap]::new($size,$size)
    $graphics=[System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $graphics.ScaleTransform(($size/64.0),($size/64.0))
    foreach($rect in $svg.SelectNodes('//*[local-name()="rect"]')){
        $fill=$rect.GetAttribute('fill');if(!$fill){$fill=$rect.ParentNode.GetAttribute('fill')}
        $brush=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($fill))
        $x=[float]$rect.x;$y=[float]$rect.y;$w=[float]$rect.width;$h=[float]$rect.height;$radius=[float]$rect.rx
        $shape=[System.Drawing.Drawing2D.GraphicsPath]::new()
        if($radius -gt 0){$d=2*$radius;$shape.AddArc($x,$y,$d,$d,180,90);$shape.AddArc(($x+$w-$d),$y,$d,$d,270,90);$shape.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90);$shape.AddArc($x,($y+$h-$d),$d,$d,90,90);$shape.CloseFigure()}else{$shape.AddRectangle([System.Drawing.RectangleF]::new($x,$y,$w,$h))}
        $graphics.FillPath($brush,$shape);$shape.Dispose();$brush.Dispose()
    }
    $stream=[System.IO.MemoryStream]::new();$bitmap.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$frames.Add($stream.ToArray());$stream.Dispose();$graphics.Dispose();$bitmap.Dispose()
}
$iconPath=Join-Path $root 'dist\icons\local-dashboard.ico'
$output=[System.IO.File]::Create($iconPath);$writer=[System.IO.BinaryWriter]::new($output)
try{
    $writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count
    for($i=0;$i -lt $sizes.Count;$i++){$dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length}
    foreach($frame in $frames){$writer.Write([byte[]]$frame)}
}finally{$writer.Dispose();$output.Dispose()}
}
$iconPath=Join-Path $root 'dist\icons\local-dashboard.ico'
$target=if($OutputPath){[IO.Path]::GetFullPath($OutputPath)}elseif($Review){Join-Path $root '.dev\Review\Combined\LocalDashboard.1.5.6.Review.exe'}else{Join-Path $root 'LocalDashboard.exe'}
if($Review){New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if($Review){$iconPath=Join-Path $root '.dev\Review\Icon\local-dashboard-draft.ico';if(!(Test-Path -LiteralPath $iconPath)){& (Join-Path $root '.dev\Tools\Export-Dashboard-Icon.ps1')}}
& $compiler /nologo /target:winexe /optimize+ /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Web.Extensions.dll ('/win32icon:'+$iconPath) ('/out:'+$target) (Join-Path $root 'LocalDashboard.cs') (Join-Path $root 'AgentStatus.cs') (Join-Path $root 'DashboardControls.cs') (Join-Path $root 'CurrentWork.cs') (Join-Path $root 'DashboardInformation.cs') (Join-Path $root 'SidebarEditor.cs') (Join-Path $root 'ReorderDrag.cs') (Join-Path $root 'WindowSettings.cs') (Join-Path $root 'CatalogEditor.cs')
if($LASTEXITCODE -ne 0){throw 'Windows helper compilation failed'}
Write-Output ('Built '+$target)









