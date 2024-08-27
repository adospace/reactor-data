using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ReactorData.Sqlite;

public static class ServiceCollectionExtensions
{
    public static void AddReactorData(this IServiceCollection services,
        string connectionStringOrDatabaseName,
        Action<StorageConfiguration>? configure = null,
        Action<ModelContextOptions>? modelContextConfigure = null)
    {
        services.AddReactorData(modelContextConfigure);
        services.AddSingleton<IStorage>(sp =>
        {
            if (!connectionStringOrDatabaseName.Trim()
                .StartsWith("Data Source",StringComparison.CurrentCultureIgnoreCase))
            {
                var pathProvider = sp.GetService<IPathProvider>();
                var connectionString = $"Data Source={Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), connectionStringOrDatabaseName)}";
                if (pathProvider != null)
                {
                    var roamingCacheDirectory = pathProvider.GetDefaultRoamingCacheDirectory();
                    if (roamingCacheDirectory != null)
                    {
                        connectionString = $"Data Source={Path.Combine(roamingCacheDirectory, connectionStringOrDatabaseName)}";
                    }
                }

                return new Implementation.Storage(sp, connectionString, configure);
            }

            return new Implementation.Storage(sp, connectionStringOrDatabaseName, configure);
        });
    }

    public static void AddReactorData(this IServiceCollection services, 
        SqliteConnection connection, 
        Action<StorageConfiguration>? configure = null,
        Action<ModelContextOptions>? modelContextConfigure = null)
    {
        services.AddReactorData(modelContextConfigure);
        services.AddSingleton<IStorage>(sp => new Implementation.Storage(sp, connection, configure));
    }
}
