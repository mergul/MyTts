namespace MyTts.Data
{
    public sealed class AudioProcessor : IAsyncDisposable
    {
        private readonly VoiceClip _voiceClip;
        private readonly SemaphoreSlim _semaphore;
        private bool _disposed;

        public AudioProcessor(VoiceClip voiceClip)
        {
            _voiceClip = voiceClip ?? throw new ArgumentNullException(nameof(voiceClip));
            _semaphore = new SemaphoreSlim(1);
        }

        public async Task CopyToAsync(Stream destination, CancellationToken cancellationToken = default)
        {
            await using var _ = await _semaphore.WaitAsyncDisposable(cancellationToken);
            
            _voiceClip.Position = 0;
            await _voiceClip.CopyToAsync(destination, 81920, cancellationToken);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _voiceClip.DisposeAsync();
                _semaphore.Dispose();
                _disposed = true;
            }
        }
    }
}