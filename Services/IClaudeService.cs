using Telltale.Models;

namespace Telltale.Services;

public interface IClaudeService
{
    Task<StoryResponse> GenerateStoryAsync(string keywords, string language);
}
