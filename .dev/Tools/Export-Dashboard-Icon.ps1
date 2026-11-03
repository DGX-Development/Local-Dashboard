$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$root=Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$master=Join-Path $root 'Artwork\Masters\local-dashboard-symbol-draft.svg'
[xml]$svg=Get-Content -LiteralPath $master -Raw
$review=Join-Path $root '.dev\Review\Icon'
New-Item -ItemType Directory -Force -Path $review | Out-Null
$sizes=@(16,20,24,32,40,48,64,128,256)
$frames=[System.Collections.Generic.List[byte[]]]::new()
foreach($size in $sizes){
    $large=[System.Drawing.Bitmap]::new(($size*8),($size*8),[System.Drawing.Imaging.PixelFormat]::Format32bppPArgb)
    $g=[System.Drawing.Graphics]::FromImage($large);$g.Clear([System.Drawing.Color]::Transparent);$g.SmoothingMode='AntiAlias';$g.ScaleTransform(($size*8/64.0),($size*8/64.0))
    foreach($rect in $svg.SelectNodes('//*[local-name()="rect"]')){
        $fill=$rect.GetAttribute('fill');if(!$fill){$fill=$rect.ParentNode.GetAttribute('fill')}
        $brush=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($fill));$x=[float]$rect.x;$y=[float]$rect.y;$w=[float]$rect.width;$h=[float]$rect.height;$d=2*[float]$rect.rx
        $shape=[System.Drawing.Drawing2D.GraphicsPath]::new();$shape.AddArc($x,$y,$d,$d,180,90);$shape.AddArc(($x+$w-$d),$y,$d,$d,270,90);$shape.AddArc(($x+$w-$d),($y+$h-$d),$d,$d,0,90);$shape.AddArc($x,($y+$h-$d),$d,$d,90,90);$shape.CloseFigure();$g.FillPath($brush,$shape);$shape.Dispose();$brush.Dispose()
    }
    $g.Dispose();$small=[System.Drawing.Bitmap]::new($size,$size,[System.Drawing.Imaging.PixelFormat]::Format32bppPArgb);$g=[System.Drawing.Graphics]::FromImage($small);$g.InterpolationMode='HighQualityBicubic';$g.PixelOffsetMode='HighQuality';$g.CompositingMode='SourceCopy';$attributes=[System.Drawing.Imaging.ImageAttributes]::new();$attributes.SetWrapMode('TileFlipXY');$g.DrawImage($large,[System.Drawing.Rectangle]::new(0,0,$size,$size),0,0,$large.Width,$large.Height,[System.Drawing.GraphicsUnit]::Pixel,$attributes);$attributes.Dispose();$g.Dispose();$large.Dispose()
    $stream=[System.IO.MemoryStream]::new();$small.Save($stream,[System.Drawing.Imaging.ImageFormat]::Png);$frames.Add($stream.ToArray());$small.Save((Join-Path $review ('local-dashboard-draft-'+$size+'.png')));$small.Dispose();$stream.Dispose()
}
$icon=Join-Path $review 'local-dashboard-draft.ico';$stream=[System.IO.File]::Create($icon);$writer=[System.IO.BinaryWriter]::new($stream)
try{$writer.Write([uint16]0);$writer.Write([uint16]1);$writer.Write([uint16]$sizes.Count);$offset=6+16*$sizes.Count;for($i=0;$i -lt $sizes.Count;$i++){$d=if($sizes[$i] -eq 256){0}else{$sizes[$i]};$writer.Write([byte]$d);$writer.Write([byte]$d);$writer.Write([byte]0);$writer.Write([byte]0);$writer.Write([uint16]1);$writer.Write([uint16]32);$writer.Write([uint32]$frames[$i].Length);$writer.Write([uint32]$offset);$offset+=$frames[$i].Length};foreach($frame in $frames){$writer.Write([byte[]]$frame)}}finally{$writer.Dispose();$stream.Dispose()}
$sheet=[System.Drawing.Bitmap]::new(1000,520);$g=[System.Drawing.Graphics]::FromImage($sheet);$font=[System.Drawing.Font]::new('Segoe UI',11)
foreach($column in @(0,1)){$x=$column*500;$background=if($column -eq 0){'#F5F5F5'}else{'#202020'};$ink=if($column -eq 0){'#202020'}else{'#F2F4F8'};$brush=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($background));$g.FillRectangle($brush,$x,0,500,520);$brush.Dispose();$brush=[System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml($ink));$g.DrawString('Local Dashboard · draft · native sizes',$font,$brush,($x+18),18);$cursor=$x+18;foreach($size in @(16,20,24,32,48,64)){$image=[System.Drawing.Image]::FromFile((Join-Path $review ('local-dashboard-draft-'+$size+'.png')));$g.DrawImageUnscaled($image,$cursor,62);$g.DrawString([string]$size,$font,$brush,$cursor,140);$image.Dispose();$cursor+=$size+28};$image=[System.Drawing.Image]::FromFile((Join-Path $review 'local-dashboard-draft-256.png'));$g.DrawImageUnscaled($image,($x+120),205);$image.Dispose();$brush.Dispose()};$sheet.Save((Join-Path $review 'comparison.png'));$font.Dispose();$g.Dispose();$sheet.Dispose()
$manifest=@{status='Draft; representative review only';source=@{path=$master;sha256=(Get-FileHash -LiteralPath $master).Hash};method='Rectangle SVG geometry; 8x supersampling; premultiplied alpha; high-quality bicubic reduction';frames=$sizes;outputs=@(Get-ChildItem -LiteralPath $review -File | Where-Object {$_.Extension -in '.png','.ico'} | ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}})}
$manifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $review 'asset-manifest.json') -Encoding UTF8
Write-Output ('Draft icon review: '+$review)
