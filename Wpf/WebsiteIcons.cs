using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

internal static class WebsiteIcons
{
    const int MaximumBytes=256*1024;

    static WebsiteIcons()
    {
        // The Framework compiler can leave HTTPS requests on TLS 1.0 by default.
        ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
    }

    internal static Uri Origin(Entry entry)
    {
        if(!String.IsNullOrEmpty(entry.iconGlyph)||!String.IsNullOrEmpty(entry.iconPath)||!String.IsNullOrEmpty(entry.iconAsset))return null;
        var target=entry.targets==null?null:entry.targets.FirstOrDefault();
        Uri uri;
        if(target==null||target.kind!="web"||!Uri.TryCreate(target.url,UriKind.Absolute,out uri)||
            (uri.Scheme!=Uri.UriSchemeHttps&&uri.Scheme!=Uri.UriSchemeHttp)||uri.UserInfo.Length!=0)return null;
        return new Uri(uri.GetLeftPart(UriPartial.Authority)+"/");
    }

    static string CachePath(string profile,Uri origin)
    {
        return Path.Combine(profile,".local","website-icons",DashboardData.Hash(Encoding.UTF8.GetBytes(origin.AbsoluteUri))+".png");
    }

    internal static BitmapSource Cached(string profile,Uri origin)
    {
        try
        {
            var path=CachePath(profile,origin);
            if(!File.Exists(path))return null;
            using(var stream=File.OpenRead(path))
            {
                if(stream.Length>MaximumBytes)return null;
                return Decode(stream);
            }
        }
        catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException||error is NotSupportedException||error is FormatException||error is ArgumentException||error is System.Runtime.InteropServices.COMException))throw;return null;}
    }

    internal static BitmapSource Fetch(string profile,Uri origin,Func<Uri,byte[]> download=null,bool usePublicIndex=false)
    {
        var cached=Cached(profile,origin);
        if(cached!=null)return cached;
        Uri pageAddress=origin;
        if(download==null)download=uri=>{
            Uri finalAddress;
            var bytes=Download(uri,out finalAddress);
            if(uri==origin)pageAddress=finalAddress;
            return bytes;
        };
        try
        {
            var icon=ReadIcon(download(new Uri(origin,"favicon.ico")));
            if(icon==null)
            {
                var page=download(origin);
                if(page!=null)
                {
                    var html=Encoding.UTF8.GetString(page);
                    int attempts=0;
                    foreach(Match link in Regex.Matches(html,@"<link\b[^>]*>",RegexOptions.IgnoreCase))
                    {
                        var rel=Attribute(link.Value,"rel");
                        if(!rel.Split((char[])null,StringSplitOptions.RemoveEmptyEntries).Contains("icon",StringComparer.OrdinalIgnoreCase))continue;
                        Uri candidate;
                        if(!Uri.TryCreate(pageAddress,Attribute(link.Value,"href"),out candidate)||!SameOrigin(pageAddress,candidate))continue;
                        if(attempts++==3)break;
                        icon=ReadIcon(download(candidate));
                        if(icon!=null)break;
                    }
                }
            }
            if(icon==null&&usePublicIndex&&CanUsePublicIndex(origin))
                icon=ReadIcon(download(new Uri("https://icons.duckduckgo.com/ip3/"+Uri.EscapeDataString(origin.IdnHost)+".ico")));
            if(icon!=null)
            {
                try
                {
                    var encoder=new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(icon));
                    using(var stream=new MemoryStream())
                    {
                        encoder.Save(stream);
                        DashboardData.Atomic(CachePath(profile,origin),stream.ToArray());
                    }
                }
                catch(Exception error){if(!(error is IOException||error is UnauthorizedAccessException))throw;}
            }
            return icon;
        }
        catch(Exception error){if(!(error is IOException||error is WebException||error is UnauthorizedAccessException||error is ArgumentException))throw;return null;}
    }

    static bool CanUsePublicIndex(Uri origin)
    {
        string host=origin.IdnHost.TrimEnd('.');
        if(origin.HostNameType!=UriHostNameType.Dns||origin.IsLoopback||!origin.IsDefaultPort||host.IndexOf('.')<0)return false;
        string suffix=host.Substring(host.LastIndexOf('.')+1);
        return !new[]{"local","localhost","internal","test","invalid","example","lan","corp","home","onion"}.Contains(suffix,StringComparer.OrdinalIgnoreCase)
            &&!host.EndsWith(".home.arpa",StringComparison.OrdinalIgnoreCase);
    }

    static string Attribute(string tag,string name)
    {
        var match=Regex.Match(tag,@"\s"+name+@"\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))",RegexOptions.IgnoreCase);
        var value=match.Groups.Cast<Group>().Skip(1).FirstOrDefault(group=>group.Success);
        return value==null?"":WebUtility.HtmlDecode(value.Value);
    }

    static bool SameOrigin(Uri origin,Uri address)
    {
        return address.IsAbsoluteUri&&address.UserInfo.Length==0&&address.Scheme==origin.Scheme&&
            address.IdnHost.Equals(origin.IdnHost,StringComparison.OrdinalIgnoreCase)&&address.Port==origin.Port;
    }

    static byte[] Download(Uri address,out Uri finalAddress)
    {
        finalAddress=address;
        for(int redirects=0;redirects<=4;redirects++)
        {
            finalAddress=address;
            try
            {
                var request=(HttpWebRequest)WebRequest.Create(address);
                request.AllowAutoRedirect=false;
                request.Timeout=2500;
                request.ReadWriteTimeout=1500;
                request.UserAgent="Local Dashboard";
                request.AutomaticDecompression=DecompressionMethods.GZip|DecompressionMethods.Deflate;
                using(var response=(HttpWebResponse)request.GetResponse())
                {
                    if((int)response.StatusCode>=300&&(int)response.StatusCode<400)
                    {
                        Uri next;
                        if(!Uri.TryCreate(address,response.Headers[HttpResponseHeader.Location],out next)||
                            (!SameOrigin(address,next)&&(next.Scheme!=Uri.UriSchemeHttps||!next.IsDefaultPort||
                            next.HostNameType!=UriHostNameType.Dns||next.IdnHost.IndexOf('.')<0||next.IsLoopback||next.UserInfo.Length!=0)))return null;
                        address=next;
                        continue;
                    }
                    if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>MaximumBytes)return null;
                    using(var input=response.GetResponseStream())
                    using(var output=new MemoryStream())
                    {
                        var deadline=DateTime.UtcNow.AddSeconds(3);
                        var buffer=new byte[8192];
                        int count;
                        while((count=input.Read(buffer,0,buffer.Length))!=0)
                        {
                            if(output.Length+count>MaximumBytes||DateTime.UtcNow>deadline)return null;
                            output.Write(buffer,0,count);
                        }
                        return output.ToArray();
                    }
                }
            }
            catch(WebException error)
            {
                if(error.Response!=null)error.Response.Dispose();
                return null;
            }
            catch(IOException){return null;}
        }
        return null;
    }

    static BitmapSource ReadIcon(byte[] bytes)
    {
        if(bytes==null||bytes.Length<8||bytes.Length>MaximumBytes)return null;
        bool ico=bytes[0]==0&&bytes[1]==0&&bytes[2]==1&&bytes[3]==0;
        bool png=bytes[0]==137&&bytes[1]==80&&bytes[2]==78&&bytes[3]==71;
        bool gif=bytes[0]==71&&bytes[1]==73&&bytes[2]==70;
        bool jpeg=bytes[0]==255&&bytes[1]==216&&bytes[2]==255;
        if(!ico&&!png&&!gif&&!jpeg)return null;
        try{using(var stream=new MemoryStream(bytes))return Decode(stream);}
        catch(Exception error){if(!(error is IOException||error is NotSupportedException||error is FormatException||error is ArgumentException||error is System.Runtime.InteropServices.COMException))throw;return null;}
    }

    static BitmapSource Decode(Stream stream)
    {
        var decoder=BitmapDecoder.Create(stream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnDemand);
        var frame=decoder.Frames.Where(item=>item.PixelWidth>0&&item.PixelHeight>0&&item.PixelWidth<=512&&item.PixelHeight<=512)
            .OrderBy(item=>Math.Abs(Math.Max(item.PixelWidth,item.PixelHeight)-48)).FirstOrDefault();
        if(frame==null)return null;
        double scale=Math.Min(1.0,64.0/Math.Max(frame.PixelWidth,frame.PixelHeight));
        var image=new TransformedBitmap(frame,new ScaleTransform(scale,scale));
        var pixels=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);
        int stride=pixels.PixelWidth*4;
        var bytes=new byte[stride*pixels.PixelHeight];
        pixels.CopyPixels(bytes,stride,0);
        var copy=BitmapSource.Create(pixels.PixelWidth,pixels.PixelHeight,96,96,PixelFormats.Bgra32,null,bytes,stride);
        copy.Freeze();
        return copy;
    }
}
