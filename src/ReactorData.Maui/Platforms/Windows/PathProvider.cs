
using System;
using System.IO;

namespace ReactorData.Maui.Platforms.Windows;

// All the code in this file is only included on Windows.
public class PathProvider : IPathProvider
{
    /// <inheritdoc />
    public string GetDefaultRoamingCacheDirectory() =>
        GetOrCreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReactorData", "BlobCache"));

    /// <inheritdoc />
    public string GetDefaultSecretCacheDirectory() =>
        GetOrCreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ReactorData", "SecretCache"));

    /// <inheritdoc />
    public string GetDefaultLocalMachineCacheDirectory() =>
        GetOrCreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ReactorData", "BlobCache"));

    static string GetOrCreateDirectory(string directory)
    {
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        return directory;
    }
}
