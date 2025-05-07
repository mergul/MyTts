
using MyTts.Data.Interfaces;

namespace MyTts.Repositories
{
    public interface IMp3FileRepository : IDisposable
    {
        Task<byte[]> LoadMp3FileAsync(string filePath);
        Task SaveMp3FileAsync(string filePath, byte[] fileData);
        Task<List<IMp3>> LoadListMp3MetadatasAsync();
        Task SaveMp3MetadatasAsync(List<IMp3> mp3Files);
        Task DeleteMp3FileAsync(string filePath);
        Task<bool> Mp3FileExistsAsync(string filePath);
        Task<IMp3> LoadMp3MetaByPathAsync(string filePath);
        Task<Data.Interfaces.IMp3> LoadMp3MetaByNewsIdAsync<IMp3>(string id);
        Task<IMp3> LoadLatestMp3MetaByLanguageAsync(string language);
        Task SaveSingleMp3MetaAsync(IMp3 mp3File);
        Task<Data.Interfaces.IMp3?> LoadAndCacheMp3File<IMp3>(string id) ;
        Task<IMp3?> GetFromCacheAsync<IMp3>(string key) ;
        Task SetToCacheAsync<IMp3>(string key, IMp3 value, TimeSpan? expiry = null);
    }
}