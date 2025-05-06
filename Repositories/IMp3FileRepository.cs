
using MyTts.Models;

namespace MyTts.Repositories
{
    public interface IMp3FileRepository : IDisposable
    {
        Task<byte[]> LoadMp3FileAsync(string filePath);
        Task SaveMp3FileAsync(string filePath, byte[] fileData);
        Task<List<Mp3File>> LoadListMp3MetadatasAsync();
        Task SaveMp3MetadatasAsync(List<Mp3File> mp3Files);
        Task DeleteMp3FileAsync(string filePath);
        Task<bool> Mp3FileExistsAsync(string filePath);
        Task<Mp3File> LoadMp3MetaByPathAsync(string filePath);
        Task<Mp3File> LoadMp3MetaByNewsIdAsync(string id);
        Task<Mp3File> LoadLatestMp3MetaByLanguageAsync(string language);
        Task SaveSingleMp3MetaAsync(Mp3File mp3File);
        Task<Mp3File?> LoadAndCacheMp3File(string id);
        Task<T?> GetFromCacheAsync<T>(string key) where T : class;
        Task SetToCacheAsync<T>(string key, T value, TimeSpan? expiry = null);
    }
}