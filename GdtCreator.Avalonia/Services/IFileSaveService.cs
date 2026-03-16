namespace GdtCreator.Avalonia.Services;

public interface IFileSaveService
{
    Task<string?> SaveAsync(string format, string defaultFileName, Func<Stream, Task> writer);
}
