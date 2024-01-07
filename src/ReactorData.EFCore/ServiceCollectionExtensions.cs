using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ReactorData.EFCore;

public static class ServiceCollectionExtensions
{
    public static void AddReactorData<T>(this IServiceCollection services) where T : DbContext
    {
        services.AddSingleton<IStorage, Implementation.Storage<T>>();
    }

}
