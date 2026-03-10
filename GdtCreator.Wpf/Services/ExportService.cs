using System.IO;
using System.Windows.Media.Imaging;
using GdtCreator.Core.Rendering;
using GdtCreator.Wpf.Rendering;

namespace GdtCreator.Wpf.Services;

public sealed class ExportService : IExportService
{
    public void ExportPng(ToleranceRenderModel model, string filePath, double scale)
    {
        var bitmap = SymbolRenderer.CreateBitmap(model, scale);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(filePath);
        encoder.Save(stream);
    }

    public void ExportSvg(ToleranceRenderModel model, string filePath, double scale)
    {
        File.WriteAllText(filePath, SymbolRenderer.BuildSvg(model, scale));
    }

    public void ExportEmf(ToleranceRenderModel model, string filePath, double scale)
    {
        File.WriteAllBytes(filePath, SymbolRenderer.BuildEmf(model, scale));
    }
}
