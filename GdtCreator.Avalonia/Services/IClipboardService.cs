using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Services;

public interface IClipboardService
{
    Task CopyImageAsync(ToleranceRenderModel model, double scale);

    Task CopyVectorAsync(ToleranceRenderModel model, double scale);
}
