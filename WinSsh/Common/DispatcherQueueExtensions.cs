using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;

namespace WinSsh.Common;

public static class DispatcherQueueExtensions
{
    public static Task EnqueueAsync(this DispatcherQueue queue, Func<Task> action)
    {
        var tcs = new TaskCompletionSource();

        queue.TryEnqueue(async () =>
        {
            try
            {
                await action();
                tcs.SetResult();
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }

    public static Task<T> EnqueueAsync<T>(this DispatcherQueue queue, Func<Task<T>> action)
    {
        var tcs = new TaskCompletionSource<T>();

        queue.TryEnqueue(async () =>
        {
            try
            {
                var result = await action();
                tcs.SetResult(result);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });

        return tcs.Task;
    }
}
