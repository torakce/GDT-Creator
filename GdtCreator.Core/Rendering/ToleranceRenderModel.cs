namespace GdtCreator.Core.Rendering;

public sealed class ToleranceRenderModel
{
    public required IReadOnlyList<ToleranceCell> Cells { get; init; }

    public required double Width { get; init; }

    public required double Height { get; init; }

    public double Padding { get; init; } = 10d;

    public double StrokeThickness { get; init; } = 1.5d;
}
