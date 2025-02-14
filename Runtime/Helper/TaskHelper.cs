using System;
using System.Threading;
using System.Threading.Tasks;

namespace Devolfer.Soma
{
    internal static class TaskHelper
    {
        internal static async Task WaitWhile(Func<bool> waitWhilePredicate,
                                             CancellationToken cancellationToken = default)
        {
            while (waitWhilePredicate())
            {
                if (cancellationToken.IsCancellationRequested) return;

                await Task.Yield();
            }
        }

        internal static CancellationTokenSource Link(ref CancellationToken externalCancellationToken,
                                                     ref CancellationTokenSource cancellationTokenSource)
        {
            return cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                externalCancellationToken,
                cancellationTokenSource.Token);
        }

        internal static CancellationToken CancelAndRefresh(ref CancellationTokenSource cancellationTokenSource)
        {
            Cancel(ref cancellationTokenSource);

            cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Token.ThrowIfCancellationRequested();

            return cancellationTokenSource.Token;
        }

        internal static void Cancel(ref CancellationTokenSource cancellationTokenSource)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
            cancellationTokenSource = null;
        }
    }
}