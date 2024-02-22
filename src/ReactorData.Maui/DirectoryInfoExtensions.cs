using System.Collections.Generic;
using System.IO;
using System.Linq;

//NOTE: part of this code is freely taken from https://github.com/reactiveui/Akavache/blob/main/src/Akavache.Core/Platforms/shared/Utility.cs

namespace ReactorData.Maui;

// All the code in this file is included in all platforms.
internal static class DirectoryInfoExtensions

{
    public static void CreateRecursive(this DirectoryInfo directoryInfo) =>
        _ = directoryInfo.SplitFullPath().Aggregate((parent, dir) =>
        {
            var path = Path.Combine(parent, dir);

            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }

            return path;
        });

    public static IEnumerable<string> SplitFullPath(this DirectoryInfo directoryInfo)
    {
        var root = Path.GetPathRoot(directoryInfo.FullName);
        var components = new List<string>();
        for (var path = directoryInfo.FullName; path != root && path is not null; path = Path.GetDirectoryName(path))
        {
            var filename = Path.GetFileName(path);
            if (string.IsNullOrEmpty(filename))
            {
                continue;
            }

            components.Add(filename);
        }

        if (root is not null)
        {
            components.Add(root);
        }

        components.Reverse();
        return components;
    }
}
