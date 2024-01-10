using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ReactorData.Sqlite;

public static class ServiceCollectionExtensions
{
    public static void AddReactorDataWithSqlite(this IServiceCollection services, 
        string connectionString,
        Action<StorageConfiguration>? configure = null,
        Action<ModelContextOptions>? modelContextConfigure = null)
    {
        services.AddReactorData(modelContextConfigure);
        services.AddSingleton<IStorage>(sp => new Implementation.Storage(sp, connectionString, configure));
    }

    public static void AddReactorDataWithSqlite(this IServiceCollection services, 
        SqliteConnection connection, 
        Action<StorageConfiguration>? configure = null,
        Action<ModelContextOptions>? modelContextConfigure = null)
    {
        services.AddReactorData(modelContextConfigure);
        services.AddSingleton<IStorage>(sp => new Implementation.Storage(sp, connection, configure));
    }
}
