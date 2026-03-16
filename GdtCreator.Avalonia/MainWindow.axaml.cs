using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using GdtCreator.Avalonia.Services;
using GdtCreator.Avalonia.ViewModels;
using GdtCreator.Core.Rendering;
using GdtCreator.Core.Validation;

namespace GdtCreator.Avalonia;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainWindowViewModel(
            new ValidationService(),
            new ToleranceRenderService(),
            new ExportService(),
            new ClipboardService(this),
            new FileSaveService(this),
            new JsonSettingsService());

        DataContext = _viewModel;
        Closing += OnClosing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        _viewModel.SaveSettings();
    }
}
