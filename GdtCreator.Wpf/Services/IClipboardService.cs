using System.Windows;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Services;

public interface IClipboardService
{
    void CopyImage(ToleranceRenderModel model, double scale);

    void CopyVector(ToleranceRenderModel model, double scale);
}
