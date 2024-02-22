using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Maui;

public static class ServiceCollectionExtensions
{
    public static void UseReactorData(this MauiAppBuilder appBuilder, Action<IServiceCollection>? serviceBuilderAction = null)
    {
        appBuilder.Services.AddSingleton<IDispatcher, Dispatcher>();

#if ANDROID
        appBuilder.Services.AddSingleton<IPathProvider, AndroidPathProvider>();
#endif

        serviceBuilderAction?.Invoke(appBuilder.Services);
    }
}
