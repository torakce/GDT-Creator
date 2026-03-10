using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Models;

public sealed class OptionItem<T>
{
    public required string Label { get; init; }

    public required T Value { get; init; }

    public string? ShortLabel { get; init; }

    public string? Hint { get; init; }

    public string? Category { get; init; }

    public RenderSymbol? Symbol { get; init; }

    public string DisplayLabel => ShortLabel ?? Label;
}
