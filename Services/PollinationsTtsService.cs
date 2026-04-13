namespace Telltale.Services;

public class PollinationsTtsService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IFileCache fileCache,
    ILogger<PollinationsTtsService> logger) : ITtsService
{
    private const string BaseUrl = "https://gen.pollinations.ai/audio";

    public async Task<(byte[] Bytes, string ContentType)> SynthesizeAsync(string text, string language, string? voice = null)
    {
        var defaultVoice = language == "zh"
            ? (configuration["Pollinations:TtsVoiceZh"] ?? "nova")
            : (configuration["Pollinations:TtsVoiceEn"] ?? "fable");
        voice = string.IsNullOrWhiteSpace(voice) ? defaultVoice : voice;

        var cacheKey = $"tts:{language}:{voice}:{text}";

        if (await fileCache.GetBytesAsync(cacheKey) is { } fromDisk)
        {
            logger.LogInformation("TTS cache HIT — lang={Lang} voice={Voice}", language, voice);
            return fromDisk;
        }
        var apiKey = configuration["Pollinations:ApiKey"];

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Pollinations API key not configured. Register free at https://auth.pollinations.ai and set Pollinations:ApiKey.");

        var url = $"{BaseUrl}/{Uri.EscapeDataString(text)}?model=tts-1&voice={voice}&response_format=mp3";

        logger.LogInformation("TTS request — voice={Voice} lang={Lang}", voice, language);

        var client = httpClientFactory.CreateClient("image");
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Headers.Add("X-Api-Key", apiKey);
        using var response = await client.SendAsync(request);

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
