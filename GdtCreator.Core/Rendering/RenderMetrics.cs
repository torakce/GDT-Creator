namespace GdtCreator.Core.Rendering;

public static class RenderMetrics
{
    public const double CellHeight = 32d;
    public const double CharacteristicCellWidth = 32d;
    public const double MinimumCellWidth = 32d;
    public const double HorizontalPadding = 6d;
    public const double TokenGap = 4d;
    public const double StrokeThickness = 1.4d;
    public const double TextFontSize = 15d;

    public static double MeasureTokenAdvance(RenderToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        if (token.IsSymbol)
        {
            return token.Symbol switch
            {
                RenderSymbol.SphericalDiameter => 28d,
                RenderSymbol.SphericalRadius => 32d,
                RenderSymbol.DatumFeatureDirect => 20d,
                RenderSymbol.DatumFeatureLeaderLeft => 24d,
                RenderSymbol.DatumFeatureLeaderRight => 24d,
                RenderSymbol.DatumFeatureLeaderDown => 24d,
                RenderSymbol.MaximumMaterialCondition or RenderSymbol.LeastMaterialCondition or RenderSymbol.ProjectedToleranceZone or RenderSymbol.FreeState => 22d,
                _ => 20d
            };
        }

        var text = token.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return TextFontSize * 0.8d;
        }

        var widthFactor = text.All(char.IsLetter) ? 0.62d : 0.58d;
        return Math.Max(TextFontSize * 0.85d, text.Length * TextFontSize * widthFactor);
    }

    public static double MeasureTokenSequenceWidth(IReadOnlyList<RenderToken> tokens)
    {
        if (tokens.Count == 0)
        {
            return 0d;
        }

        var width = 0d;
        for (var index = 0; index < tokens.Count; index++)
        {
            width += MeasureTokenAdvance(tokens[index]);
            if (index < tokens.Count - 1)
            {
                width += TokenGap;
            }
        }

        return width;
    }

    public static double MeasureCellWidth(IReadOnlyList<RenderToken> tokens, bool preferSquare = false)
    {
        var contentWidth = MeasureTokenSequenceWidth(tokens) + (HorizontalPadding * 2d);
        var minimum = preferSquare ? CharacteristicCellWidth : MinimumCellWidth;
        return Math.Max(minimum, contentWidth);
    }
}
