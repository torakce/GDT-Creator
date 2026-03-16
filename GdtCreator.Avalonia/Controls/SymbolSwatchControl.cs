using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using GdtCreator.Avalonia.Rendering;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Controls;

public sealed class SymbolSwatchControl : Control
{
    public static readonly StyledProperty<RenderSymbol> SymbolProperty =
        AvaloniaProperty.Register<SymbolSwatchControl, RenderSymbol>(nameof(Symbol), RenderSymbol.Position);

    public static readonly StyledProperty<double> StrokeThicknessProperty =
        AvaloniaProperty.Register<SymbolSwatchControl, double>(nameof(StrokeThickness), 2d);

    static SymbolSwatchControl()
    {
        AffectsRender<SymbolSwatchControl>(SymbolProperty, StrokeThicknessProperty);
    }

    public RenderSymbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public double StrokeThickness
    {
        get => GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        var side = Math.Max(0d, Math.Min(Bounds.Width, Bounds.Height) - 4d);
        var x = (Bounds.Width - side) / 2d;
        var y = (Bounds.Height - side) / 2d;
        SymbolRenderer.DrawSymbolIcon(context, Symbol, new Rect(x, y, side, side), StrokeThickness);
    }
}
