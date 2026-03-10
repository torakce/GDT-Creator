using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;
using GdtCreator.Core.Rendering;
using GdtCreator.Core.Validation;
using GdtCreator.Wpf.Models;
using GdtCreator.Wpf.Services;

namespace GdtCreator.Wpf.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
    private readonly IValidationService _validationService;
    private readonly IRenderService _renderService;
    private readonly IExportService _exportService;
    private readonly IClipboardService _clipboardService;
    private readonly ISettingsService _settingsService;

    private OptionItem<GeometricCharacteristic> _selectedCharacteristic = null!;
    private OptionItem<ToleranceZoneModifier> _selectedZoneModifier = null!;
    private OptionItem<ToleranceMaterialCondition> _selectedToleranceMaterialCondition = null!;
    private OptionItem<double> _selectedScale = null!;
    private string _toleranceValue = "0.10";
    private bool _projectedToleranceZone;
    private bool _freeState;
    private bool _combinedZone;
    private string _unequallyDisposedValue = "";
    private bool _areDatumInputsEnabled;
    private string _datumSectionHint = "Enter up to three datum references.";
    private ToleranceRenderModel? _renderModel;
    private string _statusMessage = "Ready.";
    private bool _isRefreshing;

    public MainViewModel(
        IValidationService validationService,
        IRenderService renderService,
        IExportService exportService,
        IClipboardService clipboardService,
        ISettingsService settingsService)
    {
        _validationService = validationService;
        _renderService = renderService;
        _exportService = exportService;
        _clipboardService = clipboardService;
        _settingsService = settingsService;

        Characteristics = new ObservableCollection<OptionItem<GeometricCharacteristic>>
        {
            new() { Label = "Straightness", Value = GeometricCharacteristic.Straightness, Symbol = RenderSymbol.Straightness, Category = "FORM" },
            new() { Label = "Flatness", Value = GeometricCharacteristic.Flatness, Symbol = RenderSymbol.Flatness, Category = "FORM" },
            new() { Label = "Circularity", Value = GeometricCharacteristic.Circularity, Symbol = RenderSymbol.Circularity, Category = "FORM" },
            new() { Label = "Cylindricity", Value = GeometricCharacteristic.Cylindricity, Symbol = RenderSymbol.Cylindricity, Category = "FORM" },
            new() { Label = "Profile of a line", Value = GeometricCharacteristic.ProfileOfALine, Symbol = RenderSymbol.ProfileOfALine, ShortLabel = "Line profile", Category = "PROFILE" },
            new() { Label = "Profile of a surface", Value = GeometricCharacteristic.ProfileOfASurface, Symbol = RenderSymbol.ProfileOfASurface, ShortLabel = "Surface profile", Category = "PROFILE" },
            new() { Label = "Parallelism", Value = GeometricCharacteristic.Parallelism, Symbol = RenderSymbol.Parallelism, Category = "ORIENTATION" },
            new() { Label = "Perpendicularity", Value = GeometricCharacteristic.Perpendicularity, Symbol = RenderSymbol.Perpendicularity, ShortLabel = "Perpendicular", Category = "ORIENTATION" },
            new() { Label = "Angularity", Value = GeometricCharacteristic.Angularity, Symbol = RenderSymbol.Angularity, Category = "ORIENTATION" },
            new() { Label = "Position", Value = GeometricCharacteristic.Position, Symbol = RenderSymbol.Position, Category = "LOCATION" },
            new() { Label = "Concentricity", Value = GeometricCharacteristic.Concentricity, Symbol = RenderSymbol.Concentricity, ShortLabel = "Concentric", Category = "LOCATION" },
            new() { Label = "Symmetry", Value = GeometricCharacteristic.Symmetry, Symbol = RenderSymbol.Symmetry, Category = "LOCATION" },
            new() { Label = "Circular runout", Value = GeometricCharacteristic.CircularRunout, Symbol = RenderSymbol.CircularRunout, ShortLabel = "Runout", Category = "RUNOUT" },
            new() { Label = "Total runout", Value = GeometricCharacteristic.TotalRunout, Symbol = RenderSymbol.TotalRunout, ShortLabel = "Total runout", Category = "RUNOUT" }
        };

        ZoneModifiers = new ObservableCollection<OptionItem<ToleranceZoneModifier>>
        {
            new() { Label = "No modifier", ShortLabel = "None", Value = ToleranceZoneModifier.None },
            new() { Label = "Diameter", ShortLabel = "Diameter", Value = ToleranceZoneModifier.Diameter, Symbol = RenderSymbol.Diameter },
            new() { Label = "Spherical diameter", ShortLabel = "Spherical dia", Value = ToleranceZoneModifier.SphericalDiameter, Symbol = RenderSymbol.SphericalDiameter },
            new() { Label = "Spherical radius", ShortLabel = "Spherical rad", Value = ToleranceZoneModifier.SphericalRadius, Symbol = RenderSymbol.SphericalRadius }
        };

        ToleranceMaterialConditions = new ObservableCollection<OptionItem<ToleranceMaterialCondition>>
        {
            new() { Label = "RFS / no modifier", ShortLabel = "None", Value = ToleranceMaterialCondition.None },
            new() { Label = "Maximum material condition", ShortLabel = "MMC", Value = ToleranceMaterialCondition.MaximumMaterialCondition, Symbol = RenderSymbol.MaximumMaterialCondition },
            new() { Label = "Least material condition", ShortLabel = "LMC", Value = ToleranceMaterialCondition.LeastMaterialCondition, Symbol = RenderSymbol.LeastMaterialCondition }
        };

        DatumConditionOptions = new ObservableCollection<OptionItem<DatumMaterialCondition>>
        {
            new() { Label = "None", ShortLabel = "None", Value = DatumMaterialCondition.None },
            new() { Label = "MMC", ShortLabel = "MMC", Value = DatumMaterialCondition.MaximumMaterialCondition, Symbol = RenderSymbol.MaximumMaterialCondition },
            new() { Label = "LMC", ShortLabel = "LMC", Value = DatumMaterialCondition.LeastMaterialCondition, Symbol = RenderSymbol.LeastMaterialCondition }
        };

        DatumFeatureSymbolOptions = new ObservableCollection<OptionItem<DatumFeatureSymbolStyle>>
        {
            new() { Label = "Attached datum feature symbol", ShortLabel = "Direct", Value = DatumFeatureSymbolStyle.Direct, Symbol = RenderSymbol.DatumFeatureDirect },
            new() { Label = "Leader to the left", ShortLabel = "Left", Value = DatumFeatureSymbolStyle.LeaderLeft, Symbol = RenderSymbol.DatumFeatureLeaderLeft },
            new() { Label = "Leader to the right", ShortLabel = "Right", Value = DatumFeatureSymbolStyle.LeaderRight, Symbol = RenderSymbol.DatumFeatureLeaderRight },
            new() { Label = "Leader downward", ShortLabel = "Down", Value = DatumFeatureSymbolStyle.LeaderDown, Symbol = RenderSymbol.DatumFeatureLeaderDown }
        };

        ScaleOptions = new ObservableCollection<OptionItem<double>>
        {
            new() { Label = "1x", Value = 1d },
            new() { Label = "2x", Value = 2d },
            new() { Label = "4x", Value = 4d }
        };

        DatumSlots = new ObservableCollection<DatumSlotViewModel>
        {
            new() { Ordinal = "Primary" },
            new() { Ordinal = "Secondary" },
            new() { Ordinal = "Tertiary" }
        };

        foreach (var datumSlot in DatumSlots)
        {
            datumSlot.PropertyChanged += (_, _) => RefreshState();
        }

        ValidationErrors = new ObservableCollection<string>();

        CopyImageCommand = new RelayCommand(_ => ExecuteCopyImage(), _ => CanExportOrCopy());
        CopyVectorCommand = new RelayCommand(_ => ExecuteCopyVector(), _ => CanExportOrCopy());
        ExportPngCommand = new RelayCommand(_ => ExecuteExport("png"), _ => CanExportOrCopy());
        ExportSvgCommand = new RelayCommand(_ => ExecuteExport("svg"), _ => CanExportOrCopy());
        ExportEmfCommand = new RelayCommand(_ => ExecuteExport("emf"), _ => CanExportOrCopy());

        LoadSettings();
        RefreshState();
    }

    public ObservableCollection<OptionItem<GeometricCharacteristic>> Characteristics { get; }

    public ObservableCollection<OptionItem<ToleranceZoneModifier>> ZoneModifiers { get; }

    public ObservableCollection<OptionItem<ToleranceMaterialCondition>> ToleranceMaterialConditions { get; }

    public ObservableCollection<OptionItem<DatumMaterialCondition>> DatumConditionOptions { get; }

    public ObservableCollection<OptionItem<DatumFeatureSymbolStyle>> DatumFeatureSymbolOptions { get; }

    public ObservableCollection<OptionItem<double>> ScaleOptions { get; }

    public ObservableCollection<DatumSlotViewModel> DatumSlots { get; }

    public ObservableCollection<string> ValidationErrors { get; }

    public ICommand CopyImageCommand { get; }

    public ICommand CopyVectorCommand { get; }

    public ICommand ExportPngCommand { get; }

    public ICommand ExportSvgCommand { get; }

    public ICommand ExportEmfCommand { get; }

    public OptionItem<GeometricCharacteristic> SelectedCharacteristic
    {
        get => _selectedCharacteristic;
        set
        {
            if (SetProperty(ref _selectedCharacteristic, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public OptionItem<ToleranceZoneModifier> SelectedZoneModifier
    {
        get => _selectedZoneModifier;
        set
        {
            if (SetProperty(ref _selectedZoneModifier, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public OptionItem<ToleranceMaterialCondition> SelectedToleranceMaterialCondition
    {
        get => _selectedToleranceMaterialCondition;
        set
        {
            if (SetProperty(ref _selectedToleranceMaterialCondition, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public OptionItem<double> SelectedScale
    {
        get => _selectedScale;
        set
        {
            if (SetProperty(ref _selectedScale, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public string ToleranceValue
    {
        get => _toleranceValue;
        set
        {
            if (SetProperty(ref _toleranceValue, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public bool ProjectedToleranceZone
    {
        get => _projectedToleranceZone;
        set
        {
            if (SetProperty(ref _projectedToleranceZone, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public bool FreeState
    {
        get => _freeState;
        set
        {
            if (SetProperty(ref _freeState, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public bool CombinedZone
    {
        get => _combinedZone;
        set
        {
            if (SetProperty(ref _combinedZone, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public string UnequallyDisposedValue
    {
        get => _unequallyDisposedValue;
        set
        {
            if (SetProperty(ref _unequallyDisposedValue, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public bool AreDatumInputsEnabled
    {
        get => _areDatumInputsEnabled;
        private set => SetProperty(ref _areDatumInputsEnabled, value);
    }

    public string DatumSectionHint
    {
        get => _datumSectionHint;
        private set => SetProperty(ref _datumSectionHint, value);
    }

    public ToleranceRenderModel? RenderModel
    {
        get => _renderModel;
        private set => SetProperty(ref _renderModel, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            LastSpec = BuildSpec(),
            ExportScale = SelectedScale.Value
        });
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        var spec = settings.LastSpec ?? GeometricToleranceSpec.CreateDefault();

        _isRefreshing = true;
        SelectedCharacteristic = Characteristics.First(option => option.Value == spec.Characteristic);
        SelectedZoneModifier = ZoneModifiers.First(option => option.Value == spec.ZoneModifier);
        SelectedToleranceMaterialCondition = ToleranceMaterialConditions.First(option => option.Value == spec.ToleranceMaterialCondition);
        SelectedScale = ScaleOptions.FirstOrDefault(option => Math.Abs(option.Value - settings.ExportScale) < 0.001d) ?? ScaleOptions[1];
        ToleranceValue = spec.ToleranceValue;
        ProjectedToleranceZone = spec.ProjectedToleranceZone;
        FreeState = spec.FreeState;
        CombinedZone = spec.CombinedZone;
        UnequallyDisposedValue = spec.UnequallyDisposedValue ?? "";

        for (var index = 0; index < DatumSlots.Count; index++)
        {
            if (index < spec.DatumReferences.Count)
            {
                DatumSlots[index].Load(spec.DatumReferences[index]);
            }
        }

        _isRefreshing = false;
    }

    private void RefreshState()
    {
        AreDatumInputsEnabled = CharacteristicAllowsDatums(SelectedCharacteristic?.Value ?? GeometricCharacteristic.Position);
        DatumSectionHint = AreDatumInputsEnabled
            ? "Enter up to three datum references and choose the datum feature symbol direction."
            : "This characteristic does not use datum references.";

        var spec = BuildSpec();
        RenderModel = _renderService.Render(spec);

        var validation = _validationService.Validate(spec);
        ValidationErrors.Clear();
        foreach (var error in validation.Errors)
        {
            ValidationErrors.Add(error);
        }

        StatusMessage = validation.IsValid
            ? "Ready to copy or export."
            : validation.Errors.First();

        NotifyCommands();
    }

    private GeometricToleranceSpec BuildSpec()
    {
        var characteristic = SelectedCharacteristic?.Value ?? GeometricCharacteristic.Position;
        var datumReferences = AreDatumInputsEnabled
            ? DatumSlots.Select(slot => slot.ToDatumReference()).ToList()
            : [];

        return new GeometricToleranceSpec
        {
            Characteristic = characteristic,
            ToleranceValue = ToleranceValue,
            ZoneModifier = SelectedZoneModifier?.Value ?? ToleranceZoneModifier.None,
            ToleranceMaterialCondition = SelectedToleranceMaterialCondition?.Value ?? ToleranceMaterialCondition.None,
            ProjectedToleranceZone = ProjectedToleranceZone,
            FreeState = FreeState,
            CombinedZone = CombinedZone,
            UnequallyDisposedValue = string.IsNullOrWhiteSpace(UnequallyDisposedValue) ? null : UnequallyDisposedValue,
            DatumReferences = datumReferences
        };
    }

    private static bool CharacteristicAllowsDatums(GeometricCharacteristic characteristic)
    {
        return characteristic is not GeometricCharacteristic.Straightness
            and not GeometricCharacteristic.Flatness
            and not GeometricCharacteristic.Circularity
            and not GeometricCharacteristic.Cylindricity;
    }

    private bool CanExportOrCopy()
    {
        return RenderModel is not null;
    }

    private void ExecuteCopyImage()
    {
        _clipboardService.CopyImage(RenderModel!, SelectedScale.Value);
        StatusMessage = "Bitmap copied to the clipboard.";
    }

    private void ExecuteCopyVector()
    {
        _clipboardService.CopyVector(RenderModel!, SelectedScale.Value);
        StatusMessage = "Bitmap and SVG payload copied to the clipboard.";
    }

    private void ExecuteExport(string format)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            AddExtension = true,
            FileName = $"gdt-{SelectedCharacteristic.Label.ToLowerInvariant().Replace(' ', '-')}",
            DefaultExt = format,
            Filter = format switch
            {
                "png" => "PNG image (*.png)|*.png",
                "svg" => "SVG vector (*.svg)|*.svg",
                "emf" => "Enhanced Metafile (*.emf)|*.emf",
                _ => "All files (*.*)|*.*"
            }
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        switch (format)
        {
            case "png":
                _exportService.ExportPng(RenderModel!, dialog.FileName, SelectedScale.Value);
                break;
            case "svg":
                _exportService.ExportSvg(RenderModel!, dialog.FileName, SelectedScale.Value);
                break;
            case "emf":
                _exportService.ExportEmf(RenderModel!, dialog.FileName, SelectedScale.Value);
                break;
        }

        StatusMessage = $"Exported {format.ToUpperInvariant()} to {dialog.FileName}.";
    }

    private void NotifyCommands()
    {
        ((RelayCommand)CopyImageCommand).NotifyCanExecuteChanged();
        ((RelayCommand)CopyVectorCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ExportPngCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ExportSvgCommand).NotifyCanExecuteChanged();
        ((RelayCommand)ExportEmfCommand).NotifyCanExecuteChanged();
    }
}

