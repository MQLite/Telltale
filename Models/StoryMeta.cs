namespace Telltale.Models;

public record StoryMeta(
    string TitleEn,
    string TitleZh,
    string Keywords,
    string Language,
    DateTime CreatedAt,
    List<string>? CachedVoices = null
);
