using ColorAnalyzer.Enum;
using ColorAnalyzer.Util;
using Grpc.Core;
using ImageFilter;
using System.Drawing;
using System.Drawing.Imaging;

public class ImageFilterService : ImageFilter.ImageFilterService.ImageFilterServiceBase
{
    private TipoFiltro? _filterType { get; set; }
    private string? _imageId { get; set; }

    public override async Task ApplyFilterStream(
        IAsyncStreamReader<ImageFilterChunk> requestStream,
        IServerStreamWriter<ImageFilterChunk> responseStream,
        ServerCallContext context)
    {
        var imageChunks = new List<byte>();

        while (await requestStream.MoveNext())
        {
            ServiceUtil.CheckContext(context);

            var chunk = requestStream.Current;

            if (_filterType == null && Enum.IsDefined(typeof(TipoFiltro), chunk.FilterType))
            {
                _filterType = (TipoFiltro)chunk.FilterType;
                _imageId = chunk.ImageId;
            }
            else if (_filterType == null)
            {
                throw new RpcException(new Status(StatusCode.InvalidArgument, $"Filtro inválido: {chunk.FilterType}"));
            }

            imageChunks.AddRange(chunk.Data.ToByteArray());

            if (chunk.IsLast)
                break;
        }

        var processedImage = ApplyFilter(imageChunks.ToArray());

        const int chunkSize = 16 * 1024;
        int totalChunks = (int)Math.Ceiling((double)processedImage.Length / chunkSize);

        for (int i = 0; i < totalChunks; i++)
        {
            var chunkData = processedImage.Skip(i * chunkSize).Take(chunkSize).ToArray();

            await responseStream.WriteAsync(new ImageFilterChunk
            {
                ImageId = _imageId,
                Data = Google.Protobuf.ByteString.CopyFrom(chunkData),
                ChunkNumber = i,
                IsLast = (i == totalChunks - 1)
            });
        }
    }

    private byte[] ApplyFilter(byte[] imageBytes)
    {
        using var inputStream = new MemoryStream(imageBytes);
        using var original = new Bitmap(inputStream);
        Bitmap filtered;

        switch (_filterType)
        {
            case TipoFiltro.Grayscale:
                filtered = ApplyGrayscale(original);
                break;
            case TipoFiltro.Sepia:
                filtered = ApplySepia(original);
                break;
            case TipoFiltro.Negative:
                filtered = ApplyNegative(original);
                break;
            default:
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Filtro não suportado"));
        }

        using var outputStream = new MemoryStream();
        filtered.Save(outputStream, ImageFormat.Jpeg);
        return outputStream.ToArray();
    }

    // Filtros (sem alteração)
    private Bitmap ApplyGrayscale(Bitmap original)
    {
        var result = new Bitmap(original.Width, original.Height);
        for (int y = 0; y < original.Height; y++)
            for (int x = 0; x < original.Width; x++)
            {
                var pixel = original.GetPixel(x, y);
                int gray = (int)((pixel.R + pixel.G + pixel.B) / 3);
                result.SetPixel(x, y, Color.FromArgb(gray, gray, gray));
            }
        return result;
    }

    private Bitmap ApplySepia(Bitmap original)
    {
        var result = new Bitmap(original.Width, original.Height);
        for (int y = 0; y < original.Height; y++)
            for (int x = 0; x < original.Width; x++)
            {
                var p = original.GetPixel(x, y);
                int tr = (int)(0.393 * p.R + 0.769 * p.G + 0.189 * p.B);
                int tg = (int)(0.349 * p.R + 0.686 * p.G + 0.168 * p.B);
                int tb = (int)(0.272 * p.R + 0.534 * p.G + 0.131 * p.B);

                tr = Math.Min(255, tr);
                tg = Math.Min(255, tg);
                tb = Math.Min(255, tb);

                result.SetPixel(x, y, Color.FromArgb(tr, tg, tb));
            }
        return result;
    }

    private Bitmap ApplyNegative(Bitmap original)
    {
        var result = new Bitmap(original.Width, original.Height);
        for (int y = 0; y < original.Height; y++)
            for (int x = 0; x < original.Width; x++)
            {
                var p = original.GetPixel(x, y);
                result.SetPixel(x, y, Color.FromArgb(255 - p.R, 255 - p.G, 255 - p.B));
            }
        return result;
    }
}
