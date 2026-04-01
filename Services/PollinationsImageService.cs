using System.Net;
using Microsoft.Extensions.Caching.Memory;

namespace Telltale.Services;

public class PollinationsImageService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    IFileCache fileCache,
    ILogger<PollinationsImageService> logger) : IImageService
{
    private const string BaseUrl = "https://gen.pollinations.ai";
    private const int MaxRetries = 4;

    private static readonly SemaphoreSlim _throttle = new(1, 1);

    public async Task<(byte[] Bytes, string ContentType)> GenerateImageAsync(string prompt, int seed)
    {
        var cacheKey = $"img:{prompt}:{seed}";

        // L1 — memory
        if (cache.TryGetValue(cacheKey, out (byte[], string)? hit))
        {
            logger.LogInformation("Memory HIT — image seed={Seed}", seed);
            return hit!.Value;
        }

        // L2 — disk
        if (await fileCache.GetBytesAsync(cacheKey) is { } fromDisk)
        {
            var memTtl = configuration.GetValue<int>("Cache:ImageTtlHours", 24);
            cache.Set(cacheKey, fromDisk, TimeSpan.FromHours(memTtl));
            return fromDisk;
        }

        logger.LogInformation("Cache MISS — generating image seed={Seed}", seed);

        var model  = configuration["Pollinations:Model"]  ?? "flux";
        var width  = configuration.GetValue<int>("Pollinations:Width",  800);
        var height = configuration.GetValue<int>("Pollinations:Height", 520);
        var apiKey = configuration["Pollinations:ApiKey"];

        var full = $"{prompt}, vibrant warm colors, soft golden light, magical storybook atmosphere";
        var url  = $"{BaseUrl}/image/{Uri.EscapeDataString(full)}" +
                   $"?model={model}&width={width}&height={height}&seed={seed}&nologo=true";

        if (!string.IsNullOrWhiteSpace(apiKey))
            url += $"&key={Uri.EscapeDataString(apiKey)}";

        var client = httpClientFactory.CreateClient("image");

        for (var attempt = 1; attempt <= MaxRetries; attempt++)
        {
            await _throttle.WaitAsync();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrWhiteSpace(apiKey))
                    request.Headers.Add("Authorization", $"Bearer {apiKey}");

                logger.LogInformation(
                    "Pollinations request — model={Model} seed={Seed} attempt={Attempt}",
                    model, seed, attempt);

                using var response = await client.SendAsync(request);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var wait = GetRetryDelay(response, attempt);
                    logger.LogWarning(
                        "429 rate-limited — seed={Seed} attempt={Attempt}, waiting {Wait}s",
                        seed, attempt, wait.TotalSeconds);

                    if (attempt == MaxRetries)
                        throw new HttpRequestException("Pollinations returned 429", null, response.StatusCode);

                    await Task.Delay(wait);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    logger.LogError("Pollinations returned {Status} — Body: {Body}", response.StatusCode, body);
                    throw new HttpRequestException($"Pollinations returned {(int)response.StatusCode}", null, response.StatusCode);
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";

                logger.LogInformation(
                    "Image received — model={Model} seed={Seed} size={Bytes}b",
                    model, seed, bytes.Length);

                var ttl = configuration.GetValue<int>("Cache:ImageTtlHours", 24);
                cache.Set(cacheKey, (bytes, contentType), TimeSpan.FromHours(ttl));
                await fileCache.SetBytesAsync(cacheKey, bytes, contentType);

                return (bytes, contentType);
            }
            finally
            {
                _throttle.Release();
            }
        }

        throw new HttpRequestException("Max retries exceeded for image generation");
    }

    private static TimeSpan GetRetryDelay(HttpResponseMessage response, int attempt)
    {
        if (response.Headers.RetryAfter?.Delta is { } delta)
            return delta + TimeSpan.FromSeconds(1);

        // Exponential backoff: 5s, 10s, 20s, 40s
        return TimeSpan.FromSeconds(5 * Math.Pow(2, attempt - 1));
    }
}
