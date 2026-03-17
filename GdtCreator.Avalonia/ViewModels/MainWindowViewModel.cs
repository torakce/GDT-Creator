using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Input;
using GdtCreator.Avalonia.Models;
using GdtCreator.Avalonia.Services;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;
using GdtCreator.Core.Rendering;
using GdtCreator.Core.Validation;

namespace GdtCreator.Avalonia.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private static readonly Regex InlineTokenPattern = new(@"\b(?:CZ|UZ)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private readonly IValidationService _validationService;
    private readonly IRenderService _renderService;
    private readonly IExportService _exportService;
    private readonly IClipboardService _clipboardService;
    private readonly IFileSaveService _fileSaveService;
    private readonly ISettingsService _settingsService;

    private OptionItem<GeometricCharacteristic> _selectedCharacteristic = null!;
    private OptionItem<ToleranceZoneModifier> _selectedZoneModifier = null!;
    private OptionItem<ToleranceMaterialCondition> _selectedToleranceMaterialCondition = null!;
    private const double FixedExportScale = 4d;
    private OptionItem<string>? _selectedContentColor;
    private string _toleranceValue = "0.10";
    private bool _projectedToleranceZone;
    private bool _freeState;
    private bool _topTextEnabled;
    private bool _bottomTextEnabled;
    private string _topText = string.Empty;
    private string _bottomText = string.Empty;
    private string _contentColorHex = "#000000";
    private bool _areDatumInputsEnabled;
    private string _datumSectionHint = "Enter up to three datum references.";
    private ToleranceRenderModel? _renderModel;
    private string _statusMessage = "Ready.";
    private bool _hasValidationErrors;
    private bool _isRefreshing;

    public MainWindowViewModel(
        IValidationService validationService,
        IRenderService renderService,
        IExportService exportService,
        IClipboardService clipboardService,
        IFileSaveService fileSaveService,
        ISettingsService settingsService)
    {
        _validationService = validationService;
        _renderService = renderService;
        _exportService = exportService;
        _clipboardService = clipboardService;
        _fileSaveService = fileSaveService;
        _settingsService = settingsService;

        Characteristics = new ObservableCollection<OptionItem<GeometricCharacteristic>>
        {
            new() { Label = "Straightness", Value = GeometricCharacteristic.Straightness, Symbol = RenderSymbol.Straightness, Category = "Form" },
            new() { Label = "Flatness", Value = GeometricCharacteristic.Flatness, Symbol = RenderSymbol.Flatness, Category = "Form" },
            new() { Label = "Circularity", Value = GeometricCharacteristic.Circularity, Symbol = RenderSymbol.Circularity, Category = "Form" },
            new() { Label = "Cylindricity", Value = GeometricCharacteristic.Cylindricity, Symbol = RenderSymbol.Cylindricity, Category = "Form" },
            new() { Label = "Profile of a line", Value = GeometricCharacteristic.ProfileOfALine, Symbol = RenderSymbol.ProfileOfALine, ShortLabel = "Line profile", Category = "Profile" },
            new() { Label = "Profile of a surface", Value = GeometricCharacteristic.ProfileOfASurface, Symbol = RenderSymbol.ProfileOfASurface, ShortLabel = "Surface profile", Category = "Profile" },
            new() { Label = "Parallelism", Value = GeometricCharacteristic.Parallelism, Symbol = RenderSymbol.Parallelism, Category = "Orientation" },
            new() { Label = "Perpendicularity", Value = GeometricCharacteristic.Perpendicularity, Symbol = RenderSymbol.Perpendicularity, ShortLabel = "Perpendicular", Category = "Orientation" },
            new() { Label = "Angularity", Value = GeometricCharacteristic.Angularity, Symbol = RenderSymbol.Angularity, Category = "Orientation" },
            new() { Label = "Position", Value = GeometricCharacteristic.Position, Symbol = RenderSymbol.Position, Category = "Location" },
            new() { Label = "Concentricity", Value = GeometricCharacteristic.Concentricity, Symbol = RenderSymbol.Concentricity, ShortLabel = "Concentric", Category = "Location" },
            new() { Label = "Symmetry", Value = GeometricCharacteristic.Symmetry, Symbol = RenderSymbol.Symmetry, Category = "Location" },
            new() { Label = "Circular runout", Value = GeometricCharacteristic.CircularRunout, Symbol = RenderSymbol.CircularRunout, ShortLabel = "Runout", Category = "Runout" },
            new() { Label = "Total runout", Value = GeometricCharacteristic.TotalRunout, Symbol = RenderSymbol.TotalRunout, ShortLabel = "Total runout", Category = "Runout" }
        };

        ZoneModifiers = new ObservableCollection<OptionItem<ToleranceZoneModifier>>
        {
            new() { Label = "No modifier", ShortLabel = "None", Value = ToleranceZoneModifier.None },
            new() { Label = "Diameter", ShortLabel = "\u00D8", Value = ToleranceZoneModifier.Diameter, Symbol = RenderSymbol.Diameter },
            new() { Label = "Spherical diameter", ShortLabel = "S\u00D8", Value = ToleranceZoneModifier.SphericalDiameter, Symbol = RenderSymbol.SphericalDiameter },
            new() { Label = "Spherical radius", ShortLabel = "SR", Value = ToleranceZoneModifier.SphericalRadius }
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

        QuickColorOptions = new ObservableCollection<OptionItem<string>>
        {
            new() { Label = "Black", Value = "#000000", SwatchHex = "#000000" },
            new() { Label = "Red", Value = "#FF0000", SwatchHex = "#FF0000" },
            new() { Label = "Blue", Value = "#0000FF", SwatchHex = "#0000FF" },
            new() { Label = "Green", Value = "#00FF00", SwatchHex = "#00FF00" }
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

        CopyImageCommand = new AsyncRelayCommand(_ => ExecuteCopyImageAsync(), _ => CanExportOrCopy());
        CopyVectorCommand = new AsyncRelayCommand(_ => ExecuteCopyVectorAsync(), _ => CanExportOrCopy());
        ExportPngCommand = new AsyncRelayCommand(_ => ExecuteExportAsync("png"), _ => CanExportOrCopy());
        ExportSvgCommand = new AsyncRelayCommand(_ => ExecuteExportAsync("svg"), _ => CanExportOrCopy());
        ExportEmfCommand = new AsyncRelayCommand(_ => ExecuteExportAsync("emf"), _ => CanExportOrCopy());

        LoadSettings();
        RefreshState();
    }

    public ObservableCollection<OptionItem<GeometricCharacteristic>> Characteristics { get; }

    public ObservableCollection<OptionItem<ToleranceZoneModifier>> ZoneModifiers { get; }

    public ObservableCollection<OptionItem<ToleranceMaterialCondition>> ToleranceMaterialConditions { get; }

    public ObservableCollection<OptionItem<DatumMaterialCondition>> DatumConditionOptions { get; }

    public ObservableCollection<OptionItem<string>> QuickColorOptions { get; }

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

    public OptionItem<string>? SelectedContentColor
    {
        get => _selectedContentColor;
        set
        {
            if (SetProperty(ref _selectedContentColor, value) && !_isRefreshing && value is not null)
            {
                var normalizedColor = NormalizeColorHex(value.Value);
                if (!string.Equals(_contentColorHex, normalizedColor, StringComparison.Ordinal))
                {
                    _contentColorHex = normalizedColor;
                    RaisePropertyChanged(nameof(ContentColorHex));
                }

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

    public bool TopTextEnabled
    {
        get => _topTextEnabled;
        set
        {
            if (SetProperty(ref _topTextEnabled, value) && !_isRefreshing)
            {
                if (!value && !string.IsNullOrWhiteSpace(_topText))
                {
                    _topText = string.Empty;
                    RaisePropertyChanged(nameof(TopText));
                }

                RefreshState();
            }
        }
    }

    public bool BottomTextEnabled
    {
        get => _bottomTextEnabled;
        set
        {
            if (SetProperty(ref _bottomTextEnabled, value) && !_isRefreshing)
            {
                if (!value && !string.IsNullOrWhiteSpace(_bottomText))
                {
                    _bottomText = string.Empty;
                    RaisePropertyChanged(nameof(BottomText));
                }

                RefreshState();
            }
        }
    }

    public string TopText
    {
        get => _topText;
        set
        {
            if (SetProperty(ref _topText, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public string BottomText
    {
        get => _bottomText;
        set
        {
            if (SetProperty(ref _bottomText, value) && !_isRefreshing)
            {
                RefreshState();
            }
        }
    }

    public string ContentColorHex
    {
        get => _contentColorHex;
        set
        {
            var normalizedColor = NormalizeColorHex(value);
            if (SetProperty(ref _contentColorHex, normalizedColor) && !_isRefreshing)
            {
                SyncSelectedContentColor();
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

    public bool HasValidationErrors
    {
        get => _hasValidationErrors;
        private set => SetProperty(ref _hasValidationErrors, value);
    }

    public bool IsSpecValid => !HasValidationErrors;

    public void SaveSettings()
    {
        _settingsService.Save(new AppSettings
        {
            LastSpec = BuildSpec(),
            ExportScale = FixedExportScale
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
        ToleranceValue = ComposeToleranceInput(spec);
        ProjectedToleranceZone = spec.ProjectedToleranceZone;
        FreeState = spec.FreeState;
        TopText = spec.TopText ?? string.Empty;
        BottomText = spec.BottomText ?? string.Empty;
        TopTextEnabled = !string.IsNullOrWhiteSpace(TopText);
        BottomTextEnabled = !string.IsNullOrWhiteSpace(BottomText);
        ContentColorHex = NormalizeColorHex(spec.ContentColorHex);
        SyncSelectedContentColor();

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
            ? "Enter up to three datum references."
            : "This characteristic does not use datum references.";

        var spec = BuildSpec();
        RenderModel = _renderService.Render(spec);

        var validation = _validationService.Validate(spec);
        ValidationErrors.Clear();
        foreach (var error in validation.Errors)
        {
            ValidationErrors.Add(error);
        }

        HasValidationErrors = ValidationErrors.Count > 0;
        RaisePropertyChanged(nameof(IsSpecValid));
        StatusMessage = validation.IsValid
            ? "Ready to copy or export."
            : "Fix the validation errors before copying or exporting.";

        RaisePropertyChanged(nameof(ValidationErrors));
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
            ZoneModifier = SelectedZoneModifier?.Value ?? ToleranceZoneModifier.Diameter,
            ToleranceMaterialCondition = SelectedToleranceMaterialCondition?.Value ?? ToleranceMaterialCondition.None,
            ProjectedToleranceZone = ProjectedToleranceZone,
            FreeState = FreeState,
            CombinedZone = false,
            UnequallyDisposedValue = null,
            TopText = TopTextEnabled && !string.IsNullOrWhiteSpace(TopText) ? TopText : null,
            BottomText = BottomTextEnabled && !string.IsNullOrWhiteSpace(BottomText) ? BottomText : null,
            ContentColorHex = ContentColorHex,
            DatumReferences = datumReferences
        };
    }

    private static string ComposeToleranceInput(GeometricToleranceSpec spec)
    {
        var parts = new List<string>();
        var baseValue = spec.ToleranceValue?.Trim() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(baseValue))
        {
            parts.Add(baseValue);
        }

        if (spec.CombinedZone && !ContainsInlineToken(baseValue, "CZ"))
        {
            parts.Add("CZ");
        }

        if (!string.IsNullOrWhiteSpace(spec.UnequallyDisposedValue) && !ContainsInlineToken(baseValue, "UZ"))
        {
            parts.Add("UZ");
            parts.Add(spec.UnequallyDisposedValue.Trim());
        }

        return string.Join(' ', parts.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
    }

    private static bool ContainsInlineToken(string value, string token)
    {
        return InlineTokenPattern.Matches(value).Any(match => string.Equals(match.Value, token, StringComparison.OrdinalIgnoreCase));
    }

    private void SyncSelectedContentColor()
    {
        var matchingOption = QuickColorOptions.FirstOrDefault(option => string.Equals(option.Value, _contentColorHex, StringComparison.OrdinalIgnoreCase));
        SetProperty(ref _selectedContentColor, matchingOption, nameof(SelectedContentColor));
    }

    private static string NormalizeColorHex(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "#000000";
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
            : "#000000";
    }

    private static bool IsHexDigit(char character)
    {
        return character is >= '0' and <= '9'
            or >= 'A' and <= 'F'
            or >= 'a' and <= 'f';
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
        return RenderModel is not null && !HasValidationErrors;
    }

    private async Task ExecuteCopyImageAsync()
    {
        try
        {
            await _clipboardService.CopyImageAsync(RenderModel!, FixedExportScale);
            StatusMessage = "Bitmap copied to the clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private async Task ExecuteCopyVectorAsync()
    {
        try
        {
            await _clipboardService.CopyVectorAsync(RenderModel!, FixedExportScale);
            StatusMessage = "Bitmap and SVG payload copied to the clipboard.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Copy failed: {ex.Message}";
        }
    }

    private async Task ExecuteExportAsync(string format)
    {
        try
        {
            var fileName = $"gdt-{SelectedCharacteristic.Label.ToLowerInvariant().Replace(' ', '-')}";
            var path = await _fileSaveService.SaveAsync(format, fileName, stream => format switch
            {
                "png" => _exportService.ExportPngAsync(RenderModel!, stream, FixedExportScale),
                "svg" => _exportService.ExportSvgAsync(RenderModel!, stream, FixedExportScale),
                "emf" => _exportService.ExportEmfAsync(RenderModel!, stream, FixedExportScale),
                _ => Task.CompletedTask
            });

            if (path is not null)
            {
                StatusMessage = $"Exported {format.ToUpperInvariant()} to {path}.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Export failed: {ex.Message}";
        }
    }

    private void NotifyCommands()
    {
        NotifyCommand(CopyImageCommand);
        NotifyCommand(CopyVectorCommand);
        NotifyCommand(ExportPngCommand);
        NotifyCommand(ExportSvgCommand);
        NotifyCommand(ExportEmfCommand);
    }

    private static void NotifyCommand(ICommand command)
    {
        switch (command)
        {
            case RelayCommand relayCommand:
                relayCommand.NotifyCanExecuteChanged();
                break;
            case AsyncRelayCommand asyncRelayCommand:
                asyncRelayCommand.NotifyCanExecuteChanged();
                break;
        }
    }
}
