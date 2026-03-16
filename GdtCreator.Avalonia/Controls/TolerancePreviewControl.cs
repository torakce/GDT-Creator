using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GdtCreator.Avalonia.Rendering;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Controls;

public sealed class TolerancePreviewControl : Control
{
    public static readonly StyledProperty<ToleranceRenderModel?> RenderModelProperty =
        AvaloniaProperty.Register<TolerancePreviewControl, ToleranceRenderModel?>(nameof(RenderModel));

    static TolerancePreviewControl()
    {
        AffectsRender<TolerancePreviewControl>(RenderModelProperty);
    }

    public ToleranceRenderModel? RenderModel
    {
        get => GetValue(RenderModelProperty);
        set => SetValue(RenderModelProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var bounds = new Rect(Bounds.Size);
        var background = new SolidColorBrush(Color.FromRgb(255, 255, 255));
        var border = new Pen(new SolidColorBrush(Color.FromRgb(202, 215, 228)), 1d);
        context.DrawRectangle(background, border, bounds, 18d, 18d);

        if (RenderModel is null)
        {
            var text = new FormattedText(
                "The tolerance frame preview will appear here.",
                System.Globalization.CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                new Typeface("Bahnschrift"),
                18d,
                new SolidColorBrush(Color.FromRgb(89, 102, 116)));
            context.DrawText(text, new Point((Bounds.Width - text.Width) / 2d, (Bounds.Height - text.Height) / 2d));
            return;
        }

        SymbolRenderer.DrawModel(context, RenderModel, new Rect(18d, 18d, Math.Max(Bounds.Width - 36d, 40d), Math.Max(Bounds.Height - 36d, 40d)), 1d);
    }
}
