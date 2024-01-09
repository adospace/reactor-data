using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace ReactorData.Sqlite;

public static class ServiceCollectionExtensions
{
    public static void AddReactorDataWithSqlite(this IServiceCollection services, string connectionString, Action<StorageConfiguration>? configureAction = null)
    {
        services.AddReactorData();
        services.AddSingleton<IStorage>(sp => new Implementation.Storage(sp, connectionString, configureAction));
    }

    public static void AddReactorDataWithSqlite(this IServiceCollection services, SqliteConnection connection, Action<StorageConfiguration>? configureAction = null)
    {
        services.AddReactorData();
        services.AddSingleton<IStorage>(sp => new Implementation.Storage(sp, connection, configureAction));
    }
}
