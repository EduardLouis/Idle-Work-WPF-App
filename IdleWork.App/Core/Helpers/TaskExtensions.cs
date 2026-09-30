// [v0.004: TaskExtensions] Safe fire-and-forget extension method with centralized error logging
using System;
using System.Threading.Tasks;
using IdleWork.App.Core.Services;

namespace IdleWork.App.Core.Helpers
{
    public static class TaskExtensions
    {
        public static void SafeFireAndForget(this Task task, string context, Action<Exception>? onError = null)
        {
            if (task == null) return;

            task.ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                {
                    var ex = t.Exception.Flatten().InnerException ?? t.Exception;
                    LoggingService.Instance.LogError($"[FireAndForget:{context}]", ex.Message, ex);
                    onError?.Invoke(ex);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);
        }
    }
}
