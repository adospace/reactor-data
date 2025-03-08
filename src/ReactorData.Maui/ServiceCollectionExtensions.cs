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
    public static MauiAppBuilder UseReactorData(this MauiAppBuilder appBuilder, Action<IServiceCollection>? serviceBuilderAction = null, Action<Exception>? onError = null)
    {
        var dispatcher = new Dispatcher(onError);
        appBuilder.Services.AddSingleton<IDispatcher>(dispatcher);

#if ANDROID
        appBuilder.Services.AddSingleton<IPathProvider, Platforms.Android.PathProvider>();
#elif IOS
        appBuilder.Services.AddSingleton<IPathProvider, Platforms.iOS.PathProvider>();
#elif MACCATALYST
        appBuilder.Services.AddSingleton<IPathProvider, Platforms.MacCatalyst.PathProvider>();
#elif WINDOWS 
        appBuilder.Services.AddSingleton<IPathProvider, Platforms.Windows.PathProvider>();
#endif

        serviceBuilderAction?.Invoke(appBuilder.Services);

        return appBuilder;
    }
}
