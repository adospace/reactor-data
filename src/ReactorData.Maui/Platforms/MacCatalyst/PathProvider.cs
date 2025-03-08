using Foundation;
using System.IO;
using System;

namespace ReactorData.Maui.Platforms.MacCatalyst;

public class PathProvider : IPathProvider
{
    /// <inheritdoc />
    public string GetDefaultLocalMachineCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.CachesDirectory);

    /// <inheritdoc />
    public string GetDefaultRoamingCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.ApplicationSupportDirectory);

    /// <inheritdoc />
    public string GetDefaultSecretCacheDirectory() => CreateAppDirectory(NSSearchPathDirectory.ApplicationSupportDirectory, "SecretCache");

    private string CreateAppDirectory(NSSearchPathDirectory targetDir, string subDir = "BlobCache")
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
