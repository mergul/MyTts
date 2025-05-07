using MyTts.Data;
using MyTts.Models;
using MyTts.Repositories;
using Microsoft.AspNetCore.Mvc;
using MyTts.Data.Interfaces;

namespace MyTts.Services
{
    public class Mp3Service : IMp3Service
    {
        private readonly ILogger<Mp3Service> _logger;
        private readonly IMp3FileRepository _mp3FileRepository;
        private readonly TtsManager _ttsManager;
        private readonly NewsFeedsService _newsFeedsService;
        private readonly IRedisCacheService? _cache;
        private const string AudioBasePath = "audio";
        private readonly SemaphoreSlim _processingSemaphore;
        private const int MaxConcurrentProcessing = 3;
        private bool _disposed;

        public Mp3Service(
            ILogger<Mp3Service> logger,
            IMp3FileRepository mp3FileRepository,
            TtsManager ttsManager,
            IRedisCacheService _cache,
            NewsFeedsService newsFeedsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _mp3FileRepository = mp3FileRepository ?? throw new ArgumentNullException(nameof(mp3FileRepository));
            _ttsManager = ttsManager ?? throw new ArgumentNullException(nameof(ttsManager));
            _cache = _cache ?? throw new ArgumentNullException(nameof(_cache));
            _newsFeedsService = newsFeedsService ?? throw new ArgumentNullException(nameof(newsFeedsService));
            _processingSemaphore = new SemaphoreSlim(MaxConcurrentProcessing);
        }
        public async Task<string> CreateMultipleMp3Async(string language, int limit, CancellationToken cancellationToken)
        {
            try
            {
                await _processingSemaphore.WaitAsync();
                var contents = await _newsFeedsService.GetFeedByLanguageAsync(language, limit);
                return await _ttsManager.ProcessContentsAsync(contents) ?? string.Empty;
            }
            finally
            {
                _processingSemaphore.Release();
            }
      }
        public async Task<IMp3> CreateSingleMp3Async(OneRequest request, CancellationToken cancellationToken)
        {
            try
            {
                await _processingSemaphore.WaitAsync();
                var content = await _newsFeedsService.GetFeedUrl(request.News);
                var filePath = await _ttsManager.ProcessContentAsync(content, Guid.NewGuid(), cancellationToken);
                return await _mp3FileRepository.LoadMp3MetaByPathAsync(filePath);
            }
            finally
            {
                _processingSemaphore.Release();
            }
        }
        public async Task<IEnumerable<IMp3>> GetFeedByLanguageAsync(ListRequest listRequest, CancellationToken cancellationToken)
        {
            var cacheKey = $"feed_{listRequest.Language}";
            return _cache != null 
                ? await _cache.GetAsync<IEnumerable<IMp3>>(cacheKey) ?? Enumerable.Empty<IMp3>()
                : Enumerable.Empty<IMp3>();
        }
        public async Task<IMp3> GetMp3FileAsync(string id, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(id);
            return await _mp3FileRepository.LoadMp3MetaByNewsIdAsync<IMp3>(id)
                ?? throw new KeyNotFoundException($"MP3 file not found for ID: {id}");
        }

