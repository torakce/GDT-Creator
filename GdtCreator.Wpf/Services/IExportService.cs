using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Services;

public interface IExportService
{
    void ExportPng(ToleranceRenderModel model, string filePath, double scale);

    void ExportSvg(ToleranceRenderModel model, string filePath, double scale);

    void ExportEmf(ToleranceRenderModel model, string filePath, double scale);
}
