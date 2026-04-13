using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Telltale.Models;

namespace Telltale.Services;

public class FileCache : IFileCache
{
    private readonly string _root;
    private readonly ILogger<FileCache> _logger;
    private readonly IMemoryCache _memoryCache;
    private static readonly SemaphoreSlim _indexLock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public FileCache(IConfiguration configuration, IMemoryCache memoryCache, ILogger<FileCache> logger)
    {
        _logger = logger;
        _memoryCache = memoryCache;
        _root = Path.GetFullPath(configuration["Storage:Path"] ?? "./data");
        _logger.LogInformation("FileCache storage root resolved to: {Root}", _root);
    }

    private string Dir(string sub)
    {
        var dir = Path.Combine(_root, sub);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Hash(string key)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes)[..24].ToLowerInvariant();
    }

    // ── JSON (stories) ────────────────────────────────────────────────

    public async Task<T?> GetAsync<T>(string key) where T : class
    {
        var path = Path.Combine(Dir("stories"), $"{Hash(key)}.json");
        if (!File.Exists(path)) return null;

        try
        {
            await using var fs = File.OpenRead(path);
            var value = await JsonSerializer.DeserializeAsync<T>(fs,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            _logger.LogInformation("Disk HIT  — story key={Key}", key);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk read failed for story key={Key}, deleting corrupt file", key);
            File.Delete(path);
            return null;
        }
    }

    public async Task SetAsync<T>(string key, T value) where T : class
    {
        var path = Path.Combine(Dir("stories"), $"{Hash(key)}.json");
        try
        {
            await using var fs = File.Create(path);
            await JsonSerializer.SerializeAsync(fs, value);
            _logger.LogInformation("Disk WRITE — story key={Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk write failed for story key={Key}", key);
        }
    }

    // ── Binary (images) ───────────────────────────────────────────────

    public async Task<(byte[] Data, string ContentType)?> GetBytesAsync(string key)
    {
        var hash = Hash(key);
        var dataPath = Path.Combine(Dir("images"), $"{hash}.bin");
        var metaPath = Path.Combine(Dir("images"), $"{hash}.meta");

        if (!File.Exists(dataPath) || !File.Exists(metaPath)) return null;

        try
        {
            var data        = await File.ReadAllBytesAsync(dataPath);
            var contentType = await File.ReadAllTextAsync(metaPath);
            _logger.LogInformation("Disk HIT  — image key={Key}", key);
            return (data, contentType.Trim());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk read failed for image key={Key}", key);
            return null;
        }
    }

    public async Task SetBytesAsync(string key, byte[] data, string contentType)
    {
        var hash = Hash(key);
        var dataPath = Path.Combine(Dir("images"), $"{hash}.bin");
        var metaPath = Path.Combine(Dir("images"), $"{hash}.meta");

        try
        {
            await File.WriteAllBytesAsync(dataPath, data);
            await File.WriteAllTextAsync(metaPath, contentType);
            _logger.LogInformation("Disk WRITE — image key={Key} size={Size}b", key, data.Length);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Disk write failed for image key={Key}", key);
        }
    }

    // ── Story index ───────────────────────────────────────────────────

    private string IndexPath => Path.Combine(Dir("stories"), "index.json");

    private static string NormalizeKeywords(string keywords) =>
        string.Join(' ', keywords.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order());

    public async Task<List<StoryMeta>> GetStoryListAsync()
    {
        if (!File.Exists(IndexPath)) return [];

        try
        {
            await using var fs = File.OpenRead(IndexPath);
            return await JsonSerializer.DeserializeAsync<List<StoryMeta>>(fs) ?? [];
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read story index");
            return [];
        }
    }

    public async Task AddCachedVoiceAsync(string keywords, string language, string voice)
    {
        await _indexLock.WaitAsync();
        try
        {
            var list = await GetStoryListAsync();
            var idx = list.FindIndex(s => s.Keywords == keywords && s.Language == language);
            if (idx < 0) return;

            var entry = list[idx];
            var voices = entry.CachedVoices ?? [];
            if (voices.Contains(voice)) return;

            list[idx] = entry with { CachedVoices = [.. voices, voice] };

            await using var fs = File.Create(IndexPath);
            await JsonSerializer.SerializeAsync(fs, list, _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update cached voices for keywords={Keywords}", keywords);
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public string BuildStoryCacheKey(string keywords, string language) =>
        $"story:{NormalizeKeywords(keywords)}:{language}";

    public async Task DeleteStoryAsync(string keywords, string language)
    {
        var cacheKey = BuildStoryCacheKey(keywords, language);
        var path = Path.Combine(Dir("stories"), $"{Hash(cacheKey)}.json");
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogInformation("Disk DELETE — story key={Key}", cacheKey);
        }

        _memoryCache.Remove(cacheKey);

        await _indexLock.WaitAsync();
        try
        {
            var list = await GetStoryListAsync();
            list.RemoveAll(s => s.Keywords == keywords && s.Language == language);
            await using var fs = File.Create(IndexPath);
            await JsonSerializer.SerializeAsync(fs, list, _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update story index after delete");
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public async Task AddStoryMetaAsync(StoryMeta meta)
    {
        await _indexLock.WaitAsync();
        try
        {
            var list = await GetStoryListAsync();

            // Replace if same keywords + language already exists
            var existing = list.FindIndex(s =>
                s.Keywords == meta.Keywords && s.Language == meta.Language);

            if (existing >= 0)
                list[existing] = meta;
            else
                list.Insert(0, meta); // newest first

            await using var fs = File.Create(IndexPath);
            await JsonSerializer.SerializeAsync(fs, list, _json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update story index");
        }
        finally
        {
            _indexLock.Release();
        }
    }
}
