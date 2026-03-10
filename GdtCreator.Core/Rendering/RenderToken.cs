namespace GdtCreator.Core.Rendering;

public sealed class RenderToken
{
    private RenderToken()
    {
    }

    public string? Text { get; private init; }

    public RenderSymbol? Symbol { get; private init; }

    public bool IsSymbol => Symbol.HasValue;

    public static RenderToken ForText(string value)
    {
        return new RenderToken { Text = value };
    }

    public static RenderToken ForSymbol(RenderSymbol symbol)
    {
        return new RenderToken { Symbol = symbol };
    }
}
