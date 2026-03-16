using System.Text;
using GdtCreator.Avalonia.Rendering;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Services;

public sealed class ExportService : IExportService
{
    public Task ExportPngAsync(ToleranceRenderModel model, Stream output, double scale)
    {
        using var bitmap = SymbolRenderer.CreateBitmap(model, scale);
        bitmap.Save(output);
        return Task.CompletedTask;
    }

    public async Task ExportSvgAsync(ToleranceRenderModel model, Stream output, double scale)
    {
        await using var writer = new StreamWriter(output, new UTF8Encoding(false), leaveOpen: true);
        await writer.WriteAsync(SymbolRenderer.BuildSvg(model, scale));
        await writer.FlushAsync();
    }

    public Task ExportEmfAsync(ToleranceRenderModel model, Stream output, double scale)
    {
        var bytes = SymbolRenderer.BuildEmf(model, scale);
        return output.WriteAsync(bytes, 0, bytes.Length);
    }
}
