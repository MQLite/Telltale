namespace Telltale.Services;

public class PollinationsTtsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IFileCache fileCache,
    ILogger<PollinationsTtsService> logger) : ITtsService
{
    private const string BaseUrl = "https://text.pollinations.ai";

    public async Task<(byte[] Bytes, string ContentType)> SynthesizeAsync(string text, string language)
    {
        var cacheKey = $"tts:{language}:{text}";

        if (await fileCache.GetBytesAsync(cacheKey) is { } fromDisk)
        {
            logger.LogInformation("TTS cache HIT — lang={Lang}", language);
            return fromDisk;
        }

        var voice   = language == "zh"
            ? (configuration["Pollinations:TtsVoiceZh"] ?? "nova")
            : (configuration["Pollinations:TtsVoiceEn"] ?? "alloy");
        var apiKey  = configuration["Pollinations:ApiKey"];

        var url = $"{BaseUrl}/{Uri.EscapeDataString(text)}?model=openai-audio&voice={voice}";
        if (!string.IsNullOrWhiteSpace(apiKey))
            url += $"&key={apiKey}";

        logger.LogInformation("TTS request — voice={Voice} lang={Lang}", voice, language);

        var client = httpClientFactory.CreateClient("image"); // reuse 5-min timeout client
        using var response = await client.GetAsync(url);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            logger.LogError("Pollinations TTS returned {Status} — {Body}", (int)response.StatusCode, body);
            response.EnsureSuccessStatusCode();
        }

        var bytes = await response.Content.ReadAsByteArrayAsync();
        const string contentType = "audio/mpeg";

        await fileCache.SetBytesAsync(cacheKey, bytes, contentType);
        logger.LogInformation("TTS received — voice={Voice} size={Size}b", voice, bytes.Length);

        return (bytes, contentType);
    }
}
