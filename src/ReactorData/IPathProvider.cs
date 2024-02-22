using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public interface IPathProvider
{    
    /// <summary>
    /// Gets the default local machine cache directory (i.e. the one for temporary data).
    /// </summary>
    /// <returns>The default local machine cache directory.</returns>
    string? GetDefaultLocalMachineCacheDirectory();

    /// <summary>
    /// Gets the default roaming cache directory (i.e. the one for user settings).
    /// </summary>
    /// <returns>The default roaming cache directory.</returns>
    string? GetDefaultRoamingCacheDirectory();

    /// <summary>
    /// Gets the default roaming cache directory (i.e. the one for user secrets).
    /// </summary>
    /// <returns>The default roaming cache directory.</returns>
    string? GetDefaultSecretCacheDirectory();
}
