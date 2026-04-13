namespace Telltale.Models;

public record StoryRequest(string Keywords, string Language);

public record StoryVoiceRequest(string Keywords, string Language, string Voice);

public record BatchTtsRequest(string[] Texts, string Language, string? Voice);

public record Sentence(string Text, string Emotion);

public class StoryPage
{
    public int PageNumber { get; set; }
    public List<Sentence> SentencesEn { get; set; } = [];
    public List<Sentence> SentencesZh { get; set; } = [];

    // Computed from sentences; settable so old cached JSON (pre-sentences format) deserializes correctly.
    private string? _contentEn;
    private string? _contentZh;
    public string ContentEn
    {
        get => SentencesEn.Count > 0 ? string.Join(" ", SentencesEn.Select(s => s.Text)) : (_contentEn ?? "");
        set => _contentEn = value;
    }
    public string ContentZh
    {
        get => SentencesZh.Count > 0 ? string.Join(" ", SentencesZh.Select(s => s.Text)) : (_contentZh ?? "");
        set => _contentZh = value;
    }

    public string ImagePrompt { get; set; } = "";
}

public class StoryResponse
{
    public string TitleEn { get; set; } = "";
    public string TitleZh { get; set; } = "";
    public List<StoryPage> Pages { get; set; } = [];
}
