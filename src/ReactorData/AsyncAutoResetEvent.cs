using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ReactorData;

internal sealed class AsyncAutoResetEvent
{
    private static readonly Task _completed = Task.FromResult(true);
    private readonly Queue<TaskCompletionSource<bool>> _waits = new Queue<TaskCompletionSource<bool>>();
    private bool _signaled;

    public Task WaitAsync()
    {
        lock (_waits)
        {
            if (_signaled)
            {
                _signaled = false;
                return _completed;
            }
            else
            {
                var tcs = new TaskCompletionSource<bool>();
                _waits.Enqueue(tcs);
                return tcs.Task;
            }
        }
    }

    public void Set()
    {
        TaskCompletionSource<bool>? toRelease = null;

        lock (_waits)
        {
            if (_waits.Count > 0)
            {
                toRelease = _waits.Dequeue();
            }
            else if (!_signaled)
            {
                _signaled = true;
            }
        }

        toRelease?.SetResult(true);
    }
}