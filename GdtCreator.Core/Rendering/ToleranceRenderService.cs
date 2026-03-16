using System.Globalization;
using System.Text.RegularExpressions;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;

namespace GdtCreator.Core.Rendering;

public sealed class ToleranceRenderService : IRenderService
{
    private static readonly Regex InlineTokenPattern = new(@"UZ|CZ|[+-]?(?:\d+(?:[\.,]\d+)?|[\.,]\d+)|[A-Za-z]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private const double FrameHeight = RenderMetrics.CellHeight;
    private const double TextLineHeight = 24d;
    private const double TextGap = 8d;
    private const string DefaultContentColorHex = "#000000";

    public ToleranceRenderModel Render(GeometricToleranceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var cells = new List<ToleranceCell>
        {
            CreateCell([RenderToken.ForSymbol(ToCharacteristicSymbol(spec.Characteristic))]),
            CreateCell(CreateToleranceTokens(spec))
        };

        foreach (var datum in spec.DatumReferences.Where(HasDatumReference))
        {
            var datumTokens = new List<RenderToken> { RenderToken.ForText(datum.Label.Trim().ToUpperInvariant()) };
            if (datum.MaterialCondition == DatumMaterialCondition.MaximumMaterialCondition)
            {
                datumTokens.Add(RenderToken.ForSymbol(RenderSymbol.MaximumMaterialCondition));
            }
            else if (datum.MaterialCondition == DatumMaterialCondition.LeastMaterialCondition)
            {
                datumTokens.Add(RenderToken.ForSymbol(RenderSymbol.LeastMaterialCondition));
            }

            cells.Add(CreateCell(datumTokens));
        }

        var topText = NormalizeFreeText(spec.TopText);
        var bottomText = NormalizeFreeText(spec.BottomText);
        var contentColorHex = NormalizeColorHex(spec.ContentColorHex);
        var frameWidth = cells.Sum(cell => cell.Width);
        var measuredTextWidth = Math.Max(EstimateFreeTextWidth(topText), EstimateFreeTextWidth(bottomText));
        var totalWidth = Math.Max(frameWidth, measuredTextWidth);
        var topTextHeight = topText is null ? 0d : TextLineHeight;
        var bottomTextHeight = bottomText is null ? 0d : TextLineHeight;
        var totalHeight = FrameHeight
            + topTextHeight
            + bottomTextHeight
            + (topTextHeight > 0d ? TextGap : 0d)
            + (bottomTextHeight > 0d ? TextGap : 0d);

        return new ToleranceRenderModel
        {
            Cells = cells,
            Width = totalWidth,
            Height = totalHeight,
            FrameWidth = frameWidth,
            FrameHeight = FrameHeight,
            ContentColorHex = contentColorHex,
            TopText = topText,
            BottomText = bottomText,
            TopTextHeight = topTextHeight,
            BottomTextHeight = bottomTextHeight,
            TextGap = TextGap,
            StrokeThickness = RenderMetrics.StrokeThickness
        };
    }

    private static string? NormalizeFreeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultContentColorHex;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith('#'))
        {
            trimmed = trimmed[1..];
        }

        if (trimmed.Length == 3 && trimmed.All(IsHexDigit))
        {
            trimmed = string.Concat(trimmed.Select(ch => new string(ch, 2)));
        }

