using ColorAnalyzer;
using ColorAnalyzer.Util;
using Grpc.Core;
using System.Drawing;

public class ColorAnalyzerService : ColorAnalyzer.ColorAnalyzer.ColorAnalyzerBase
{
    public override async Task<ColorPalette> AnalyzeColors(
        IAsyncStreamReader<ImageChunk> requestStream,
        ServerCallContext context)
    {
        using var memoryStream = new MemoryStream();

        while (await requestStream.MoveNext())
        {
            ServiceUtil.CheckContext(context);

            var chunk = requestStream.Current;
            memoryStream.Write(chunk.Data.ToByteArray());

            if (chunk.IsLast)
                break;
        }

        var colors = ExtrairPaleta(memoryStream.ToArray());
        var response = new ColorPalette();
        response.Colors.AddRange(colors);
        return response;
    }

    private List<string> ExtrairPaleta(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        using var bitmap = new Bitmap(ms);

        var resized = new Bitmap(bitmap, new Size(50, 50));
        var pixels = new List<Color>();

        for (int y = 0; y < resized.Height; y++)
            for (int x = 0; x < resized.Width; x++)
                pixels.Add(resized.GetPixel(x, y));

        return pixels
            .GroupBy(c => c)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(c => $"#{c.Key.R:X2}{c.Key.G:X2}{c.Key.B:X2}")
            .ToList();
    }
}
