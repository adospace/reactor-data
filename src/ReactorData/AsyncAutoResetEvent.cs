using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactorData;

internal sealed class AsyncAutoResetEvent(bool signaled)
{
    private readonly Queue<TaskCompletionSource> _queue = new();

    private bool _signaled = signaled;

    public Task WaitAsync(CancellationToken cancellationToken = default)
    {
        lock (_queue)
        {
            if (_signaled)
            {
                _signaled = false;
                return Task.CompletedTask;
            }
            else
            {
                var tcs = new TaskCompletionSource();
                if (cancellationToken.CanBeCanceled)
                {
                    // If the token is cancelled, cancel the waiter.
                    var registration = cancellationToken.Register(() => tcs.TrySetCanceled(), useSynchronizationContext: false);

                    // If the waiter completes or faults, unregister our interest in cancellation.
                    tcs.Task.ContinueWith(
                        _ => registration.Unregister(),
                        cancellationToken,
                        TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.NotOnFaulted,
                        TaskScheduler.Default);
                }
                _queue.Enqueue(tcs);
                return tcs.Task;
            }
        }
    }

    public void Set()
    {
        TaskCompletionSource? toRelease = null;

        lock (_queue)
        {
            if (_queue.Count > 0)
            {
                toRelease = _queue.Dequeue();
            }
            else if (!_signaled)
            {
                _signaled = true;
            }
        }

        // It's possible that the TCS has already been cancelled.
        toRelease?.TrySetResult();
    }
}
