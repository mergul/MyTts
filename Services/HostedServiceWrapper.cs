using MyTts.Data;

namespace MyTts.Services
{
    internal class HostedServiceWrapper : IHostedService
    {
        private readonly TtsManager _ttsManager;

        public HostedServiceWrapper(TtsManager ttsManager)
        {
            _ttsManager = ttsManager;
        }

        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _ttsManager.DisposeAsync();
        }
    }
}
