using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData.Maui;

internal class Dispatcher(Action<Exception>? exceptionCallBack) : IDispatcher
{
    private readonly Action<Exception>? _exceptionCallBack = exceptionCallBack;

    public void Dispatch(Action action)
    {
        if (Microsoft.Maui.Controls.Application.Current == null)
        {
            return;
        }

        if (Microsoft.Maui.Controls.Application.Current.Dispatcher.IsDispatchRequired == true)
        {
            Microsoft.Maui.Controls.Application.Current.Dispatcher.Dispatch(action);
        }
        else
        {
            action();
        }
    }

    public void OnError(Exception exception)
    {
        _exceptionCallBack?.Invoke(exception);
    }
}
