using System.Collections.Concurrent;
using Newtonsoft.Json;
using MyTts.Data.Interfaces;
using MyTts.Services;
using MyTts.Data.Repositories;

namespace MyTts.Repositories
{
    public class Mp3FileRepository : IMp3FileRepository
    {
        private readonly ILogger<Mp3FileRepository> _logger;
        private readonly string _baseStoragePath;
        private readonly string _metadataPath;
        private readonly IRedisCacheService _cache;
        private readonly SemaphoreSlim _dbLock;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _fileLocks;
        private readonly JsonSerializerSettings _jsonSettings;
        private bool _disposed;
        private readonly Mp3MetaRepository _mp3MetaRepository;

        private const string DB_CACHE_KEY = "MP3_FILES_DB";
        private const string FILE_CACHE_KEY_PREFIX = "MP3_FILE_";
        private static readonly TimeSpan DB_CACHE_DURATION = TimeSpan.FromMinutes(5);
        private static readonly TimeSpan FILE_CACHE_DURATION = TimeSpan.FromMinutes(30);

        public Mp3FileRepository(
            ILogger<Mp3FileRepository> logger,
            IConfiguration configuration,
            IRedisCacheService cache,
            Mp3MetaRepository mp3MetaRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _baseStoragePath = configuration["Storage:BasePath"] ?? "storage/mp3";
            _metadataPath = configuration["Storage:MetadataPath"] ?? "storage/meta/mp3files.json";
            _dbLock = new SemaphoreSlim(1, 1);
            _fileLocks = new ConcurrentDictionary<string, SemaphoreSlim>();
            _jsonSettings = new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                NullValueHandling = NullValueHandling.Ignore
            };
            _mp3MetaRepository = mp3MetaRepository ?? throw new ArgumentNullException(nameof(mp3MetaRepository));
            // Initialize directories asynchronously
            InitializeDirectoriesAsync().GetAwaiter().GetResult();
        }
        
        public async Task<bool> Mp3FileExistsInCacheAsync(string cacheKey)
        {
            ArgumentNullException.ThrowIfNull(cacheKey);
            try
            {
                var existsInCache = await _cache.GetAsync<bool?>(cacheKey);
                return existsInCache.Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking file existence at {CacheKey}", cacheKey);
                throw;
            }
        }
        public async Task<bool> Mp3FileExistsInSqlAsync(int id)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(id, 1);

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    var exists = await _mp3MetaRepository.ExistByIdAsync(id);
                    if (!exists)
                        throw new KeyNotFoundException($"MP3 file not found for path: {id}");

