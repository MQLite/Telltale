namespace Telltale.Models;

public record StoryRequest(string Keywords, string Language);

public record BatchTtsRequest(string[] Texts, string Language, string? Voice);

public record Sentence(string Text, string Emotion);

public class StoryPage
{
    public int PageNumber { get; set; }
    public List<Sentence> SentencesEn { get; set; } = [];
    public List<Sentence> SentencesZh { get; set; } = [];
    public string ContentEn => string.Join(" ", SentencesEn.Select(s => s.Text));
    public string ContentZh => string.Join(" ", SentencesZh.Select(s => s.Text));
    public string ImagePrompt { get; set; } = "";
}

public class StoryResponse
{
    public string TitleEn { get; set; } = "";
    public string TitleZh { get; set; } = "";
    public List<StoryPage> Pages { get; set; } = [];
}
