using GdtCreator.Core.Enums;

namespace GdtCreator.Core.Models;

public sealed class GeometricToleranceSpec
{
    public GeometricCharacteristic Characteristic { get; set; } = GeometricCharacteristic.Position;

    public string ToleranceValue { get; set; } = "0.10";

    public ToleranceZoneModifier ZoneModifier { get; set; } = ToleranceZoneModifier.Diameter;

    public ToleranceMaterialCondition ToleranceMaterialCondition { get; set; }

    public bool ProjectedToleranceZone { get; set; }

    public bool FreeState { get; set; }

    public bool CombinedZone { get; set; }

    public string? UnequallyDisposedValue { get; set; }

    public List<DatumReference> DatumReferences { get; set; } =
    [
        new DatumReference { Label = "A" },
        new DatumReference { Label = "B" },
        new DatumReference { Label = "C" }
    ];

    public string Standard => "ISO GPS";

    public static GeometricToleranceSpec CreateDefault()
    {
        return new GeometricToleranceSpec();
    }
}
