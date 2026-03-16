using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Services;

public interface IExportService
{
    Task ExportPngAsync(ToleranceRenderModel model, Stream output, double scale);

    Task ExportSvgAsync(ToleranceRenderModel model, Stream output, double scale);

    Task ExportEmfAsync(ToleranceRenderModel model, Stream output, double scale);
}