        return trimmed.Length == 6 && trimmed.All(IsHexDigit)
            ? $"#{trimmed.ToUpperInvariant()}"
            : DefaultContentColorHex;
    }

    private static bool IsHexDigit(char character)
    {
        return character is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';
    }

    private static double EstimateFreeTextWidth(string? text)
    {
        return string.IsNullOrWhiteSpace(text)
            ? 0d
            : Math.Max(90d, text.Trim().Length * 13d);
    }

    private static bool HasDatumReference(DatumReference datumReference)
    {
        return !string.IsNullOrWhiteSpace(datumReference.Label);
    }

    private static IReadOnlyList<RenderToken> CreateToleranceTokens(GeometricToleranceSpec spec)
    {
        var tokens = new List<RenderToken>();

        if (spec.ZoneModifier == ToleranceZoneModifier.Diameter)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.Diameter));
        }
        else if (spec.ZoneModifier == ToleranceZoneModifier.SphericalDiameter)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.SphericalDiameter));
        }
        else if (spec.ZoneModifier == ToleranceZoneModifier.SphericalRadius)
        {
            tokens.Add(RenderToken.ForText("SR"));
        }

        tokens.AddRange(ParseInlineToleranceTokens(spec.ToleranceValue));

        if (spec.ToleranceMaterialCondition == ToleranceMaterialCondition.MaximumMaterialCondition)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.MaximumMaterialCondition));
        }
        else if (spec.ToleranceMaterialCondition == ToleranceMaterialCondition.LeastMaterialCondition)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.LeastMaterialCondition));
        }

        if (spec.ProjectedToleranceZone)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.ProjectedToleranceZone));
        }

        if (spec.FreeState)
        {
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.FreeState));
        }

        return tokens;
    }

    private static IReadOnlyList<RenderToken> ParseInlineToleranceTokens(string toleranceValue)
    {
        var trimmed = toleranceValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return [];
        }

        var matches = InlineTokenPattern.Matches(trimmed);
        if (matches.Count == 0)
        {
            return [RenderToken.ForText(NormalizeToleranceValue(trimmed))];
        }

        var tokens = new List<RenderToken>(matches.Count);
        foreach (Match match in matches)
        {
            var token = match.Value.Trim();
            if (string.IsNullOrWhiteSpace(token))
            {
                continue;
            }

            if (TryParseToleranceValue(token, out _))
            {
                tokens.Add(RenderToken.ForText(NormalizeToleranceValue(token)));
            }
            else
            {
                tokens.Add(RenderToken.ForText(token.ToUpperInvariant()));
            }
        }

        return tokens;
    }

    private static string NormalizeToleranceValue(string toleranceValue)
    {
        var trimmed = toleranceValue.Trim();
        if (!TryParseToleranceValue(trimmed, out _))
        {
            return trimmed.ToUpperInvariant();
        }

        var normalized = trimmed.Replace(',', '.');
        if (normalized.StartsWith(".", StringComparison.Ordinal))
        {
            return $"0{normalized}";
        }

        if (normalized.StartsWith("-.", StringComparison.Ordinal))
        {
            return $"-0{normalized[1..]}";
        }

        return normalized;
    }

    internal static bool TryParseToleranceValue(string toleranceValue, out decimal value)
    {
        var trimmed = toleranceValue.Trim();
        var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (decimal.TryParse(trimmed, styles, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        if (decimal.TryParse(trimmed, styles, CultureInfo.CurrentCulture, out value))
        {
            return true;
        }

        if (decimal.TryParse(trimmed, styles, CultureInfo.GetCultureInfo("fr-FR"), out value))
        {
            return true;
        }

        return decimal.TryParse(trimmed.Replace(',', '.'), styles, CultureInfo.InvariantCulture, out value);
    }

    private static ToleranceCell CreateCell(IReadOnlyList<RenderToken> tokens)
    {
        var preferSquare = tokens.Count == 1 && tokens[0].IsSymbol;
        return new ToleranceCell
        {
            Tokens = tokens,
            Width = RenderMetrics.MeasureCellWidth(tokens, preferSquare)
        };
    }

    private static RenderSymbol ToCharacteristicSymbol(GeometricCharacteristic characteristic)
    {
        return characteristic switch
        {
            GeometricCharacteristic.Straightness => RenderSymbol.Straightness,
            GeometricCharacteristic.Flatness => RenderSymbol.Flatness,
            GeometricCharacteristic.Circularity => RenderSymbol.Circularity,
            GeometricCharacteristic.Cylindricity => RenderSymbol.Cylindricity,
            GeometricCharacteristic.ProfileOfALine => RenderSymbol.ProfileOfALine,
            GeometricCharacteristic.ProfileOfASurface => RenderSymbol.ProfileOfASurface,
            GeometricCharacteristic.Parallelism => RenderSymbol.Parallelism,
            GeometricCharacteristic.Perpendicularity => RenderSymbol.Perpendicularity,
            GeometricCharacteristic.Angularity => RenderSymbol.Angularity,
            GeometricCharacteristic.Position => RenderSymbol.Position,
            GeometricCharacteristic.Concentricity => RenderSymbol.Concentricity,
            GeometricCharacteristic.Symmetry => RenderSymbol.Symmetry,
            GeometricCharacteristic.CircularRunout => RenderSymbol.CircularRunout,
            GeometricCharacteristic.TotalRunout => RenderSymbol.TotalRunout,
            _ => throw new ArgumentOutOfRangeException(nameof(characteristic), characteristic, null)
        };
    }
}
