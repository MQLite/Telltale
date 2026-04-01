using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Telltale.Models;

namespace Telltale.Services;

public class ClaudeService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    IFileCache fileCache,
    ILogger<ClaudeService> logger) : IClaudeService
{
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    private static string NormalizeKeywords(string keywords) =>
        string.Join(' ', keywords.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Order());

    public async Task<StoryResponse> GenerateStoryAsync(string keywords, string language)
    {
        var cacheKey = $"story:{NormalizeKeywords(keywords)}:{language}";

        // L1 — memory
        if (cache.TryGetValue(cacheKey, out StoryResponse? cached))
        {
            logger.LogInformation("Memory HIT — story key={Key}", cacheKey);
            return cached!;
        }

        // L2 — disk
        var fromDisk = await fileCache.GetAsync<StoryResponse>(cacheKey);
        if (fromDisk is not null)
        {
            var memTtl = configuration.GetValue<int>("Cache:StoryTtlHours", 24);
            cache.Set(cacheKey, fromDisk, TimeSpan.FromHours(memTtl));
            return fromDisk;
        }

        logger.LogInformation("Cache MISS — calling Claude for key={Key}", cacheKey);

        var apiKey = configuration["Claude:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Claude API key not configured. Set Claude:ApiKey via User Secrets or CLAUDE__APIKEY env var.");

        var model = configuration["Claude:Model"] ?? "claude-sonnet-4-6";
        var client = httpClientFactory.CreateClient();

        var prompt = $$"""
            You are a creative children's story writer. Generate a short illustrated story based on these keywords: {{keywords}}

            The preferred reading language is: {{language}} (en = English-first, zh = Chinese-first — adjust narrative tone accordingly, but always provide BOTH languages).

            Return ONLY a valid JSON object with this exact structure — no markdown, no code fences, no extra text:
            {
              "titleEn": "Story title in English",
              "titleZh": "故事标题（中文）",
              "pages": [
                {
                  "pageNumber": 1,
                  "contentEn": "2-3 sentences of story content in English.",
                  "contentZh": "2-3句中文故事内容。",
                  "imagePrompt": "cartoon oil painting style, children's book illustration, warm soft colors, [describe the exact scene: characters, setting, actions, mood, lighting]"
                }
              ]
            }

            Requirements:
            - Generate exactly 4 pages
            - Each page: 2-3 sentences, warm and whimsical, age-appropriate for children 4-8
            - Story must have a clear beginning, middle, and positive ending
            - imagePrompt: vivid, painterly scene description in English — always start with "cartoon oil painting style, children's book illustration"
            - CRITICAL: All string values must be on a single line — no newlines inside any JSON string value
            - CRITICAL: Do not use double-quote characters inside any string value
            - CRITICAL: Return ONLY valid JSON — nothing else before or after, no trailing commas
            """;

        var requestBody = new
        {
            model,
            max_tokens = 3000,
            messages = new[] { new { role = "user", content = prompt } }
        };

        logger.LogInformation("Calling Claude API — model={Model} keywords={Keywords}", model, keywords);

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex,
                "HTTP request to Claude API failed — Status={Status} Message={Message}",
                ex.StatusCode, ex.Message);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogError(
                "Claude API returned {StatusCode} {ReasonPhrase} — Body: {Body}",
                (int)response.StatusCode, response.ReasonPhrase, errorBody);
            response.EnsureSuccessStatusCode(); // rethrow as HttpRequestException
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        logger.LogDebug("Claude API raw response: {Response}", responseJson);

        using var doc = JsonDocument.Parse(responseJson);
        var rawText = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? "{}";

        // Strip markdown code fences, then extract the outermost { ... } block
        var stripped = Regex.Replace(rawText, @"^```(?:json)?\s*|\s*```$", "", RegexOptions.Multiline).Trim();
        var jsonMatch = Regex.Match(stripped, @"\{[\s\S]*\}", RegexOptions.Singleline);
        var json = jsonMatch.Success ? jsonMatch.Value : stripped;

        StoryResponse? story;
        try
        {
            story = JsonSerializer.Deserialize<StoryResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            logger.LogError(ex,
                "Failed to parse story JSON — {Message}\nRaw Claude text:\n{Raw}",
                ex.Message, rawText);
            throw;
        }

        logger.LogInformation("Story generated — Title={Title} Pages={Pages}",
            story?.TitleEn, story?.Pages.Count);

        var result = story ?? new StoryResponse { TitleEn = "A Story", TitleZh = "一个故事", Pages = [] };

        var ttl = configuration.GetValue<int>("Cache:StoryTtlHours", 24);
        cache.Set(cacheKey, result, TimeSpan.FromHours(ttl));
        await fileCache.SetAsync(cacheKey, result);
        await fileCache.AddStoryMetaAsync(new(result.TitleEn, result.TitleZh, keywords, language, DateTime.UtcNow));

        return result;
    }
}
