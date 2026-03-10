# GDT Creator

Standalone WPF tool for generating ISO GPS tolerance frames and exporting them as PNG, SVG, or EMF.

## Build

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet restore --configfile NuGet.Config
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet build GdtCreator.sln --configfile NuGet.Config
```

## Run tests

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet run --project GdtCreator.Tests\GdtCreator.Tests.csproj --configfile NuGet.Config
```

## Publish portable build

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet publish GdtCreator.Wpf\GdtCreator.Wpf.csproj /p:PublishProfile=PortableFolder --configfile NuGet.Config
```
