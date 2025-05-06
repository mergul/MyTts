using MyTts.Data;
using MyTts.Models;
using MyTts.Repositories;
using Microsoft.AspNetCore.Mvc;

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
        public Mp3Service(
            ILogger<Mp3Service> logger,
            TtsManager ttsManager,
            IRedisCacheService _cache,
            NewsFeedsService newsFeedsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _ttsManager = ttsManager ?? throw new ArgumentNullException(nameof(ttsManager));
            _cache = _cache ?? throw new ArgumentNullException(nameof(_cache));
            _newsFeedsService = newsFeedsService ?? throw new ArgumentNullException(nameof(newsFeedsService));
        }
        public async Task<string?> CreateMultipleMp3Async(ListRequest listRequest)
        {
            List<string> contents = await _newsFeedsService.GetFeedByLanguageAsync("listRequestUrl", 20);
            return await _ttsManager.ProcessContentsAsync(contents);
        }
        public async Task<Mp3File> CreateSingleMp3Async(OneRequest request)
        {
            throw new NotImplementedException("This method is not implemented yet.");
        }
        public async Task<IEnumerable<Mp3File>> GetFeedByLanguageAsync(string language, int limit)
        {
            if (_cache == null)
            {
                throw new InvalidOperationException("Cache service is not initialized.");
            }

            var result = await _cache.GetAsync<IEnumerable<Mp3File>>($"feed_{language}_{limit}");
            return result ?? Enumerable.Empty<Mp3File>();
        }
        public async Task<Mp3File> GetMp3FileAsync(string id)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            return await _mp3FileRepository.LoadMp3MetaByNewsIdAsync(id)
                ?? throw new KeyNotFoundException($"MP3 file not found for ID: {id}");
        }

        public async Task<Mp3File> GetLastMp3ByLanguageAsync(string language)
        {
            if (string.IsNullOrEmpty(language)) throw new ArgumentNullException(nameof(language));
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
            return new ObjectResult(new
            {
                message = message,
                error = ex.Message
            })
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
                var mp3File = await _mp3FileRepository.GetFromCacheAsync<Mp3File>($"mp3stream:{id}")
                    ?? await _mp3FileRepository.LoadAndCacheMp3File(id);

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
                string localPath=""+id;
                byte[] fileData;
                fileData = await _mp3FileRepository.LoadMp3FileAsync(id);
                if (fileData == null || fileData.Length == 0)
                {
                    var content =  _newsFeedsService.GetFeedUrl("id");
                    localPath = await _ttsManager.ProcessContentAsync(content, Guid.NewGuid(), cancellationToken).ConfigureAwait(false);
                    fileData = await _mp3FileRepository.LoadMp3FileAsync(localPath);

                }
                // Set response headers
                var headers = new Dictionary<string, string>
                {
                    { "Content-Disposition", $"attachment; filename=\"{Path.GetFileName(localPath)}\"" },
                    { "Cache-Control", "public, max-age=3600" },
                    { "Accept-Ranges", "bytes" }
                };

                // Return file result with proper headers
                return new FileContentResult(fileData, "audio/mpeg")
                {
                    FileDownloadName = Path.GetFileName(localPath),
                    EnableRangeProcessing = true
                };

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing MP3 download for ID: {Id}", id);
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
        public async Task<IActionResult> DownloadMp3FromDisk(string id, CancellationToken cancellationToken = default)
        {
            try
            {
                // Check cache first
                string cacheKey = $"mp3disk:{id}";
                var cachedPath = await _cache.GetAsync<string>(cacheKey);

                string filePath;
                if (cachedPath != null)
                {
                    filePath = cachedPath;
                }
                else
                {
                    // Get file metadata from repository
                    var mp3File = await _mp3FileRepository.LoadMp3MetaByNewsIdAsync(id);
                    if (mp3File == null)
                    {
                        _logger.LogWarning("MP3 file metadata not found for ID: {Id}", id);
                        return new NotFoundObjectResult(new { message = "MP3 file not found." });
                    }

                    filePath = Path.Combine(TtsManager.LocalSavePath, mp3File.FileName);

                    // Cache the path for future requests
                    await _cache.SetAsync(cacheKey, filePath, TimeSpan.FromHours(1));
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

    }
}
