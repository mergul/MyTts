namespace MyTts.Data
{
    public static class SemaphoreExtensions
    {
        public static async Task<IAsyncDisposable> WaitAsyncDisposable(
            this SemaphoreSlim semaphore,
            CancellationToken cancellationToken = default)
        {
            await semaphore.WaitAsync(cancellationToken);
            return new SemaphoreReleaser(semaphore);
        }

        private sealed class SemaphoreReleaser : IAsyncDisposable
        {
            private readonly SemaphoreSlim _semaphore;
            private bool _disposed;

            public SemaphoreReleaser(SemaphoreSlim semaphore)
            {
                _semaphore = semaphore;
            }

            public ValueTask DisposeAsync()
            {
                if (!_disposed)
                {
                    _semaphore.Release();
                    _disposed = true;
                }
                return ValueTask.CompletedTask;
            }
        }
    }
}