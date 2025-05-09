using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElevenLabs;
using ElevenLabs.Models;
using ElevenLabs.TextToSpeech;
using ElevenLabs.Voices;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Options;
using MyTts.Config;
using MyTts.Services;
using MyTts.Storage;
using StackExchange.Redis;

namespace MyTts.Data
{
    public class TtsManager
    {
        private readonly ElevenLabsClient _elevenLabsClient;
        private readonly StorageClient _storageClient;
        private readonly IRedisCacheService? _cache;
        private readonly IOptions<ElevenLabsConfig> _config;
        private readonly ILogger<TtsManager> _logger;
        public const string LocalSavePath = "audio";
        private readonly SemaphoreSlim _semaphore;
        private const int MaxConcurrentOperations = 3;
        private readonly StorageConfiguration _storageConfig;
        private readonly string _bucketName;

        public TtsManager(
            ElevenLabsClient elevenLabsClient,
            IOptions<ElevenLabsConfig> config,
            IOptions<StorageConfiguration> storageConfig,
            IRedisCacheService cache,
            ILogger<TtsManager> logger)
        {
            _elevenLabsClient = elevenLabsClient ?? throw new ArgumentNullException(nameof(elevenLabsClient));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _storageClient = StorageClient.Create();
            _semaphore = new SemaphoreSlim(MaxConcurrentOperations);
            _storageConfig = storageConfig.Value;
            // Initialize Google Cloud Storage
            (_storageClient, _bucketName) = InitializeGoogleCloudStorage(_storageConfig);


            Directory.CreateDirectory(LocalSavePath);
        }
        private (StorageClient client, string bucketName) InitializeGoogleCloudStorage(StorageConfiguration config)
        {
            try
            {
                // Get Google Cloud configuration  
                if (!config.Disks.TryGetValue("GoogleCloud", out var googleCloudDisk))
                {
                    throw new InvalidOperationException("GoogleCloud disk configuration not found.");
                }

                // Ensure Config is not null
                if (googleCloudDisk.Config == null)
                {
                    throw new InvalidOperationException("GoogleCloud disk configuration is null.");
                }

                // Handle bucket name
                string? bucketName = null;
                if (!googleCloudDisk.Config.TryGetValue("BucketName", out bucketName) || string.IsNullOrEmpty(bucketName))
                {
                    throw new InvalidOperationException("BucketName not configured for GoogleCloud disk.");
                }

                // Create StorageClient with builder for more control  
                var clientBuilder = new StorageClientBuilder();

                // If AuthJson is provided in config, use it  
                if (googleCloudDisk.Config.TryGetValue("AuthJson", out var authJson))
                {
                    clientBuilder.CredentialsPath = authJson;
                }

                var client = clientBuilder.Build();

                // Verify bucket exists and is accessible  
                try
                {
                    client.GetBucket(bucketName);
                    _logger.LogInformation("Successfully connected to Google Cloud Storage bucket: {BucketName}", bucketName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to access Google Cloud Storage bucket: {BucketName}", bucketName);
                    throw new InvalidOperationException($"Cannot access bucket: {bucketName}", ex);
                }

                return (client, bucketName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Google Cloud Storage client");
                throw;
            }
        }
        public async Task<string?> ProcessContentsAsync(IEnumerable<string> contents, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(contents);
           // ArgumentException.ThrowIfNullOrEmpty(bucketName);
            var processedFiles = new List<string>();
            try
            {
                var tasks = contents.Select(async content =>
                {
                    await using var _ = await _semaphore.WaitAsyncDisposable(cancellationToken);
                    var filePath = await ProcessContentAsync(content, Guid.NewGuid(), cancellationToken);
                    processedFiles.Add(filePath);
                });

                await Task.WhenAll(tasks);
                string? mergedPath = null;
                if (processedFiles.Count > 1)
                {
                    var mergedFileName = $"merged_{DateTime.UtcNow:yyyyMMddHHmmss}.mp3";
                    mergedPath = await MergeContentsAsync(
                        processedFiles,
                        mergedFileName,
                        breakAudioPath: Path.Combine(LocalSavePath, "break.mp3"),
                        headerAudioPath: Path.Combine(LocalSavePath, "header.mp3"),
                        insertBreakEvery: 2,
                        cancellationToken);
                }
                _logger.LogInformation(
                            "Processed {Count} files. Merged file created: {MergedCreated}",
                            processedFiles.Count,
                            mergedPath != null);
                return mergedPath;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Content processing was cancelled");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing contents batch");
                throw;
            }
        }

        public async Task<string> ProcessContentAsync(string text, Guid id, CancellationToken cancellationToken)
        {
            var fileName = $"speech_{id}.mp3";
            var localPath = Path.Combine(LocalSavePath, fileName);

            try
            {
                // Generate audio using ElevenLabs API
                var voice = await _elevenLabsClient.VoicesEndpoint
                    .GetVoiceAsync(_config.Value.VoiceId, false, cancellationToken);

                var request = await CreateTtsRequestAsync(voice, text);

                // Process audio and upload
                await using VoiceClip voiceClip = await _elevenLabsClient.TextToSpeechEndpoint
                    .TextToSpeechAsync(request, null, cancellationToken);

                await using var audioProcessor = new AudioProcessor(voiceClip);

                // Parallel operations for saving and uploading
                await Task.WhenAll(
                    // Save locally
                    SaveLocallyAsync(audioProcessor, localPath, cancellationToken),
                    // Save metadata to SQL
                    SaveMetadataSqlAsync(id, text, localPath, fileName, cancellationToken),
                    // Upload to Google Cloud Storage
                    UploadToCloudAsync(audioProcessor, fileName, cancellationToken),
                    // Store metadata in Redis
                    StoreMetadataRedisAsync(id, text, localPath, fileName, cancellationToken)
                );

                _logger.LogInformation("Processed content {Id}: {FileName}", id, fileName);
                return localPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content {Id}", id);
                throw;
            }
        }

        private Task<TextToSpeechRequest> CreateTtsRequestAsync(Voice voice, string text)
        {
            var voiceSettings = new VoiceSettings
            {
                Stability = _config.Value.Stability,
                SimilarityBoost = _config.Value.Similarity,
                Style = _config.Value.Style,
                SpeakerBoost = _config.Value.Boost
            };

            var request = new TextToSpeechRequest(
                voice,
                text,
                Encoding.UTF8,
                voiceSettings,
                OutputFormat.MP3_44100_128,
                new Model(_config.Value.Model)
            );

            return Task.FromResult(request);
        }

        private async Task SaveLocallyAsync(AudioProcessor processor, string localPath, CancellationToken cancellationToken)
        {
            await using var fileStream = File.Create(localPath);
            await processor.CopyToAsync(fileStream, cancellationToken);
        }

        private async Task UploadToCloudAsync(AudioProcessor processor, string fileName, CancellationToken cancellationToken)
        {
            //await using var memoryStream = new MemoryStream();
            //await processor.CopyToAsync(memoryStream, cancellationToken);
            //memoryStream.Position = 0;
            using var uploadStream = await processor.GetStreamForCloudUpload(cancellationToken);

            await _storageClient.UploadObjectAsync(
                _bucketName,
                fileName,
                "audio/mpeg",
                uploadStream,
                cancellationToken: cancellationToken);
        }

        private async Task StoreMetadataRedisAsync(Guid id, string text, string localPath, string fileName, CancellationToken cancellationToken)
        {
            var metadata = new AudioMetadata
            {
                Id = id,
                Text = text,
                LocalPath = localPath,
                GcsPath = $"gs://{_bucketName}/{fileName}",
                Timestamp = DateTime.UtcNow
            };

            await _cache!.SetAsync<AudioMetadata>($"tts:{id}", metadata, TimeSpan.FromHours(1));
                
        }

        private Task SaveMetadataSqlAsync(Guid id, string text, string localPath, string fileName, CancellationToken cancellationToken)
        {
            // TODO: Implement database operations
            
            return Task.CompletedTask;
        }

        public async Task<string> MergeContentsAsync(
            IEnumerable<string> audioFiles,
            string outputFileName,
            string? breakAudioPath = null,
            string? headerAudioPath = null,
            int insertBreakEvery = 0,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(audioFiles);
            ArgumentException.ThrowIfNullOrEmpty(outputFileName);

            var outputPath = Path.Combine(LocalSavePath, outputFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? string.Empty);

            try
            {
                var fileList = audioFiles.ToList();

                if (!string.IsNullOrEmpty(breakAudioPath))
                {
                    fileList = InsertBreakFile(fileList, breakAudioPath, insertBreakEvery);
                }

                if (!string.IsNullOrEmpty(headerAudioPath))
                {
                    fileList.Insert(0, headerAudioPath);
                }

                var success = await MergeFilesWithFfmpeg(fileList, outputPath, cancellationToken);

                if (!success)
                {
                    throw new InvalidOperationException("Failed to merge audio files");
                }

                return outputPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging audio files to: {OutputPath}", outputPath);
                throw;
            }
        }

        private List<string> InsertBreakFile(List<string> files, string breakFile, int insertEvery)
        {
            if (insertEvery <= 0) return files;

            var result = new List<string>();
            for (int i = 0; i < files.Count; i++)
            {
                result.Add(files[i]);
                if ((i + 1) % insertEvery == 0 && i < files.Count - 1)
                {
                    result.Add(breakFile);
                }
            }
            return result;
        }

        private async Task<bool> MergeFilesWithFfmpeg(
            List<string> files,
            string outputFilePath,
            CancellationToken cancellationToken)
        {
            try
            {
                foreach (var file in files)
                {
                    if (!await ValidateAudioStream(file))
                    {
                        _logger.LogError("Invalid or no audio stream found in file: {File}", file);
                        return false;
                    }
                }

                var tempFileList = Path.GetTempFileName();
                var fileListContent = string.Join("\n", files.Select(f => $"file '{f}'"));
                await File.WriteAllTextAsync(tempFileList, fileListContent, cancellationToken);

                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg",
                        Arguments = $"-f concat -safe 0 -i \"{tempFileList}\" -c copy \"{outputFilePath}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();

                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync(cancellationToken);

                var output = await outputTask;
                var error = await errorTask;

                _logger.LogInformation("FFmpeg Output: {Output}", output);
                if (!string.IsNullOrEmpty(error))
                {
                    _logger.LogError("FFmpeg Error: {Error}", error);
                }

                File.Delete(tempFileList);
                return process.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error merging audio files with FFmpeg");
                throw;
            }
        }

        private async Task<bool> ValidateAudioStream(string filePath)
        {
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffprobe",
                        Arguments = $"-i \"{filePath}\" -show_streams -select_streams a -of json",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0) return false;

                var streamInfo = JsonSerializer.Deserialize<FFprobeOutput>(output);
                return streamInfo?.Streams?.Any(s => s.CodecType == "audio") ?? false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate audio stream for file: {File}", filePath);
                return false;
            }
        }
        