                    _logger.LogDebug("Found MP3 metadata for path: {id}", id);
                    return true;
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Failed to load MP3 metadata for path: {id}", id);
                throw;
            }
        }
        public async Task<bool> Mp3FileExistsAsync(string filePath)
        {
            string fullPath = Path.Combine(_baseStoragePath, filePath);
            bool exists = await Task.Run(() => File.Exists(fullPath));
            if(exists) await _cache.SetAsync(fullPath, exists, TimeSpan.FromMinutes(24 * 60));
            return exists;
        }
        /// <summary>
        /// Loads an MP3 file from the specified path. If the file is not found, it returns an empty byte array.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public async Task<byte[]> LoadMp3FileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            string cacheKey = $"{FILE_CACHE_KEY_PREFIX}{filePath}";
            if (await Mp3FileExistsInCacheAsync(cacheKey) || await Mp3FileExistsInSqlAsync(22) || await Mp3FileExistsAsync(filePath))
            {
                _logger.LogDebug("Cache hit for file: {FilePath}", filePath);

                var fileLock = await GetFileLockAsync(filePath);
                await fileLock.WaitAsync();
                try
                {
                    string fullPath = GetFullPath(filePath);
                    var fileData = await File.ReadAllBytesAsync(fullPath);
                    return fileData;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to get MP3 file: {FilePath}", filePath);
                    throw;
                }
                finally
                {
                    fileLock.Release();
                }
            }
            return Array.Empty<byte>();
        }

        public async Task SaveMp3FileAsync(string filePath, byte[] fileData)
        {
            ArgumentNullException.ThrowIfNull(filePath);
            ArgumentNullException.ThrowIfNull(fileData);

            var fileLock = await GetFileLockAsync(filePath);
            await fileLock.WaitAsync();
            try
            {
                string fullPath = GetFullPath(filePath);
                string directory = Path.GetDirectoryName(fullPath)!;
                Directory.CreateDirectory(directory);

                string tempPath = $"{fullPath}.tmp";
                await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
                {
                    await fileStream.WriteAsync(fileData);
                }

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                }
                File.Move(tempPath, fullPath);

                // Update cache
                string cacheKey = $"{FILE_CACHE_KEY_PREFIX}{filePath}";
                await _cache.SetAsync(cacheKey, fileData, FILE_CACHE_DURATION);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save MP3 file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                fileLock.Release();
            }
        }

        public async Task DeleteMp3FileAsync(string filePath)
        {
            ArgumentNullException.ThrowIfNull(filePath);

            var fileLock = await GetFileLockAsync(filePath);
            await fileLock.WaitAsync();
            try
            {
                string fullPath = GetFullPath(filePath);
                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    string cacheKey = $"{FILE_CACHE_KEY_PREFIX}{filePath}";
                    await _cache.RemoveAsync(cacheKey);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete MP3 file: {FilePath}", filePath);
                throw;
            }
            finally
            {
                fileLock.Release();
            }
        }

        private Task<SemaphoreSlim> GetFileLockAsync(string filePath)
        {
            return Task.FromResult(_fileLocks.GetOrAdd(filePath, _ => new SemaphoreSlim(1, 1)));
        }

        public async Task<List<IMp3>> LoadListMp3MetadatasAsync()
        {
            // Try get from cache first
            var cachedData = await _cache.GetAsync<List<IMp3>>(DB_CACHE_KEY);
            if (cachedData != null)
            {
                _logger.LogDebug("Cache hit for MP3 metadata database");
                return cachedData;
            }

            await _dbLock.WaitAsync();
            try
            {
                // Double-check cache after acquiring lock
                cachedData = await _cache.GetAsync<List<IMp3>>(DB_CACHE_KEY);
                if (cachedData != null)
                {
                    return cachedData;
                }

                // Load from file if not in cache
                if (!File.Exists(_metadataPath))
                {
                    var emptyList = new List<IMp3>();
                    await _cache.SetAsync(DB_CACHE_KEY, emptyList, DB_CACHE_DURATION);
                    return emptyList;
                }

                var json = await File.ReadAllTextAsync(_metadataPath);
                var mp3Files = JsonConvert.DeserializeObject<List<IMp3>>(json, _jsonSettings)
                    ?? new List<IMp3>();

                // Cache the loaded data
                await _cache.SetAsync(DB_CACHE_KEY, mp3Files, DB_CACHE_DURATION);

                _logger.LogDebug("Loaded {Count} MP3 files from database", mp3Files.Count);
                return mp3Files;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load MP3 files database");
                throw;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        public async Task SaveMp3MetadatasAsync(List<IMp3> mp3Files)
        {
            ArgumentNullException.ThrowIfNull(mp3Files);

            await _dbLock.WaitAsync();
            try
            {
                // Write to temporary file first
                string tempPath = $"{_metadataPath}.tmp";
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(mp3Files, Newtonsoft.Json.Formatting.Indented);
                await File.WriteAllTextAsync(tempPath, json);

                // Atomic rename
                if (File.Exists(_metadataPath))
                    File.Delete(_metadataPath);
                File.Move(tempPath, _metadataPath);

                // Update cache
                await _cache.SetAsync(DB_CACHE_KEY, mp3Files, DB_CACHE_DURATION);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save MP3 files database");
                throw;
            }
            finally
            {
                _dbLock.Release();
            }
        }

        /// <summary>
        /// Initializes the directories for storage and database.
        /// </summary>
        private async Task InitializeDirectoriesAsync()
        {
            try
            {
                // Create base storage directory
                if (!Directory.Exists(_baseStoragePath))
                {
                    await Task.Run(() => Directory.CreateDirectory(_baseStoragePath));
                    _logger.LogInformation("Created base storage directory: {Path}", _baseStoragePath);
                }

                // Create database directory
                string dbDirectory = Path.GetDirectoryName(_metadataPath)!;
                if (!Directory.Exists(dbDirectory))
                {
                    await Task.Run(() => Directory.CreateDirectory(dbDirectory));
                    _logger.LogInformation("Created database directory: {Path}", dbDirectory);
                }

                // Verify write permissions
                await VerifyDirectoryAccessAsync(_baseStoragePath);
                await VerifyDirectoryAccessAsync(dbDirectory);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied while creating directories. Please check permissions");
                throw;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error while creating directories");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize directories");
                throw;
            }
        }

        private async Task VerifyDirectoryAccessAsync(string path)
        {
            try
            {
                string testFile = Path.Combine(path, ".write-test");
                await using (var fs = new FileStream(testFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await fs.WriteAsync(new byte[1], 0, 1);
                }
                await Task.Run(() => File.Delete(testFile));
            }
            catch (Exception ex)
            {
                throw new UnauthorizedAccessException($"Cannot write to directory: {path}", ex);
            }
        }

        #region File Operations

        private string GetFullPath(string filePath)
        {
            return Path.Combine(_baseStoragePath, filePath);
        }
        public async Task<IMp3> LoadMp3MetaByPathAsync(string filePath)
        {
            try
            {
                var mp3Files = await LoadListMp3MetadatasAsync();
                return mp3Files.FirstOrDefault(f => f.FilePath == filePath)
                    ?? throw new FileNotFoundException($"MP3 file not found: {filePath}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load single MP3 file: {FilePath}", filePath);
                throw;
            }
        }

        #endregion

        #region Database Operations
        public async Task<Data.Interfaces.IMp3> LoadMp3MetaByNewsIdAsync<IMp3>(string id) 
        {
            ArgumentException.ThrowIfNullOrEmpty(id);

            try
            {
                await _dbLock.WaitAsync();
                try
                {
                    Data.Interfaces.IMp3 mp3File = (Data.Interfaces.IMp3)await _mp3MetaRepository.GetByIdAsync(int.Parse(id))
                        ?? throw new KeyNotFoundException($"MP3 file not found for ID: {id}");

                    _logger.LogDebug("Found MP3 metadata for ID: {Id}", id);
                    return mp3File;
                }
                finally
                {
                    _dbLock.Release();
                }
            }
            catch (Exception ex) when (ex is not KeyNotFoundException)
            {
                _logger.LogError(ex, "Failed to load MP3 metadata for ID: {Id}", id);
                throw;
            }
        }

        public async Task<IMp3> LoadLatestMp3MetaByLanguageAsync(string language)
        {
            try
            {
                var mp3Files = await LoadListMp3MetadatasAsync();
                return mp3Files
                    .Where(f => f.Language.Equals(language, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.CreatedDate)
                    .FirstOrDefault()
                    ?? throw new KeyNotFoundException($"No MP3 file found for language: {language}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get latest MP3 file for language: {Language}", language);
                throw;
            }
        }

        public async Task SaveSingleMp3MetaAsync(IMp3 mp3File)
        {
            try
            {
                var mp3Files = await LoadListMp3MetadatasAsync();
                mp3Files.Add(mp3File);
                await SaveMp3MetadatasAsync(mp3Files);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save MP3 file metadata");
                throw;
            }
        }
        public async Task<Data.Interfaces.IMp3?> LoadAndCacheMp3File<IMp3>(string id) 
        {
            var mp3File = await LoadMp3MetaByNewsIdAsync<Data.Interfaces.IMp3>(id);
            if (mp3File != null)
            {
                await SetToCacheAsync($"IMp3:{id}", mp3File, TimeSpan.FromHours(1));
            }
            return mp3File;
        }
        public async Task<IMp3?> GetFromCacheAsync<IMp3>(string key) 
        {
            ArgumentNullException.ThrowIfNull(_cache);
            return await _cache.GetAsync<IMp3>(key);
        }
        public async Task SetToCacheAsync<IMp3>(string key, IMp3 value, TimeSpan? expiry = null)
        {
            ArgumentNullException.ThrowIfNull(_cache);
            await _cache.SetAsync(key, value, expiry);
        }

        #endregion

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _dbLock.Dispose();
                    foreach (var fileLock in _fileLocks.Values)
                    {
                        fileLock.Dispose();
                    }
                    _fileLocks.Clear();
                }
                _disposed = true;
            }
        }
    }
}