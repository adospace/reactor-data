using Foundation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Maui.Platforms.iOS;

internal class PathProvider : IPathProvider
{
    public string GetDefaultLocalMachineCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.CachesDirectory);

    /// <inheritdoc />
    public string GetDefaultRoamingCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.ApplicationSupportDirectory);

    /// <inheritdoc />
    public string GetDefaultSecretCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.ApplicationSupportDirectory, "SecretCache");

    private string CreateAppDirectory(NSSearchPathDirectory targetDir, string subDir = "Cache")
    {
        using var fm = new NSFileManager();
        var url = fm.GetUrl(targetDir, NSSearchPathDomain.All, null, true, out _) ?? throw new DirectoryNotFoundException();
        var rp = url.RelativePath ?? throw new DirectoryNotFoundException();
        var ret = Path.Combine(rp, "ReactorData", subDir);
        if (!Directory.Exists(ret))
        {
            Directory.CreateDirectory(ret);
        }

        return ret;
    }
}
