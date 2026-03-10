using GdtCreator.Core.Models;

namespace GdtCreator.Wpf.Services;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
