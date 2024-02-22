using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Implementation;

namespace ReactorData.EFCore;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ReactorData services with EF Core storage
    /// </summary>
    /// <typeparam name="T">DbContext-derived type that describe the Ef Core database context</typeparam>
    /// <param name="services">Service collection to modify</param>
    /// <param name="optionsAction">Action called when the database context needs to be configured</param>
    /// <param name="modelContextConfigure">Action called when the <see cref="IModelContext"/> needs to be configured</param>
    public static void AddReactorData<T>(this IServiceCollection services, 
        Action<DbContextOptionsBuilder>? optionsAction = null,
        Action<ModelContextOptions>? modelContextConfigure = null) where T : DbContext
    {
        services.AddReactorData(modelContextConfigure);
        services.AddDbContext<T>(optionsAction);
        services.AddSingleton<IStorage, Implementation.Storage<T>>();
    }
}
