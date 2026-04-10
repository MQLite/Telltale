using Telltale.Models;
using Telltale.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient("image", c => c.Timeout = TimeSpan.FromMinutes(5));

if (builder.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Claude:UseMock"))
    builder.Services.AddSingleton<IClaudeService, MockClaudeService>();
else
    builder.Services.AddSingleton<IClaudeService, ClaudeService>();

builder.Services.AddSingleton<IFileCache, FileCache>();
builder.Services.AddSingleton<IImageService, PollinationsImageService>();
builder.Services.AddSingleton<ITtsService, PollinationsTtsService>();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseHttpsRedirection();

app.MapPost("/api/story/generate", async (
    StoryRequest request,
    IClaudeService claude,
    ILogger<Program> logger) =>
{
    try
    {
        var story = await claude.GenerateStoryAsync(request.Keywords, request.Language);
        return Results.Ok(story);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "HttpRequestException in /api/story/generate — Status={Status}", ex.StatusCode);
        return Results.Problem(detail: ex.Message, statusCode: (int?)ex.StatusCode ?? 502, title: "Upstream API error");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unexpected error in /api/story/generate");
        return Results.Problem(ex.Message);
    }
})
.WithName("GenerateStory")
.WithOpenApi();

app.MapGet("/api/story/list", async (IFileCache fileCache) =>
    Results.Ok(await fileCache.GetStoryListAsync()))
.WithName("ListStories")
.WithOpenApi();

app.MapGet("/api/image", async (
    string prompt,
    int seed,
    IImageService imageService,
    ILogger<Program> logger) =>
{
    try
    {
        var (bytes, contentType) = await imageService.GenerateImageAsync(prompt, seed);
        return Results.Bytes(bytes, contentType);
    }
    catch (TaskCanceledException)
    {
        return Results.Problem("Image request timed out", statusCode: 504);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "Image generation failed");
        return Results.Problem(ex.Message, statusCode: (int?)ex.StatusCode ?? 502);
    }
})
.WithName("GetImage")
.WithOpenApi();

app.MapGet("/api/tts", async (
    string text,
    string lang,
    string? voice,
    ITtsService tts,
    ILogger<Program> logger) =>
{
    if (string.IsNullOrWhiteSpace(text))
        return Results.BadRequest("text is required");
    try
    {
        var (bytes, contentType) = await tts.SynthesizeAsync(text, lang, voice);
        return Results.Bytes(bytes, contentType);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(ex.Message, statusCode: 501);
    }
    catch (HttpRequestException ex)
    {
        logger.LogError(ex, "TTS generation failed");
        return Results.Problem(ex.Message, statusCode: (int?)ex.StatusCode ?? 502);
    }
})
.WithName("GetTts")
.WithOpenApi();

app.Run();
