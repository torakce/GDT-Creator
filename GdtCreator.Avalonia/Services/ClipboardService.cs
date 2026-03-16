using Avalonia.Controls;
using Avalonia.Input;
using GdtCreator.Avalonia.Rendering;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Services;

public sealed class ClipboardService : IClipboardService
{
    private static readonly DataFormat<string> SvgFormat = DataFormat.CreateStringPlatformFormat("image/svg+xml");
    private readonly Window _window;

    public ClipboardService(Window window)
    {
        _window = window;
    }

    public async Task CopyImageAsync(ToleranceRenderModel model, double scale)
    {
        var clipboard = _window.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var payload = new DataTransfer();
        var item = new DataTransferItem();
        item.SetBitmap(SymbolRenderer.CreateBitmap(model, scale));
        payload.Add(item);
        await clipboard.SetDataAsync(payload);
        await clipboard.FlushAsync();
    }

    public async Task CopyVectorAsync(ToleranceRenderModel model, double scale)
    {
        var clipboard = _window.Clipboard;
        if (clipboard is null)
        {
            return;
        }

        var payload = new DataTransfer();
        var item = new DataTransferItem();
        item.SetBitmap(SymbolRenderer.CreateBitmap(model, scale));
        item.Set(SvgFormat, SymbolRenderer.BuildSvg(model, scale));
        payload.Add(item);
        await clipboard.SetDataAsync(payload);
        await clipboard.FlushAsync();
    }
}
