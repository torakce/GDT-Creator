using GdtCreator.Core.Models;

namespace GdtCreator.Avalonia.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