        public async Task<IMp3> GetLastMp3ByLanguageAsync(string language, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(language);
            return await _mp3FileRepository.LoadLatestMp3MetaByLanguageAsync(language);
        }
        // Fix FileStream properties in all methods
        private FileStreamResult CreateFileStreamResponse(
            Stream stream,
            string fileName,
            Dictionary<string, string> headers,
            int bufferSize = 81920)
        {
            var result = new FileStreamResult(stream, "audio/mpeg")
            {
                EnableRangeProcessing = true,
                FileDownloadName = fileName
            };

            // Create a Microsoft.AspNetCore.Http.IHeaderDictionary from the result
            var responseHeaders = new HeaderDictionary();
            foreach (var header in headers)
            {
                responseHeaders[header.Key] = header.Value;
            }

            // Set the headers using the ResponseHeaders property
            foreach (var header in headers)
            {
                switch (header.Key.ToLower())
                {
                    case "content-disposition":
                        // Content-Disposition is handled by FileDownloadName
                        continue;
                    case "content-type":
                        // Content-Type is handled by the constructor
                        continue;
                    default:
                        responseHeaders.Add(header.Key, header.Value);
                        break;
                }
            }
            return result;
        }
        private ObjectResult CreateErrorResponse(Exception ex, string message)
        {
            return new ObjectResult(new { message, error = ex.Message })
            {
                StatusCode = 500
            };
        }
        private Stream CreateFileStream(string filePath, bool isStreaming = false)
        {
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: isStreaming ? 81920 : 4096,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan
            );
        }
        // Update file path handling
        private string GetAudioFilePath(string fileName)
        {
            return Path.Combine(AudioBasePath, fileName);
        }
        public async Task<IActionResult> StreamMp3(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                var mp3File = await _mp3FileRepository.GetFromCacheAsync<IMp3>($"mp3stream:{id}")
                    ?? await _mp3FileRepository.LoadAndCacheMp3File<IMp3>(id);

                if (mp3File == null)
                {
                    _logger.LogWarning("MP3 file metadata not found for ID: {Id}", id);
                    return new NotFoundObjectResult(new { message = "MP3 file not found." });
                }

                string filePath = GetAudioFilePath(mp3File.FileName);

                if (!await _mp3FileRepository.Mp3FileExistsAsync(filePath))
                {
                    _logger.LogWarning("MP3 file not found at path: {Path}", filePath);
                    return new NotFoundObjectResult(new { message = "MP3 file not found." });
                }

                var stream = CreateFileStream(filePath, isStreaming: true);
                var headers = CreateStandardHeaders(mp3File.FileName);

                return CreateFileStreamResponse(stream, mp3File.FileName, headers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error streaming MP3 for ID: {Id}", id);
                return CreateErrorResponse(ex, "An error occurred while streaming the audio.");
            }
        }
        private async Task<IMp3?> GetOrLoadMp3File(string id)
        {
            var cacheKey = $"mp3stream:{id}";
            return await _mp3FileRepository.GetFromCacheAsync<IMp3>(cacheKey)
                ?? await _mp3FileRepository.LoadAndCacheMp3File<IMp3>(id);
        }
        private async Task<(byte[] FileData, string LocalPath)> GetOrProcessMp3File(string id, CancellationToken cancellationToken)
        {
            var fileData = await _mp3FileRepository.LoadMp3FileAsync(id);
            var localPath = id;

            if (fileData == null || fileData.Length == 0)
            {
                var content = _newsFeedsService.GetFeedUrl(id);
                localPath = await _ttsManager.ProcessContentAsync(content, Guid.NewGuid(), cancellationToken);
                fileData = await _mp3FileRepository.LoadMp3FileAsync(localPath);
            }

            return (fileData, localPath);
        }

        private IActionResult CreateStreamResponse(string filePath, string fileName)
        {
            var stream = CreateFileStream(filePath, isStreaming: true);
            var headers = CreateStandardHeaders(fileName);
            return CreateFileStreamResponse(stream, fileName, headers);
        }

