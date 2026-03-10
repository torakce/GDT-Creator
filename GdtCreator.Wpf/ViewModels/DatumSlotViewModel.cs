using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;

namespace GdtCreator.Wpf.ViewModels;

public sealed class DatumSlotViewModel : ViewModelBase
{
    private string _label = string.Empty;
    private DatumMaterialCondition _materialCondition;
    private DatumFeatureSymbolStyle _featureSymbolStyle = DatumFeatureSymbolStyle.Direct;

    public string Ordinal { get; init; } = "";

    public string Label
    {
        get => _label;
        set => SetProperty(ref _label, value.ToUpperInvariant());
    }

    public DatumMaterialCondition MaterialCondition
    {
        get => _materialCondition;
        set => SetProperty(ref _materialCondition, value);
    }

    public DatumFeatureSymbolStyle FeatureSymbolStyle
    {
        get => _featureSymbolStyle;
        set => SetProperty(ref _featureSymbolStyle, value);
    }

    public DatumReference ToDatumReference()
    {
        return new DatumReference
        {
            Label = Label.Trim(),
            MaterialCondition = MaterialCondition,
            FeatureSymbolStyle = FeatureSymbolStyle
        };
    }

    public void Load(DatumReference datumReference)
    {
        Label = datumReference.Label;
        MaterialCondition = datumReference.MaterialCondition;
        FeatureSymbolStyle = datumReference.FeatureSymbolStyle;
    }
}
