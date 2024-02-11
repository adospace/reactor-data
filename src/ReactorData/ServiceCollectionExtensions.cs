using Microsoft.Extensions.DependencyInjection;
using ReactorData.Implementation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Add ReactorData services
    /// </summary>
    /// <param name="services">Service collection to modify</param>
    /// <param name="configureAction">Uses this function to modify any options related to the <see cref="IModelContext"/> creation</param>
    public static void AddReactorData(this IServiceCollection services, Action<ModelContextOptions>? configureAction = null)
    {
        services.AddSingleton<IModelContext>(sp =>
        { 
            var options = new ModelContextOptions();
            configureAction?.Invoke(options);
            return new ModelContext(sp, options);
        });
    }
}
