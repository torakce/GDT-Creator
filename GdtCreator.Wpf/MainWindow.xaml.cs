using System.Windows;
using System.Windows.Input;
using GdtCreator.Core.Rendering;
using GdtCreator.Core.Validation;
using GdtCreator.Wpf.Services;
using GdtCreator.Wpf.ViewModels;

namespace GdtCreator.Wpf;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(
            new ValidationService(),
            new ToleranceRenderService(),
            new ExportService(),
            new ClipboardService(),
            new JsonSettingsService());

        DataContext = _viewModel;
        Closing += OnClosing;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.SaveSettings();
    }

    private void OnWindowPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        EditorScrollViewer.ScrollToVerticalOffset(EditorScrollViewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }
}
