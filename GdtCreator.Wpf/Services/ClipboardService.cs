using GdtCreator.Core.Rendering;
using GdtCreator.Wpf.Rendering;

namespace GdtCreator.Wpf.Services;

public sealed class ClipboardService : IClipboardService
{
    public void CopyImage(ToleranceRenderModel model, double scale)
    {
        var dataObject = new System.Windows.DataObject();
        dataObject.SetImage(SymbolRenderer.CreateBitmap(model, scale));
        System.Windows.Clipboard.SetDataObject(dataObject, true);
    }

    public void CopyVector(ToleranceRenderModel model, double scale)
    {
        var dataObject = new System.Windows.DataObject();
        dataObject.SetImage(SymbolRenderer.CreateBitmap(model, scale));
        dataObject.SetData("image/svg+xml", SymbolRenderer.BuildSvg(model, scale));
        System.Windows.Clipboard.SetDataObject(dataObject, true);
    }
}
