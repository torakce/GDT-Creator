using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace GdtCreator.Avalonia.Services;

public sealed class FileSaveService : IFileSaveService
{
    private readonly Window _window;

    public FileSaveService(Window window)
    {
        _window = window;
    }

    public async Task<string?> SaveAsync(string format, string defaultFileName, Func<Stream, Task> writer)
    {
        var file = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = $"Export {format.ToUpperInvariant()}",
            SuggestedFileName = $"{defaultFileName}.{format}",
            DefaultExtension = format,
            ShowOverwritePrompt = true,
            FileTypeChoices =
            [
                new FilePickerFileType($"{format.ToUpperInvariant()} file")
                {
                    Patterns = [$"*.{format}"]
                }
            ]
        });

        if (file is null)
        {
            return null;
        }

        await using var stream = await file.OpenWriteAsync();
        await writer(stream);
        await stream.FlushAsync();
        return file.TryGetLocalPath() ?? file.Name;
    }
}
