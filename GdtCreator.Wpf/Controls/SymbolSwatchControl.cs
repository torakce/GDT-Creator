using System.Windows;
using System.Windows.Media;
using GdtCreator.Core.Rendering;
using GdtCreator.Wpf.Rendering;

namespace GdtCreator.Wpf.Controls;

public sealed class SymbolSwatchControl : FrameworkElement
{
    public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register(
        nameof(Symbol),
        typeof(RenderSymbol),
        typeof(SymbolSwatchControl),
        new FrameworkPropertyMetadata(RenderSymbol.Position, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register(
        nameof(StrokeThickness),
        typeof(double),
        typeof(SymbolSwatchControl),
        new FrameworkPropertyMetadata(2d, FrameworkPropertyMetadataOptions.AffectsRender));

    public RenderSymbol Symbol
    {
        get => (RenderSymbol)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        var side = Math.Max(0d, Math.Min(ActualWidth, ActualHeight) - 4d);
        var x = (ActualWidth - side) / 2d;
        var y = (ActualHeight - side) / 2d;
        SymbolRenderer.DrawSymbolIcon(
            drawingContext,
            Symbol,
            new Rect(x, y, side, side),
            StrokeThickness,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
    }
}
