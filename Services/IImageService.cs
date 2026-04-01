namespace Telltale.Services;

public interface IImageService
{
    Task<(byte[] Bytes, string ContentType)> GenerateImageAsync(string prompt, int seed);
}
