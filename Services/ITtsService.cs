namespace Telltale.Services;

public interface ITtsService
{
    Task<(byte[] Bytes, string ContentType)> SynthesizeAsync(string text, string language, string? voice = null);
}
