using ColorAnalyzer;
using Grpc.Core;
using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

public class ColorAnalyzerService : ColorAnalyzer.ColorAnalyzer.ColorAnalyzerBase
{
    public override Task<ColorPalette> AnalyzeColors(ImageData request, ServerCallContext context)
    {
        var colors = ExtrairPaleta(request.ImageData_.ToByteArray());

        var response = new ColorPalette();
        response.Colors.AddRange(colors);

        return Task.FromResult(response);
    }

    private List<string> ExtrairPaleta(byte[] imageBytes, int numColors = 5)
    {
        using var image = Image.Load<Rgba32>(imageBytes);

        var pixels = new List<Vector3>();

        for (int y = 0; y < image.Height; y++)
        {
            for (int x = 0; x < image.Width; x++)
            {
                var p = image[x, y];
                pixels.Add(new Vector3(p.R / 255f, p.G / 255f, p.B / 255f));
            }
        }

        var clusters = KMeansCluster(pixels, numColors);

        return clusters
            .Select(v => $"#{(int)(v.X * 255):X2}{(int)(v.Y * 255):X2}{(int)(v.Z * 255):X2}")
            .ToList();
    }

    private List<Vector3> KMeansCluster(List<Vector3> data, int k, int maxIterations = 10)
    {
        var rnd = new Random();
        var centroids = data.OrderBy(_ => rnd.Next()).Take(k).ToArray();
        var clusters = new int[data.Count];

        for (int iter = 0; iter < maxIterations; iter++)
        {
            for (int i = 0; i < data.Count; i++)
            {
                float minDist = float.MaxValue;
                for (int c = 0; c < k; c++)
                {
                    float dist = Vector3.Distance(data[i], centroids[c]);
                    if (dist < minDist)
                    {
                        minDist = dist;
                        clusters[i] = c;
                    }
                }
            }

            for (int c = 0; c < k; c++)
            {
                var clusterPoints = data.Where((_, i) => clusters[i] == c).ToList();
                if (clusterPoints.Count > 0)
                {
                    centroids[c] = new Vector3(
                        clusterPoints.Average(p => p.X),
                        clusterPoints.Average(p => p.Y),
                        clusterPoints.Average(p => p.Z)
                    );
                }
            }
        }

        return centroids.ToList();
    }
}
