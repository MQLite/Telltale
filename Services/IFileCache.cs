using Telltale.Models;

namespace Telltale.Services;

public interface IFileCache
{
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value) where T : class;
    Task<(byte[] Data, string ContentType)?> GetBytesAsync(string key);
    Task SetBytesAsync(string key, byte[] data, string contentType);
    Task<List<StoryMeta>> GetStoryListAsync();
    Task AddStoryMetaAsync(StoryMeta meta);
    Task DeleteStoryAsync(string keywords, string language);
    string BuildStoryCacheKey(string keywords, string language);
}