        private IActionResult CreateDownloadResponse(byte[] fileData, string localPath)
        {
            return new FileContentResult(fileData, "audio/mpeg")
            {
                FileDownloadName = Path.GetFileName(localPath),
                EnableRangeProcessing = true
            };
        }
        private NotFoundObjectResult CreateNotFoundResponse(string message)
        {
            return new NotFoundObjectResult(new { message });
        }
        /// <summary>
        /// Download MP3 file by ID, if not found, process the content and save it.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<IActionResult> DownloadMp3(string id, CancellationToken cancellationToken)
        {
            try
            {
                var (fileData, localPath) = await GetOrProcessMp3File(id, cancellationToken);
                return CreateDownloadResponse(fileData, localPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MP3 download for ID: {Id}", id);
                return CreateErrorResponse(ex, "An error occurred while processing the request.");
            }
        }
        // public async Task<IActionResult> DownloadMp3(string id, CancellationToken cancellationToken)
        // {
        //     try
        //     {
        //         string localPath=""+id;
        //         byte[] fileData;
        //         fileData = await _mp3FileRepository.LoadMp3FileAsync(id);
        //         if (fileData == null || fileData.Length == 0)
        //         {
        //             var content =  _newsFeedsService.GetFeedUrl("id");
        //             localPath = await _ttsManager.ProcessContentAsync(content, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
        //             fileData = await _mp3FileRepository.LoadMp3FileAsync(localPath);

        //         }
        //         // Set response headers
        //         var headers = new Dictionary<string, string>
        //         {
        //             { "Content-Disposition", $"attachment; filename=\"{Path.GetFileName(localPath)}\"" },
        //             { "Cache-Control", "public, max-age=3600" },
        //             { "Accept-Ranges", "bytes" }
        //         };

        //         // Return file result with proper headers
        //         return new FileContentResult(fileData, "audio/mpeg")
        //         {
        //             FileDownloadName = Path.GetFileName(localPath),
        //             EnableRangeProcessing = true
        //         };

        //     }
        //     catch (Exception ex)
        //     {
        //         _logger.LogError(ex, "Error processing MP3 download for ID: {Id}", id);
        //         return new ObjectResult(new
        //         {
        //             message = "An error occurred while processing the request.",
        //             error = ex.Message
        //         })
        //         {
        //             StatusCode = 500
        //         };
        //     }
        // }
        public async Task<IActionResult> DownloadMp3FromDisk(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                string cacheKey = $"mp3disk:{id}";
                var cachedPath = _cache != null ? await _cache.GetAsync<string>(cacheKey) : null;

                string filePath;
                if (cachedPath != null)
                {
                    filePath = cachedPath;
                }
                else
                {
                    // Get file metadata from repository
                    var mp3File = await _mp3FileRepository.LoadMp3MetaByNewsIdAsync<IMp3>(id);
                    if (mp3File == null)
                    {
                        _logger.LogWarning("MP3 file metadata not found for ID: {Id}", id);
                        return new NotFoundObjectResult(new { message = "MP3 file not found." });
                    }

                    filePath = Path.Combine(TtsManager.LocalSavePath, mp3File.FileName);

                    // Cache the path for future requests
                    await _cache!.SetAsync(cacheKey, filePath, TimeSpan.FromHours(1));
                }

                // Check if file exists
                if (!await _mp3FileRepository.Mp3FileExistsAsync(filePath))
                {
                    _logger.LogWarning("MP3 file not found at path: {Path}", filePath);
                    return new NotFoundObjectResult(new { message = "MP3 file not found." });
                }

                // Set response headers for better caching and streaming
                var headers = new Dictionary<string, string>
                {
                    { "Content-Disposition", $"attachment; filename=\"{Path.GetFileName(filePath)}\"" },
                    { "Cache-Control", "public, max-age=3600" },
                    { "Accept-Ranges", "bytes" }
                };

                // Create FileStreamResult with proper settings
                var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096, // Default buffer size
                    FileOptions.Asynchronous | FileOptions.SequentialScan
                );

                return new FileStreamResult(stream, "audio/mpeg")
                {
                    FileDownloadName = Path.GetFileName(filePath),
                    EnableRangeProcessing = true // Enables partial content requests
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error downloading MP3 file from disk for ID: {Id}", id);
                return new ObjectResult(new
                {
                    message = "An error occurred while processing the request.",
                    error = ex.Message
                })
                {
                    StatusCode = 500
                };
            }
        }
        private Dictionary<string, string> CreateStandardHeaders(string fileName)
        {
            return new Dictionary<string, string>
            {
                // Content-Disposition is handled by FileDownloadName
                { "Cache-Control", "public, max-age=3600" },
                { "Accept-Ranges", "bytes" },
                // Content-Type is handled by FileStreamResult constructor
                { "X-Content-Type-Options", "nosniff" }
            };
        }
        public async ValueTask DisposeAsync()
        {
            if (!_disposed)
            {
                _processingSemaphore.Dispose();
                _disposed = true;
            }
            await Task.CompletedTask;
        }

        public async Task<IEnumerable<IMp3>> GetMp3FileListAsync(CancellationToken cancellationToken)
        {
            // return await _mp3FileRepository.LoadAllMp3MetaAsync();
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<IMp3>> GetMp3FileListByLanguageAsync(string language, CancellationToken cancellationToken)
        {
            ArgumentException.ThrowIfNullOrEmpty(language);
            // return await _mp3FileRepository.LoadMp3MetaByLanguageAsync(language);
            throw new NotImplementedException();
        }
    }
}
