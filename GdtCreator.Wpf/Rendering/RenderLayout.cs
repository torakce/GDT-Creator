using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Rendering;

public static class RenderLayout
{
    public const double CellHeight = RenderMetrics.CellHeight;
    public const double HorizontalPadding = RenderMetrics.HorizontalPadding;
    public const double TokenGap = RenderMetrics.TokenGap;
    public const double TextFontSize = RenderMetrics.TextFontSize;

    public static double GetTokenAdvance(RenderToken token)
    {
        return RenderMetrics.MeasureTokenAdvance(token);
    }

    public static double GetTokenSequenceWidth(IReadOnlyList<RenderToken> tokens)
    {
        return RenderMetrics.MeasureTokenSequenceWidth(tokens);
    }
}
