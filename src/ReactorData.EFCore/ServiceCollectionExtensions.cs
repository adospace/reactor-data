using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ReactorData.EFCore;

public static class ServiceCollectionExtensions
{
    public static void AddReactorDataWithEfCore<T>(this IServiceCollection services, Action<DbContextOptionsBuilder>? optionsAction = null) where T : DbContext
    {
        services.AddReactorData();
        services.AddDbContext<T>(optionsAction);
        services.AddSingleton<IStorage, Implementation.Storage<T>>();
    }
}
