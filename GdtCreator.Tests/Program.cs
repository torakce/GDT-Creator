using System.Text.Json;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Models;
using GdtCreator.Core.Rendering;
using GdtCreator.Core.Validation;

namespace GdtCreator.Tests;

internal static class Program
{
    private static int Main()
    {
        var failures = new List<string>();

        Run("Validation rejects datums for flatness", TestValidationRejectsFlatnessDatums, failures);
        Run("Validation requires datum for position", TestValidationRequiresDatumForPosition, failures);
        Run("Render model builds expected cells", TestRenderModelCellCount, failures);
        Run("Render model normalizes tolerance text", TestRenderModelNormalizesValue, failures);
        Run("Render model includes top and bottom text", TestRenderModelIncludesContextText, failures);
        Run("Render model carries content color", TestRenderModelCarriesContentColor, failures);
        Run("Settings serialize cleanly", TestSettingsSerialization, failures);

        if (failures.Count == 0)
        {
            Console.WriteLine("All tests passed.");
            return 0;
        }

        Console.Error.WriteLine("Failures:");
        foreach (var failure in failures)
        {
            Console.Error.WriteLine($" - {failure}");
        }

        return 1;
    }

    private static void TestValidationRejectsFlatnessDatums()
    {
        var service = new ValidationService();
        var result = service.Validate(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Flatness,
            ToleranceValue = "0.2",
            ZoneModifier = ToleranceZoneModifier.None,
            DatumReferences = [new DatumReference { Label = "A" }]
        });

        AssertFalse(result.IsValid, "Flatness with datums should be invalid.");
    }

    private static void TestValidationRequiresDatumForPosition()
    {
        var service = new ValidationService();
        var result = service.Validate(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Position,
            ToleranceValue = "0.1",
            DatumReferences = []
        });

        AssertFalse(result.IsValid, "Position without datums should be invalid.");
    }

    private static void TestRenderModelCellCount()
    {
        var renderer = new ToleranceRenderService();
        var model = renderer.Render(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Position,
            ToleranceValue = "0.1",
            DatumReferences =
            [
                new DatumReference { Label = "A" },
                new DatumReference { Label = "B" }
            ]
        });

        AssertEqual(4, model.Cells.Count, "Expected characteristic, value, and two datum cells.");
        AssertTrue(model.Width > 0, "Render width should be positive.");
    }

    private static void TestRenderModelNormalizesValue()
    {
        var renderer = new ToleranceRenderService();
        var model = renderer.Render(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Perpendicularity,
            ToleranceValue = "0,250",
            DatumReferences = [new DatumReference { Label = "A" }]
        });

        var toleranceCellText = string.Concat(model.Cells[1].Tokens.Where(token => !token.IsSymbol).Select(token => token.Text));
        AssertEqual("0.25", toleranceCellText, "Tolerance text should normalize to invariant format.");
    }

    private static void TestRenderModelIncludesContextText()
    {
        var renderer = new ToleranceRenderService();
        var model = renderer.Render(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Position,
            ToleranceValue = "0.1",
            TopText = "TOP NOTE",
            BottomText = "BOTTOM NOTE",
            DatumReferences = [new DatumReference { Label = "A" }]
        });

        AssertEqual("TOP NOTE", model.TopText!, "Top text should be carried into the render model.");
        AssertEqual("BOTTOM NOTE", model.BottomText!, "Bottom text should be carried into the render model.");
        AssertTrue(model.Height > model.FrameHeight, "Render height should expand when context text is present.");
    }

    private static void TestRenderModelCarriesContentColor()
    {
        var renderer = new ToleranceRenderService();
        var model = renderer.Render(new GeometricToleranceSpec
        {
            Characteristic = GeometricCharacteristic.Position,
            ToleranceValue = "0.1",
            ContentColorHex = "#b42318",
            DatumReferences = [new DatumReference { Label = "A" }]
        });

        AssertEqual("#B42318", model.ContentColorHex, "Content color should normalize to uppercase hex.");
    }

    private static void TestSettingsSerialization()
    {
        var settings = new AppSettings
        {
            ExportScale = 4d,
            LastSpec = new GeometricToleranceSpec
            {
                Characteristic = GeometricCharacteristic.Perpendicularity,
                ToleranceValue = "0.05",
                ContentColorHex = "#0F766E"
            }
        };

        var json = JsonSerializer.Serialize(settings);
        var roundTrip = JsonSerializer.Deserialize<AppSettings>(json);

        AssertTrue(roundTrip is not null, "Settings deserialization returned null.");
        AssertEqual(4d, roundTrip!.ExportScale, "Export scale should round-trip.");
        AssertEqual(GeometricCharacteristic.Perpendicularity, roundTrip.LastSpec.Characteristic, "Characteristic should round-trip.");
        AssertEqual("#0F766E", roundTrip.LastSpec.ContentColorHex, "Content color should round-trip.");
    }

    private static void Run(string name, Action test, List<string> failures)
    {
        try
        {
            test();
            Console.WriteLine($"PASS: {name}");
        }
        catch (Exception ex)
        {
            failures.Add($"{name}: {ex.Message}");
            Console.WriteLine($"FAIL: {name}");
        }
    }

    private static void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool condition, string message)
    {
        AssertTrue(!condition, message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
        where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            throw new InvalidOperationException($"{message} Expected {expected}, got {actual}.");
        }
    }
}
