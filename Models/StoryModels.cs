namespace Telltale.Models;

public record StoryRequest(string Keywords, string Language);

public record BatchTtsRequest(string[] Texts, string[] Emotions, string Language, string? Voice);

public class StoryPage
{
    public int PageNumber { get; set; }
    public string ContentEn { get; set; } = "";
    public string ContentZh { get; set; } = "";
    public string ImagePrompt { get; set; } = "";
    public string Emotion { get; set; } = "warmly";
}

public class StoryResponse
{
    public string TitleEn { get; set; } = "";
    public string TitleZh { get; set; } = "";
    public List<StoryPage> Pages { get; set; } = [];
}
