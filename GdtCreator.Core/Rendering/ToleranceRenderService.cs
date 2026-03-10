using System.Globalization;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;

namespace GdtCreator.Core.Rendering;

public sealed class ToleranceRenderService : IRenderService
{
    public ToleranceRenderModel Render(GeometricToleranceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var cells = new List<ToleranceCell>
        {
            CreateCell([RenderToken.ForSymbol(ToCharacteristicSymbol(spec.Characteristic))], preferSquare: true),
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

            cells.Add(CreateCell(datumTokens, preferSquare: datumTokens.Count == 1));
        }

        return new ToleranceRenderModel
        {
            Cells = cells,
            Height = RenderMetrics.CellHeight,
            Width = cells.Sum(cell => cell.Width),
            Padding = RenderMetrics.HorizontalPadding,
            StrokeThickness = RenderMetrics.StrokeThickness
        };
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
            tokens.Add(RenderToken.ForSymbol(RenderSymbol.SphericalRadius));
        }

        tokens.Add(RenderToken.ForText(NormalizeToleranceValue(spec.ToleranceValue)));

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

        if (!string.IsNullOrWhiteSpace(spec.UnequallyDisposedValue))
        {
            tokens.Add(RenderToken.ForText("UZ"));
            tokens.Add(RenderToken.ForText(NormalizeToleranceValue(spec.UnequallyDisposedValue)));
        }

        if (spec.CombinedZone)
        {
            tokens.Add(RenderToken.ForText("CZ"));
        }

        return tokens;
    }

    private static string NormalizeToleranceValue(string toleranceValue)
    {
        if (decimal.TryParse(
                toleranceValue,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var invariantValue))
        {
            return invariantValue.ToString("0.###", CultureInfo.InvariantCulture);
        }

        if (decimal.TryParse(
                toleranceValue,
                NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                CultureInfo.CurrentCulture,
                out var currentValue))
        {
            return currentValue.ToString("0.###", CultureInfo.InvariantCulture);
        }

        return toleranceValue.Trim();
    }

    private static ToleranceCell CreateCell(IReadOnlyList<RenderToken> tokens, bool preferSquare = false)
    {
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
