using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Telltale.Models;

namespace Telltale.Services;

public class FileCache : IFileCache
{
    private readonly string _root;
    private readonly ILogger<FileCache> _logger;
    private static readonly SemaphoreSlim _indexLock = new(1, 1);
    private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };

    public FileCache(IConfiguration configuration, ILogger<FileCache> logger)
    {
        _logger = logger;
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
            var value = await JsonSerializer.DeserializeAsync<T>(fs);
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
