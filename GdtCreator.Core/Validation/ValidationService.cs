using System.Globalization;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;

namespace GdtCreator.Core.Validation;

public sealed class ValidationService : IValidationService
{
    public ValidationResult Validate(GeometricToleranceSpec spec)
    {
        ArgumentNullException.ThrowIfNull(spec);

        var errors = new List<string>();

        if (!IsPositiveTolerance(spec.ToleranceValue))
        {
            errors.Add("Tolerance value must be a positive number.");
        }

        var datumReferences = spec.DatumReferences
            .Where(reference => !string.IsNullOrWhiteSpace(reference.Label))
            .Select(reference => reference.Label.Trim().ToUpperInvariant())
            .ToList();

        if (datumReferences.Count > 3)
        {
            errors.Add("A maximum of three datum references is supported.");
        }

        if (datumReferences.Any(label => label.Length == 0 || label.Length > 4 || !label.All(c => char.IsLetterOrDigit(c) || c == '-')))
        {
            errors.Add("Datum labels must contain letters, digits, or hyphens (max 4 characters).");
        }

        if (datumReferences.Distinct(StringComparer.Ordinal).Count() != datumReferences.Count)
        {
            errors.Add("Datum labels must be unique.");
        }

        if (RequiresDatum(spec.Characteristic) && datumReferences.Count == 0)
        {
            errors.Add("This geometric characteristic requires at least one datum reference.");
        }

        if (ForbidsDatum(spec.Characteristic) && datumReferences.Count > 0)
        {
            errors.Add("This geometric characteristic does not use datum references.");
        }

        return errors.Count == 0
            ? ValidationResult.Success()
            : ValidationResult.Failure(errors);
    }

    private static bool IsPositiveTolerance(string value)
    {
        return TryParseToleranceValue(value, out var parsedValue) && parsedValue > 0;
    }

    private static bool TryParseToleranceValue(string value, out decimal parsedValue)
    {
        var trimmed = value.Trim();
        var styles = NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign;

        if (decimal.TryParse(trimmed, styles, CultureInfo.InvariantCulture, out parsedValue))
        {
            return true;
        }

        if (decimal.TryParse(trimmed, styles, CultureInfo.CurrentCulture, out parsedValue))
        {
            return true;
        }

        if (decimal.TryParse(trimmed, styles, CultureInfo.GetCultureInfo("fr-FR"), out parsedValue))
        {
            return true;
        }

        return decimal.TryParse(trimmed.Replace(',', '.'), styles, CultureInfo.InvariantCulture, out parsedValue);
    }

    private static bool RequiresDatum(GeometricCharacteristic characteristic)
    {
        return characteristic is GeometricCharacteristic.Parallelism
            or GeometricCharacteristic.Perpendicularity
            or GeometricCharacteristic.Angularity
            or GeometricCharacteristic.Position
            or GeometricCharacteristic.Concentricity
            or GeometricCharacteristic.Symmetry
            or GeometricCharacteristic.CircularRunout
            or GeometricCharacteristic.TotalRunout;
    }

    private static bool ForbidsDatum(GeometricCharacteristic characteristic)
    {
        return characteristic is GeometricCharacteristic.Straightness
            or GeometricCharacteristic.Flatness
            or GeometricCharacteristic.Circularity
            or GeometricCharacteristic.Cylindricity;
    }
}

