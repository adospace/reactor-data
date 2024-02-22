using Android.App;
using System.IO;

//NOTE: part of this code is freely taken from https://github.com/reactiveui/Akavache/blob/main/src/Akavache.Core/Platforms/android/AndroidFilesystemProvider.cs

namespace ReactorData.Maui
{

    /// <summary>
    /// The file system provider that understands the android.
    /// </summary>
    public class AndroidPathProvider : IPathProvider
    {
        /// <inheritdoc />
        public string? GetDefaultLocalMachineCacheDirectory() => Android.App.Application.Context.CacheDir?.AbsolutePath;

        /// <inheritdoc />
        public string? GetDefaultRoamingCacheDirectory() => Android.App.Application.Context.FilesDir?.AbsolutePath;

        /// <inheritdoc />
        public string? GetDefaultSecretCacheDirectory()
        {
            var path = Android.App.Application.Context.FilesDir?.AbsolutePath;

            if (path is null)
            {
                return null;
            }

            var di = new DirectoryInfo(Path.Combine(path, "Secret"));
            if (!di.Exists)
            {
                di.CreateRecursive();
            }

            return di.FullName;
        }
    }


}

