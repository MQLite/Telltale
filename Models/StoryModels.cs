namespace Telltale.Models;

public record StoryRequest(string Keywords, string Language);

public class StoryPage
{
    public int PageNumber { get; set; }
    public string ContentEn { get; set; } = "";
    public string ContentZh { get; set; } = "";
    public string ImagePrompt { get; set; } = "";
}

public class StoryResponse
{
    public string TitleEn { get; set; } = "";
    public string TitleZh { get; set; } = "";
    public List<StoryPage> Pages { get; set; } = [];
}
