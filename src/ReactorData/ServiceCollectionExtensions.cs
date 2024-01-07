using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

public static class ServiceCollectionExtensions
{
    public static void AddReactorData(this IServiceCollection services)
    {
        services.AddSingleton<IContainer, Implementation.Container>();
    }
}
