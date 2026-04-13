using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Telltale.Models;

namespace Telltale.Services;

public class PollinationsStoryService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache cache,
    IFileCache fileCache,
    ILogger<PollinationsStoryService> logger) : IClaudeService
{
    private const string ApiUrl = "https://gen.pollinations.ai/v1/chat/completions";

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

        var apiKey = configuration["Pollinations:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException(
                "Pollinations API key not configured. Set Pollinations:ApiKey.");

        var model = configuration["Pollinations:TextModel"] ?? "qwen-safety";

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
            stream = false,
            response_format = new { type = "json_object" },
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        };

        logger.LogInformation("Calling Pollinations chat API — model={Model} keywords={Keywords}", model, keywords);

        var client = httpClientFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        request.Content = JsonContent.Create(requestBody);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP request to Pollinations chat API failed — {Message}", ex.Message);
            throw;
        }

        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            logger.LogError("Pollinations chat API returned {Status} — Body: {Body}",
                (int)response.StatusCode, errorBody);
            response.EnsureSuccessStatusCode();
        }

        var responseJson = await response.Content.ReadAsStringAsync();
        logger.LogDebug("Pollinations chat raw response: {Response}", responseJson);

        // Parse OpenAI-compatible response: choices[0].message.content
        using var doc = JsonDocument.Parse(responseJson);
        var rawText = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";

        // Strip markdown fences, extract outermost { ... }
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
            logger.LogError(ex, "Failed to parse story JSON — {Message}\nRaw text:\n{Raw}", ex.Message, rawText);
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
