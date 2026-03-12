namespace GdtCreator.Core.Rendering;

public sealed class ToleranceRenderModel
{
    public required IReadOnlyList<ToleranceCell> Cells { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public required double FrameWidth { get; init; }

    public required double FrameHeight { get; init; }

    public required string ContentColorHex { get; init; }

    public string? TopText { get; init; }

    public string? BottomText { get; init; }

    public double TopTextHeight { get; init; }

    public double BottomTextHeight { get; init; }

    public double TextGap { get; init; } = 6d;

    public double Padding { get; init; } = 10d;

    public double StrokeThickness { get; init; } = 1.5d;
}
