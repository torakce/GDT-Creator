namespace GdtCreator.Core.Rendering;

public sealed class ToleranceCell
{
    public required IReadOnlyList<RenderToken> Tokens { get; init; }

    public required double Width { get; init; }
}