        private record FFprobeOutput
        {
            public List<StreamInfo>? Streams { get; init; }
        }
        private class StreamInfo
        {
            [JsonPropertyName("codec_type")]
            public string? CodecType { get; set; }
        }

        private record AudioMetadata
        {
            public required Guid Id { get; init; }
            public required string Text { get; init; }
            public required string LocalPath { get; init; }
            public required string GcsPath { get; init; }
            public required DateTime Timestamp { get; init; }
        }
        // Update the DisposeAsync method to use Dispose instead of DisposeAsync for StorageClient
        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_storageClient != null)
                {
                    // StorageClient does not support DisposeAsync, so use Dispose
                    await Task.Run(() => _storageClient.Dispose());
                }
                _semaphore?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing TtsManager resources");
            }
        }
        /*
        private async Task ProcessContentAsync(string text, Guid id, string bucketName, CancellationToken cancellationToken)
        {
            var fileName = $"speech_{id}.mp3";
            var localPath = Path.Combine(LocalSavePath, fileName);
            var tempFiles = new List<string>();

            try
            {
                // Split text into chunks if needed (e.g., by sentences or paragraphs)
                var textChunks = SplitTextIntoChunks(text);
                var chunkFiles = new List<string>();

                // Process each chunk
                foreach (var chunk in textChunks)
                {
                    var chunkFile = await ProcessChunkAsync(chunk, id, cancellationToken);
                    if (chunkFile != null)
                    {
                        chunkFiles.Add(chunkFile);
                        tempFiles.Add(chunkFile);
                    }
                }

                // Merge chunks if there are multiple
                if (chunkFiles.Count > 1)
                {
                    localPath = await MergeContentsAsync(
                        chunkFiles,
                        fileName,
                        breakAudioPath: Path.Combine(LocalSavePath, "break.mp3"),
                        headerAudioPath: Path.Combine(LocalSavePath, "header.mp3"),
                        insertBreakEvery: 2,
                        cancellationToken
                    );
                }
                else if (chunkFiles.Count == 1)
                {
                    localPath = chunkFiles[0];
                }

                // Upload final file and store metadata
                await Task.WhenAll(
                    UploadFinalToCloudAsync(localPath, bucketName, fileName, cancellationToken),
                    StoreMetadataAsync(id, text, localPath, bucketName, fileName, cancellationToken)
                );

                _logger.LogInformation("Processed and merged content {Id}: {FileName}", id, fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content {Id}", id);
                throw;
            }
            finally
            {
                // Cleanup temporary files
                foreach (var tempFile in tempFiles)
                {
                    try
                    {
                        if (File.Exists(tempFile) && tempFile != localPath)
                        {
                            File.Delete(tempFile);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {File}", tempFile);
                    }
                }
            }
        }

        private IEnumerable<string> SplitTextIntoChunks(string text, int maxChunkLength = 2000)
        {
            var sentences = text.Split(new[] { ". ", "! ", "? " }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim() + ".");

            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length + sentence.Length > maxChunkLength)
                {
                    if (currentChunk.Length > 0)
                    {
                        yield return currentChunk.ToString();
                        currentChunk.Clear();
                    }
                    if (sentence.Length > maxChunkLength)
                    {
                        // Handle very long sentences by splitting them into words
                        var words = sentence.Split(' ');
                        foreach (var chunk in SplitIntoChunks(words, maxChunkLength))
                        {
                            yield return chunk;
                        }
                        continue;
                    }
                }
                if (currentChunk.Length > 0)
                {
                    currentChunk.Append(" ");
                }
                currentChunk.Append(sentence);
            }

            if (currentChunk.Length > 0)
            {
                yield return currentChunk.ToString();
            }
        }

        private IEnumerable<string> SplitIntoChunks(string[] words, int maxLength)
        {
            var chunk = new StringBuilder();

            foreach (var word in words)
            {
                if (chunk.Length + word.Length + 1 > maxLength && chunk.Length > 0)
                {
                    yield return chunk.ToString();
                    chunk.Clear();
                }
                if (chunk.Length > 0)
                {
                    chunk.Append(" ");
                }
                chunk.Append(word);
            }

            if (chunk.Length > 0)
            {
                yield return chunk.ToString();
            }
        }

        private async Task<string?> ProcessChunkAsync(string chunk, Guid id, CancellationToken cancellationToken)
        {
            var chunkFileName = $"chunk_{Guid.NewGuid()}_{id}.mp3";
            var chunkPath = Path.Combine(TempPath, chunkFileName);

            try
            {
                var voice = await _elevenLabsClient.VoicesEndpoint
                    .GetVoiceAsync(_config.Value.VoiceId, false, cancellationToken);

                var request = await CreateTtsRequestAsync(voice, chunk);

                await using var voiceClip = await _elevenLabsClient.TextToSpeechEndpoint
                    .TextToSpeechAsync(request, null, cancellationToken);

                await using var audioProcessor = new AudioProcessor(voiceClip);
                await using var fileStream = File.Create(chunkPath);
                await audioProcessor.CopyToAsync(fileStream, cancellationToken);

                return chunkPath;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chunk for content {Id}", id);
                return null;
            }
        }

        private async Task UploadFinalToCloudAsync(string localPath, string bucketName, string fileName, CancellationToken cancellationToken)
        {
            await using var fileStream = File.OpenRead(localPath);
            await _storageClient.UploadObjectAsync(
                bucketName,
                fileName,
                "audio/mpeg",
                fileStream,
                cancellationToken: cancellationToken);
        }
        */

    }
}