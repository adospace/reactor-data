using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReactorData.Implementation;

namespace ReactorData.EFCore;

public static class ServiceCollectionExtensions
{
    public static void AddReactorDataWithEfCore<T>(this IServiceCollection services, 
        Action<DbContextOptionsBuilder>? optionsAction = null,
        Action<ModelContextOptions>? modelContextConfigure = null) where T : DbContext
    {
        services.AddReactorData(modelContextConfigure);
        services.AddDbContext<T>(optionsAction);
        services.AddSingleton<IStorage, Implementation.Storage<T>>();
    }
}
