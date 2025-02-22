using System.Threading;

namespace Devolfer.Soma
{
    internal static class TokenHelper
    {
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