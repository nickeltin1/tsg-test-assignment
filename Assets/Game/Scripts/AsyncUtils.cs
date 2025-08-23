using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Game.Scripts
{
    public static class AsyncUtils
    {
        public static async Task AwaitWithProgress(AsyncOperationHandle handle, Action<float> report, CancellationToken ct)
        {
            report?.Invoke(0f);
            while (!handle.IsDone)
            {
                ct.ThrowIfCancellationRequested();
                report?.Invoke(handle.PercentComplete);
                await Task.Yield();
            }
            report?.Invoke(1f);
            if (handle.Status == AsyncOperationStatus.Failed) throw handle.OperationException;
        }
    }
}