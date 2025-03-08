using System;
using System.IO;

namespace ReactorData.Maui.Platforms.Tizen;

public class PathProvider : IPathProvider
{
    /// <inheritdoc />
    public string GetDefaultLocalMachineCacheDirectory() => Application.Current.DirectoryInfo.Cache;

    /// <inheritdoc />
    public string GetDefaultRoamingCacheDirectory() => Application.Current.DirectoryInfo.ExternalCache;

    /// <inheritdoc />
    public string GetDefaultSecretCacheDirectory()
    {
        var path = Application.Current.DirectoryInfo.ExternalCache;
        var di = new System.IO.DirectoryInfo(Path.Combine(path, "Secret"));
        if (!di.Exists)
        {
            di.CreateRecursive();
        }

        return di.FullName;
    }
}