using ColorAnalyzer.Enum;
using ColorAnalyzer.Util;
using Grpc.Core;
using ImageFilter;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.IO;

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
        using var image = Image.Load<Rgba32>(imageBytes);

        switch (_filterType)
        {
            case TipoFiltro.Grayscale:
                image.Mutate(ctx => ctx.Grayscale());
                break;
            case TipoFiltro.Sepia:
                image.Mutate(ctx => ctx.Sepia());
                break;
            case TipoFiltro.Negative:
                image.Mutate(ctx => ctx.Invert());
                break;
            default:
                throw new RpcException(new Status(StatusCode.InvalidArgument, "Filtro não suportado"));
        }

        using var outputStream = new MemoryStream();
        image.Save(outputStream, new JpegEncoder());
        return outputStream.ToArray();
    }
}
