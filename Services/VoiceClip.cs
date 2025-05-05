namespace MyTts.Data
{
    public class VoiceClip : Stream, IAsyncDisposable
    {
        private readonly MemoryStream _audioDataStream;
        private bool _disposed;

        public VoiceClip(byte[] audioData)
        {
            _audioDataStream = new MemoryStream(audioData);
        }

        public override bool CanRead => _audioDataStream.CanRead;
        public override bool CanSeek => _audioDataStream.CanSeek;
        public override bool CanWrite => _audioDataStream.CanWrite;
        public override long Length => _audioDataStream.Length;

        public override long Position
        {
            get => _audioDataStream.Position;
            set => _audioDataStream.Position = value;
        }

        public override void Flush() => _audioDataStream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
            => _audioDataStream.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin)
            => _audioDataStream.Seek(offset, origin);

        public override void SetLength(long value)
            => _audioDataStream.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count)
            => _audioDataStream.Write(buffer, offset, count);

        public override async Task CopyToAsync(Stream destination, int bufferSize = 81920, CancellationToken cancellationToken = default)
            => await _audioDataStream.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _audioDataStream.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }

        public override async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                await _audioDataStream.DisposeAsync();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        public static implicit operator VoiceClip(ElevenLabs.VoiceClip voiceClip)
        {
            ArgumentNullException.ThrowIfNull(voiceClip);

            try
            {
                // Get the audio data from the ElevenLabs VoiceClip  
                var audioData = voiceClip.ClipData.ToArray();

                // Create a new VoiceClip instance with the audio data  
                return new VoiceClip(audioData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("Failed to convert ElevenLabs.VoiceClip to VoiceClip", ex);
            }
        }
    }
}
