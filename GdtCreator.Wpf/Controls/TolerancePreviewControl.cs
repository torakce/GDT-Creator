using System.Windows;
using System.Windows.Media;
using GdtCreator.Core.Rendering;
using GdtCreator.Wpf.Rendering;

namespace GdtCreator.Wpf.Controls;

public sealed class TolerancePreviewControl : FrameworkElement
{
    public static readonly DependencyProperty RenderModelProperty = DependencyProperty.Register(
        nameof(RenderModel),
        typeof(ToleranceRenderModel),
        typeof(TolerancePreviewControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public ToleranceRenderModel? RenderModel
    {
        get => (ToleranceRenderModel?)GetValue(RenderModelProperty);
        set => SetValue(RenderModelProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var frameBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
        var borderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(202, 215, 228));
        frameBrush.Freeze();
        borderBrush.Freeze();

        drawingContext.DrawRoundedRectangle(frameBrush, new System.Windows.Media.Pen(borderBrush, 1d), new Rect(0, 0, ActualWidth, ActualHeight), 18d, 18d);

        if (RenderModel is null)
        {
            var typeface = new Typeface(new System.Windows.Media.FontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.SemiCondensed);
            var text = new FormattedText("The tolerance frame preview will appear here.", System.Globalization.CultureInfo.InvariantCulture, System.Windows.FlowDirection.LeftToRight, typeface, 18d, new SolidColorBrush(System.Windows.Media.Color.FromRgb(89, 102, 116)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
            drawingContext.DrawText(text, new System.Windows.Point((ActualWidth - text.Width) / 2d, (ActualHeight - text.Height) / 2d));
            return;
        }

        SymbolRenderer.DrawModel(drawingContext, RenderModel, new Rect(18d, 18d, Math.Max(ActualWidth - 36d, 40d), Math.Max(ActualHeight - 36d, 40d)), VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
