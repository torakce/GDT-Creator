# GDT Creator

Desktop tool for generating ISO GPS tolerance frames with an Avalonia UI, a reusable .NET 8 core, and PNG/SVG/EMF export.

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

## Run the app

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet run --project GdtCreator.Avalonia\GdtCreator.Avalonia.csproj --configfile NuGet.Config
```

## Publish portable build

```powershell
$env:DOTNET_CLI_HOME = "$PWD\.dotnet-home"
dotnet publish GdtCreator.Avalonia\GdtCreator.Avalonia.csproj --configfile NuGet.Config
```
