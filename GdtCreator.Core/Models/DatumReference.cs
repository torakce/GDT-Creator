using GdtCreator.Core.Enums;

namespace GdtCreator.Core.Models;

public sealed class DatumReference
{
    public string Label { get; set; } = string.Empty;

    public DatumMaterialCondition MaterialCondition { get; set; }

    public DatumFeatureSymbolStyle FeatureSymbolStyle { get; set; } = DatumFeatureSymbolStyle.Direct;
}
