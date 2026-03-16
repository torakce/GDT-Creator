namespace GdtCreator.Core.Models;

public sealed class AppSettings
{
    public GeometricToleranceSpec LastSpec { get; set; } = GeometricToleranceSpec.CreateDefault();

    public double ExportScale { get; set; } = 4.0d;
}

