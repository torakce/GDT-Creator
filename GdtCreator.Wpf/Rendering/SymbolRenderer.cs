using System.Globalization;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DrawingBrush = System.Drawing.Brush;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPen = System.Drawing.Pen;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingStringFormat = System.Drawing.StringFormat;
using WpfBrushes = System.Windows.Media.Brushes;
using WpfColor = System.Windows.Media.Color;
using WpfFontFamily = System.Windows.Media.FontFamily;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPen = System.Windows.Media.Pen;
using WpfPoint = System.Windows.Point;
using GdtCreator.Core.Enums;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Rendering;

public static class SymbolRenderer
{
    private static readonly Typeface TextTypeface = new(new WpfFontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.SemiCondensed);
    private static readonly Typeface SymbolTypeface = new(new WpfFontFamily("Segoe UI Symbol"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    public static void DrawSymbolIcon(DrawingContext context, RenderSymbol symbol, Rect bounds, double strokeThickness, double pixelsPerDip)
    {
        var brush = new SolidColorBrush(WpfColor.FromRgb(16, 42, 67));
        brush.Freeze();
        var pen = CreateWpfPen(strokeThickness);
        DrawWpfSymbol(context, symbol, bounds, pen, brush, pixelsPerDip);
    }

    public static void DrawModel(DrawingContext context, ToleranceRenderModel model, Rect bounds, double pixelsPerDip)
    {
        var scale = Math.Min(bounds.Width / model.Width, bounds.Height / model.Height);
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1d;

        var originX = bounds.Left + ((bounds.Width - (model.Width * scale)) / 2d);
        var originY = bounds.Top + ((bounds.Height - (model.Height * scale)) / 2d);

        context.PushTransform(new TranslateTransform(originX, originY));
        context.PushTransform(new ScaleTransform(scale, scale));

        var outlinePen = CreateWpfPen(model.StrokeThickness);
        context.DrawRectangle(WpfBrushes.White, outlinePen, new Rect(0, 0, model.Width, model.Height));

        double cursor = 0d;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            if (index > 0)
            {
                context.DrawLine(outlinePen, new WpfPoint(cursor, 0), new WpfPoint(cursor, model.Height));
            }

            DrawCell(context, cell, cursor, model.Height, pixelsPerDip, model.StrokeThickness);
            cursor += cell.Width;
        }

        context.Pop();
        context.Pop();
    }

    public static RenderTargetBitmap CreateBitmap(ToleranceRenderModel model, double scale)
    {
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(model.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(model.Height * scale));
        var visual = new DrawingVisual();

        using (var drawingContext = visual.RenderOpen())
        {
            drawingContext.DrawRectangle(WpfBrushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
            DrawModel(drawingContext, model, new Rect(0, 0, pixelWidth, pixelHeight), 1d);
        }

        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, 96d, 96d, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public static string BuildSvg(ToleranceRenderModel model, double scale)
    {
        var width = model.Width * scale;
        var height = model.Height * scale;
        var strokeWidth = model.StrokeThickness * scale;
        const string color = "#102A43";
        var builder = new System.Text.StringBuilder();

        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Format(width)}\" height=\"{Format(height)}\" viewBox=\"0 0 {Format(width)} {Format(height)}\">");
        builder.AppendLine($"  <rect width=\"{Format(width)}\" height=\"{Format(height)}\" fill=\"#FFFFFF\" />");
        builder.AppendLine($"  <rect x=\"0\" y=\"0\" width=\"{Format(width)}\" height=\"{Format(height)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");

        double cursor = 0d;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            var scaledCellWidth = cell.Width * scale;
            if (index > 0)
            {
                builder.AppendLine($"  <line x1=\"{Format(cursor)}\" y1=\"0\" x2=\"{Format(cursor)}\" y2=\"{Format(height)}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
            }

            var tokenWidth = RenderLayout.GetTokenSequenceWidth(cell.Tokens) * scale;
            var tokenCursor = cursor + ((scaledCellWidth - tokenWidth) / 2d);

            foreach (var token in cell.Tokens)
            {
                var advance = RenderLayout.GetTokenAdvance(token) * scale;
                var tokenRect = CreateSymbolBounds(tokenCursor, advance, height);
                if (token.IsSymbol)
                {
                    AppendSvgSymbol(builder, token.Symbol!.Value, tokenRect, color, strokeWidth);
                }
                else
                {
                    var text = SecurityElement.Escape(token.Text) ?? string.Empty;
                    var fontSize = RenderLayout.TextFontSize * scale;
                    builder.AppendLine($"  <text x=\"{Format(tokenCursor + (advance / 2d))}\" y=\"{Format((height / 2d) + (fontSize / 3.2d))}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
                }

                tokenCursor += advance;
            }

            cursor += scaledCellWidth;
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    public static byte[] BuildEmf(ToleranceRenderModel model, double scale)
    {
        var width = (float)(model.Width * scale);
        var height = (float)(model.Height * scale);
        using var stream = new MemoryStream();
        using var referenceGraphics = DrawingGraphics.FromHwnd(IntPtr.Zero);
        var hdc = referenceGraphics.GetHdc();

        try
        {
            using var metafile = new System.Drawing.Imaging.Metafile(
                stream,
                hdc,
                new DrawingRectangleF(0, 0, width, height),
                System.Drawing.Imaging.MetafileFrameUnit.Pixel,
                System.Drawing.Imaging.EmfType.EmfPlusDual);
            using var graphics = DrawingGraphics.FromImage(metafile);
            using var pen = new DrawingPen(DrawingColor.FromArgb(16, 42, 67), (float)(model.StrokeThickness * scale));
            using var textBrush = new System.Drawing.SolidBrush(DrawingColor.FromArgb(16, 42, 67));
            using var textFont = new DrawingFont("Bahnschrift", (float)(RenderLayout.TextFontSize * scale), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);

            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(DrawingColor.White);
            graphics.DrawRectangle(pen, 0f, 0f, width - pen.Width, height - pen.Width);

            float cursor = 0f;
            foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
            {
                var scaledCellWidth = (float)(cell.Width * scale);
                if (index > 0)
                {
                    graphics.DrawLine(pen, cursor, 0f, cursor, height);
                }

                var tokenWidth = (float)(cell.Tokens.Sum(RenderLayout.GetTokenAdvance) * scale);
                var tokenCursor = cursor + ((scaledCellWidth - tokenWidth) / 2f);

                foreach (var token in cell.Tokens)
                {
                    var advance = (float)(RenderLayout.GetTokenAdvance(token) * scale);
                    var tokenRect = CreateSymbolBounds(tokenCursor, advance, height);
                    if (token.IsSymbol)
                    {
                        DrawGdiSymbol(graphics, token.Symbol!.Value, tokenRect, pen, textBrush, textFont);
                    }
                    else
                    {
                        using var format = new DrawingStringFormat
                        {
                            Alignment = System.Drawing.StringAlignment.Center,
                            LineAlignment = System.Drawing.StringAlignment.Center
                        };
                        graphics.DrawString(token.Text, textFont, textBrush, new DrawingRectangleF(tokenCursor, 0f, advance, height), format);
                    }

                    tokenCursor += advance;
                }

                cursor += scaledCellWidth;
            }
        }
        finally
        {
            referenceGraphics.ReleaseHdc(hdc);
        }

        return stream.ToArray();
    }

    private static Rect CreateSymbolBounds(double startX, double advance, double cellHeight)
    {
        var size = Math.Max(14d, Math.Min(cellHeight * 0.78d, advance * 0.88d));
        return new Rect(startX + ((advance - size) / 2d), (cellHeight - size) / 2d, size, size);
    }

    private static DrawingRectangleF CreateSymbolBounds(float startX, float advance, float cellHeight)
    {
        var size = MathF.Max(14f, MathF.Min(cellHeight * 0.78f, advance * 0.88f));
        return new DrawingRectangleF(startX + ((advance - size) / 2f), (cellHeight - size) / 2f, size, size);
    }
    private static void DrawCell(DrawingContext context, ToleranceCell cell, double originX, double height, double pixelsPerDip, double strokeThickness)
    {
        var tokenWidth = RenderLayout.GetTokenSequenceWidth(cell.Tokens);
        var cursor = originX + ((cell.Width - tokenWidth) / 2d);
        var brush = new SolidColorBrush(WpfColor.FromRgb(16, 42, 67));
        brush.Freeze();
        var pen = CreateWpfPen(strokeThickness);

        foreach (var token in cell.Tokens)
        {
            var advance = RenderLayout.GetTokenAdvance(token);
            var tokenRect = CreateSymbolBounds(cursor, advance, height);
            if (token.IsSymbol)
            {
                DrawWpfSymbol(context, token.Symbol!.Value, tokenRect, pen, brush, pixelsPerDip);
            }
            else
            {
                var formatted = new FormattedText(
                    token.Text ?? string.Empty,
                    CultureInfo.InvariantCulture,
                    WpfFlowDirection.LeftToRight,
                    TextTypeface,
                    RenderLayout.TextFontSize,
                    brush,
                    pixelsPerDip);
                context.DrawText(formatted, new WpfPoint(cursor + ((advance - formatted.Width) / 2d), (height - formatted.Height) / 2d));
            }

            cursor += advance;
        }
    }

    private static WpfPen CreateWpfPen(double strokeThickness)
    {
        var pen = new WpfPen(new SolidColorBrush(WpfColor.FromRgb(16, 42, 67)), strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static void DrawWpfSymbol(DrawingContext context, RenderSymbol symbol, Rect bounds, WpfPen pen, System.Windows.Media.Brush brush, double pixelsPerDip)
    {
        switch (symbol)
        {
            case RenderSymbol.Straightness:
                context.DrawLine(pen, PointAt(bounds, 0.15, 0.5), PointAt(bounds, 0.85, 0.5));
                break;
            case RenderSymbol.Flatness:
                context.DrawGeometry(null, pen, CreatePolygonGeometry(bounds, 0.18, 0.32, 0.78, 0.32, 0.62, 0.72, 0.02, 0.72));
                break;
            case RenderSymbol.Circularity:
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.28, bounds.Height * 0.28);
                break;
            case RenderSymbol.Cylindricity:
                DrawWpfUnicodeSymbol(context, bounds, brush, "\u232D", pixelsPerDip, 0.92, -0.01);
                break;
            case RenderSymbol.ProfileOfALine:
                context.DrawGeometry(null, pen, CreateBezierGeometry(bounds, 0.18, 0.65, 0.32, 0.2, 0.68, 0.2, 0.82, 0.65));
                break;
            case RenderSymbol.ProfileOfASurface:
                context.DrawGeometry(null, pen, CreateSurfaceProfileGeometry(bounds));
                break;
            case RenderSymbol.Parallelism:
                context.DrawLine(pen, PointAt(bounds, 0.32, 0.82), PointAt(bounds, 0.50, 0.18));
                context.DrawLine(pen, PointAt(bounds, 0.50, 0.82), PointAt(bounds, 0.68, 0.18));
                break;
            case RenderSymbol.Perpendicularity:
                context.DrawLine(pen, PointAt(bounds, 0.5, 0.18), PointAt(bounds, 0.5, 0.82));
                context.DrawLine(pen, PointAt(bounds, 0.22, 0.82), PointAt(bounds, 0.78, 0.82));
                break;
            case RenderSymbol.Angularity:
                context.DrawLine(pen, PointAt(bounds, 0.18, 0.82), PointAt(bounds, 0.82, 0.82));
                context.DrawLine(pen, PointAt(bounds, 0.18, 0.82), PointAt(bounds, 0.62, 0.22));
                break;
            case RenderSymbol.Position:
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.22, bounds.Height * 0.22);
                context.DrawLine(pen, PointAt(bounds, 0.5, 0.12), PointAt(bounds, 0.5, 0.88));
                context.DrawLine(pen, PointAt(bounds, 0.12, 0.5), PointAt(bounds, 0.88, 0.5));
                break;
            case RenderSymbol.Concentricity:
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.3, bounds.Height * 0.3);
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.13, bounds.Height * 0.13);
                break;
            case RenderSymbol.Symmetry:
                DrawWpfUnicodeSymbol(context, bounds, brush, "\u232F", pixelsPerDip, 0.92, -0.01);
                break;
            case RenderSymbol.CircularRunout:
                DrawWpfUnicodeSymbol(context, bounds, brush, "\u2197", pixelsPerDip, 0.78, -0.02);
                break;
            case RenderSymbol.TotalRunout:
                DrawWpfUnicodeSymbol(context, bounds, brush, "\u2330", pixelsPerDip, 0.92, -0.01);
                break;
            case RenderSymbol.Diameter:
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.28, bounds.Height * 0.28);
                context.DrawLine(pen, PointAt(bounds, 0.24, 0.76), PointAt(bounds, 0.76, 0.24));
                break;
            case RenderSymbol.MaximumMaterialCondition:
                DrawCircledLetter(context, bounds, pen, brush, "M", pixelsPerDip);
                break;
            case RenderSymbol.LeastMaterialCondition:
                DrawCircledLetter(context, bounds, pen, brush, "L", pixelsPerDip);
                break;
            case RenderSymbol.ProjectedToleranceZone:
                DrawCircledLetter(context, bounds, pen, brush, "P", pixelsPerDip);
                break;
            case RenderSymbol.FreeState:
                DrawCircledLetter(context, bounds, pen, brush, "F", pixelsPerDip);
                break;
            case RenderSymbol.SphericalDiameter:
                DrawWpfPrefixedSymbol(context, bounds, pen, brush, "S", RenderSymbol.Diameter, pixelsPerDip);
                break;
            case RenderSymbol.SphericalRadius:
                DrawWpfPrefixedSymbol(context, bounds, pen, brush, "SR", RenderSymbol.Diameter, pixelsPerDip);
                break;
            case RenderSymbol.DatumFeatureDirect:
                DrawWpfDatumFeatureSymbol(context, bounds, pen, brush, DatumFeatureSymbolStyle.Direct);
                break;
            case RenderSymbol.DatumFeatureLeaderLeft:
                DrawWpfDatumFeatureSymbol(context, bounds, pen, brush, DatumFeatureSymbolStyle.LeaderLeft);
                break;
            case RenderSymbol.DatumFeatureLeaderRight:
                DrawWpfDatumFeatureSymbol(context, bounds, pen, brush, DatumFeatureSymbolStyle.LeaderRight);
                break;
            case RenderSymbol.DatumFeatureLeaderDown:
                DrawWpfDatumFeatureSymbol(context, bounds, pen, brush, DatumFeatureSymbolStyle.LeaderDown);
                break;
        }
    }

    private static void AppendSvgSymbol(System.Text.StringBuilder builder, RenderSymbol symbol, Rect bounds, string color, double strokeWidth)
    {
        switch (symbol)
        {
            case RenderSymbol.Straightness:
                AppendSvgLine(builder, PointAt(bounds, 0.15, 0.5), PointAt(bounds, 0.85, 0.5), color, strokeWidth);
                break;
            case RenderSymbol.Flatness:
                builder.AppendLine($"  <polygon points=\"{SvgPoint(bounds, 0.18, 0.32)} {SvgPoint(bounds, 0.78, 0.32)} {SvgPoint(bounds, 0.62, 0.72)} {SvgPoint(bounds, 0.02, 0.72)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
                break;
            case RenderSymbol.Circularity:
                AppendSvgEllipse(builder, Center(bounds), bounds.Width * 0.28, bounds.Height * 0.28, color, strokeWidth);
                break;
            case RenderSymbol.Cylindricity:
                AppendSvgUnicodeSymbol(builder, bounds, color, "\u232D", 0.92, -0.01);
                break;
            case RenderSymbol.ProfileOfALine:
                builder.AppendLine($"  <path d=\"M {SvgPoint(bounds, 0.18, 0.65)} C {SvgPoint(bounds, 0.32, 0.2)} {SvgPoint(bounds, 0.68, 0.2)} {SvgPoint(bounds, 0.82, 0.65)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" />");
                break;
            case RenderSymbol.ProfileOfASurface:
                AppendSvgSurfaceProfile(builder, bounds, color, strokeWidth);
                break;
            case RenderSymbol.Parallelism:
                AppendSvgLine(builder, PointAt(bounds, 0.32, 0.82), PointAt(bounds, 0.50, 0.18), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.50, 0.82), PointAt(bounds, 0.68, 0.18), color, strokeWidth);
                break;
            case RenderSymbol.Perpendicularity:
                AppendSvgLine(builder, PointAt(bounds, 0.5, 0.18), PointAt(bounds, 0.5, 0.82), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.22, 0.82), PointAt(bounds, 0.78, 0.82), color, strokeWidth);
                break;
            case RenderSymbol.Angularity:
                AppendSvgLine(builder, PointAt(bounds, 0.18, 0.82), PointAt(bounds, 0.82, 0.82), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.18, 0.82), PointAt(bounds, 0.62, 0.22), color, strokeWidth);
                break;
            case RenderSymbol.Position:
                AppendSvgEllipse(builder, Center(bounds), bounds.Width * 0.22, bounds.Height * 0.22, color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.5, 0.12), PointAt(bounds, 0.5, 0.88), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.12, 0.5), PointAt(bounds, 0.88, 0.5), color, strokeWidth);
                break;
            case RenderSymbol.Concentricity:
                AppendSvgEllipse(builder, Center(bounds), bounds.Width * 0.3, bounds.Height * 0.3, color, strokeWidth);
                AppendSvgEllipse(builder, Center(bounds), bounds.Width * 0.13, bounds.Height * 0.13, color, strokeWidth);
                break;
            case RenderSymbol.Symmetry:
                AppendSvgUnicodeSymbol(builder, bounds, color, "\u232F", 0.92, -0.01);
                break;
            case RenderSymbol.CircularRunout:
                AppendSvgUnicodeSymbol(builder, bounds, color, "\u2197", 0.78, -0.02);
                break;
            case RenderSymbol.TotalRunout:
                AppendSvgUnicodeSymbol(builder, bounds, color, "\u2330", 0.92, -0.01);
                break;
            case RenderSymbol.Diameter:
                AppendSvgEllipse(builder, Center(bounds), bounds.Width * 0.28, bounds.Height * 0.28, color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.24, 0.76), PointAt(bounds, 0.76, 0.24), color, strokeWidth);
                break;
            case RenderSymbol.MaximumMaterialCondition:
                AppendSvgCircledLetter(builder, bounds, color, strokeWidth, "M");
                break;
            case RenderSymbol.LeastMaterialCondition:
                AppendSvgCircledLetter(builder, bounds, color, strokeWidth, "L");
                break;
            case RenderSymbol.ProjectedToleranceZone:
                AppendSvgCircledLetter(builder, bounds, color, strokeWidth, "P");
                break;
            case RenderSymbol.FreeState:
                AppendSvgCircledLetter(builder, bounds, color, strokeWidth, "F");
                break;
            case RenderSymbol.SphericalDiameter:
                AppendSvgPrefixedSymbol(builder, bounds, color, strokeWidth, "S", RenderSymbol.Diameter);
                break;
            case RenderSymbol.SphericalRadius:
                AppendSvgPrefixedSymbol(builder, bounds, color, strokeWidth, "SR", RenderSymbol.Diameter);
                break;
            case RenderSymbol.DatumFeatureDirect:
                AppendSvgDatumFeatureSymbol(builder, bounds, color, strokeWidth, DatumFeatureSymbolStyle.Direct);
                break;
            case RenderSymbol.DatumFeatureLeaderLeft:
                AppendSvgDatumFeatureSymbol(builder, bounds, color, strokeWidth, DatumFeatureSymbolStyle.LeaderLeft);
                break;
            case RenderSymbol.DatumFeatureLeaderRight:
                AppendSvgDatumFeatureSymbol(builder, bounds, color, strokeWidth, DatumFeatureSymbolStyle.LeaderRight);
                break;
            case RenderSymbol.DatumFeatureLeaderDown:
                AppendSvgDatumFeatureSymbol(builder, bounds, color, strokeWidth, DatumFeatureSymbolStyle.LeaderDown);
                break;
        }
    }

    private static void DrawGdiSymbol(DrawingGraphics graphics, RenderSymbol symbol, DrawingRectangleF bounds, DrawingPen pen, DrawingBrush brush, DrawingFont textFont)
    {
        switch (symbol)
        {
            case RenderSymbol.Straightness:
                graphics.DrawLine(pen, PointAt(bounds, 0.15f, 0.5f), PointAt(bounds, 0.85f, 0.5f));
                break;
            case RenderSymbol.Flatness:
                graphics.DrawPolygon(pen, [PointAt(bounds, 0.18f, 0.32f), PointAt(bounds, 0.78f, 0.32f), PointAt(bounds, 0.62f, 0.72f), PointAt(bounds, 0.02f, 0.72f)]);
                break;
            case RenderSymbol.Circularity:
                graphics.DrawEllipse(pen, bounds.Left + (bounds.Width * 0.22f), bounds.Top + (bounds.Height * 0.22f), bounds.Width * 0.56f, bounds.Height * 0.56f);
                break;
            case RenderSymbol.Cylindricity:
                DrawGdiUnicodeSymbol(graphics, brush, bounds, "\u232D", 0.92, -0.01f);
                break;
            case RenderSymbol.ProfileOfALine:
                graphics.DrawBezier(pen, PointAt(bounds, 0.18f, 0.65f), PointAt(bounds, 0.32f, 0.2f), PointAt(bounds, 0.68f, 0.2f), PointAt(bounds, 0.82f, 0.65f));
                break;
            case RenderSymbol.ProfileOfASurface:
                graphics.DrawBezier(pen, PointAt(bounds, 0.18f, 0.65f), PointAt(bounds, 0.32f, 0.2f), PointAt(bounds, 0.68f, 0.2f), PointAt(bounds, 0.82f, 0.65f));
                graphics.DrawLine(pen, PointAt(bounds, 0.18f, 0.65f), PointAt(bounds, 0.82f, 0.65f));
                break;
            case RenderSymbol.Parallelism:
                graphics.DrawLine(pen, PointAt(bounds, 0.32f, 0.82f), PointAt(bounds, 0.50f, 0.18f));
                graphics.DrawLine(pen, PointAt(bounds, 0.50f, 0.82f), PointAt(bounds, 0.68f, 0.18f));
                break;
            case RenderSymbol.Perpendicularity:
                graphics.DrawLine(pen, PointAt(bounds, 0.5f, 0.18f), PointAt(bounds, 0.5f, 0.82f));
                graphics.DrawLine(pen, PointAt(bounds, 0.22f, 0.82f), PointAt(bounds, 0.78f, 0.82f));
                break;
            case RenderSymbol.Angularity:
                graphics.DrawLine(pen, PointAt(bounds, 0.18f, 0.82f), PointAt(bounds, 0.82f, 0.82f));
                graphics.DrawLine(pen, PointAt(bounds, 0.18f, 0.82f), PointAt(bounds, 0.62f, 0.22f));
                break;
            case RenderSymbol.Position:
                graphics.DrawEllipse(pen, bounds.Left + (bounds.Width * 0.28f), bounds.Top + (bounds.Height * 0.28f), bounds.Width * 0.44f, bounds.Height * 0.44f);
                graphics.DrawLine(pen, PointAt(bounds, 0.5f, 0.12f), PointAt(bounds, 0.5f, 0.88f));
                graphics.DrawLine(pen, PointAt(bounds, 0.12f, 0.5f), PointAt(bounds, 0.88f, 0.5f));
                break;
            case RenderSymbol.Concentricity:
                graphics.DrawEllipse(pen, bounds.Left + (bounds.Width * 0.2f), bounds.Top + (bounds.Height * 0.2f), bounds.Width * 0.6f, bounds.Height * 0.6f);
                graphics.DrawEllipse(pen, bounds.Left + (bounds.Width * 0.37f), bounds.Top + (bounds.Height * 0.37f), bounds.Width * 0.26f, bounds.Height * 0.26f);
                break;
            case RenderSymbol.Symmetry:
                DrawGdiUnicodeSymbol(graphics, brush, bounds, "\u232F", 0.92, -0.01f);
                break;
            case RenderSymbol.CircularRunout:
                DrawGdiUnicodeSymbol(graphics, brush, bounds, "\u2197", 0.78, -0.02f);
                break;
            case RenderSymbol.TotalRunout:
                DrawGdiUnicodeSymbol(graphics, brush, bounds, "\u2330", 0.92, -0.01f);
                break;
            case RenderSymbol.Diameter:
                graphics.DrawEllipse(pen, bounds.Left + (bounds.Width * 0.22f), bounds.Top + (bounds.Height * 0.22f), bounds.Width * 0.56f, bounds.Height * 0.56f);
                graphics.DrawLine(pen, PointAt(bounds, 0.24f, 0.76f), PointAt(bounds, 0.76f, 0.24f));
                break;
            case RenderSymbol.MaximumMaterialCondition:
                DrawGdiCircledLetter(graphics, pen, brush, textFont, bounds, "M");
                break;
            case RenderSymbol.LeastMaterialCondition:
                DrawGdiCircledLetter(graphics, pen, brush, textFont, bounds, "L");
                break;
            case RenderSymbol.ProjectedToleranceZone:
                DrawGdiCircledLetter(graphics, pen, brush, textFont, bounds, "P");
                break;
            case RenderSymbol.FreeState:
                DrawGdiCircledLetter(graphics, pen, brush, textFont, bounds, "F");
                break;
            case RenderSymbol.SphericalDiameter:
                DrawGdiPrefixedSymbol(graphics, pen, brush, textFont, bounds, "S", RenderSymbol.Diameter);
                break;
            case RenderSymbol.SphericalRadius:
                DrawGdiPrefixedSymbol(graphics, pen, brush, textFont, bounds, "SR", RenderSymbol.Diameter);
                break;
            case RenderSymbol.DatumFeatureDirect:
                DrawGdiDatumFeatureSymbol(graphics, pen, brush, bounds, DatumFeatureSymbolStyle.Direct);
                break;
            case RenderSymbol.DatumFeatureLeaderLeft:
                DrawGdiDatumFeatureSymbol(graphics, pen, brush, bounds, DatumFeatureSymbolStyle.LeaderLeft);
                break;
            case RenderSymbol.DatumFeatureLeaderRight:
                DrawGdiDatumFeatureSymbol(graphics, pen, brush, bounds, DatumFeatureSymbolStyle.LeaderRight);
                break;
            case RenderSymbol.DatumFeatureLeaderDown:
                DrawGdiDatumFeatureSymbol(graphics, pen, brush, bounds, DatumFeatureSymbolStyle.LeaderDown);
                break;
        }
    }

    private static void DrawWpfRunout(DrawingContext context, WpfPen pen, System.Windows.Media.Brush brush, Rect bounds, bool total)
    {
        if (total)
        {
            context.DrawLine(pen, PointAt(bounds, 0.22, 0.80), PointAt(bounds, 0.60, 0.80));
            context.DrawLine(pen, PointAt(bounds, 0.30, 0.80), PointAt(bounds, 0.52, 0.18));
            context.DrawLine(pen, PointAt(bounds, 0.56, 0.80), PointAt(bounds, 0.78, 0.18));
            DrawWpfFilledArrow(context, pen, brush, PointAt(bounds, 0.52, 0.18), PointAt(bounds, 0.43, 0.42));
            DrawWpfFilledArrow(context, pen, brush, PointAt(bounds, 0.78, 0.18), PointAt(bounds, 0.69, 0.42));
            return;
        }

        context.DrawLine(pen, PointAt(bounds, 0.28, 0.78), PointAt(bounds, 0.72, 0.20));
        DrawWpfFilledArrow(context, pen, brush, PointAt(bounds, 0.72, 0.20), PointAt(bounds, 0.63, 0.42));
    }

    private static void AppendSvgRunout(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, bool total)
    {
        if (total)
        {
            AppendSvgLine(builder, PointAt(bounds, 0.22, 0.80), PointAt(bounds, 0.60, 0.80), color, strokeWidth);
            AppendSvgLine(builder, PointAt(bounds, 0.30, 0.80), PointAt(bounds, 0.52, 0.18), color, strokeWidth);
            AppendSvgLine(builder, PointAt(bounds, 0.56, 0.80), PointAt(bounds, 0.78, 0.18), color, strokeWidth);
            AppendSvgFilledArrow(builder, PointAt(bounds, 0.52, 0.18), PointAt(bounds, 0.43, 0.42), color, strokeWidth);
            AppendSvgFilledArrow(builder, PointAt(bounds, 0.78, 0.18), PointAt(bounds, 0.69, 0.42), color, strokeWidth);
            return;
        }

        AppendSvgLine(builder, PointAt(bounds, 0.28, 0.78), PointAt(bounds, 0.72, 0.20), color, strokeWidth);
        AppendSvgFilledArrow(builder, PointAt(bounds, 0.72, 0.20), PointAt(bounds, 0.63, 0.42), color, strokeWidth);
    }

    private static void DrawGdiRunout(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingRectangleF bounds, bool total)
    {
        if (total)
        {
            graphics.DrawLine(pen, PointAt(bounds, 0.22f, 0.80f), PointAt(bounds, 0.60f, 0.80f));
            graphics.DrawLine(pen, PointAt(bounds, 0.30f, 0.80f), PointAt(bounds, 0.52f, 0.18f));
            graphics.DrawLine(pen, PointAt(bounds, 0.56f, 0.80f), PointAt(bounds, 0.78f, 0.18f));
            DrawGdiFilledArrow(graphics, pen, brush, PointAt(bounds, 0.52f, 0.18f), PointAt(bounds, 0.43f, 0.42f));
            DrawGdiFilledArrow(graphics, pen, brush, PointAt(bounds, 0.78f, 0.18f), PointAt(bounds, 0.69f, 0.42f));
            return;
        }

        graphics.DrawLine(pen, PointAt(bounds, 0.28f, 0.78f), PointAt(bounds, 0.72f, 0.20f));
        DrawGdiFilledArrow(graphics, pen, brush, PointAt(bounds, 0.72f, 0.20f), PointAt(bounds, 0.63f, 0.42f));
    }

    private static void DrawWpfSymmetry(DrawingContext context, WpfPen pen, Rect bounds)
    {
        context.DrawLine(pen, PointAt(bounds, 0.36, 0.31), PointAt(bounds, 0.64, 0.31));
        context.DrawLine(pen, PointAt(bounds, 0.18, 0.50), PointAt(bounds, 0.82, 0.50));
        context.DrawLine(pen, PointAt(bounds, 0.36, 0.69), PointAt(bounds, 0.64, 0.69));
    }

    private static void AppendSvgSymmetry(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        AppendSvgLine(builder, PointAt(bounds, 0.36, 0.31), PointAt(bounds, 0.64, 0.31), color, strokeWidth);
        AppendSvgLine(builder, PointAt(bounds, 0.18, 0.50), PointAt(bounds, 0.82, 0.50), color, strokeWidth);
        AppendSvgLine(builder, PointAt(bounds, 0.36, 0.69), PointAt(bounds, 0.64, 0.69), color, strokeWidth);
    }

    private static void DrawGdiSymmetry(DrawingGraphics graphics, DrawingPen pen, DrawingRectangleF bounds)
    {
        graphics.DrawLine(pen, PointAt(bounds, 0.36f, 0.31f), PointAt(bounds, 0.64f, 0.31f));
        graphics.DrawLine(pen, PointAt(bounds, 0.18f, 0.50f), PointAt(bounds, 0.82f, 0.50f));
        graphics.DrawLine(pen, PointAt(bounds, 0.36f, 0.69f), PointAt(bounds, 0.64f, 0.69f));
    }

    private static void DrawWpfUnicodeSymbol(DrawingContext context, Rect bounds, System.Windows.Media.Brush brush, string symbol, double pixelsPerDip, double sizeFactor, double verticalOffsetFactor)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * sizeFactor;
        var formatted = new FormattedText(symbol, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, SymbolTypeface, fontSize, brush, pixelsPerDip);
        var x = bounds.Left + ((bounds.Width - formatted.WidthIncludingTrailingWhitespace) / 2d);
        var y = bounds.Top + ((bounds.Height - formatted.Height) / 2d) + (bounds.Height * verticalOffsetFactor);
        context.DrawText(formatted, new WpfPoint(x, y));
    }

    private static void AppendSvgUnicodeSymbol(System.Text.StringBuilder builder, Rect bounds, string color, string symbol, double sizeFactor, double verticalOffsetFactor)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * sizeFactor;
        var x = bounds.Left + (bounds.Width / 2d);
        var y = bounds.Top + (bounds.Height / 2d) + (fontSize * 0.34) + (bounds.Height * verticalOffsetFactor);
        var text = SecurityElement.Escape(symbol) ?? string.Empty;
        builder.AppendLine($"  <text x=\"{Format(x)}\" y=\"{Format(y)}\" text-anchor=\"middle\" font-family=\"Segoe UI Symbol, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" fill=\"{color}\">{text}</text>");
    }

    private static void DrawGdiUnicodeSymbol(DrawingGraphics graphics, DrawingBrush brush, DrawingRectangleF bounds, string symbol, double sizeFactor, float verticalOffsetFactor)
    {
        using var font = new DrawingFont("Segoe UI Symbol", (float)(Math.Min(bounds.Width, bounds.Height) * sizeFactor), System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        using var format = new DrawingStringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        var adjusted = new DrawingRectangleF(bounds.Left, bounds.Top + (bounds.Height * verticalOffsetFactor), bounds.Width, bounds.Height);
        graphics.DrawString(symbol, font, brush, adjusted, format);
    }
    private static void DrawCircledLetter(DrawingContext context, Rect bounds, WpfPen pen, System.Windows.Media.Brush brush, string letter, double pixelsPerDip)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38;
        context.DrawEllipse(null, pen, Center(bounds), radius, radius);
        var formatted = new FormattedText(letter, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, TextTypeface, Math.Min(bounds.Width, bounds.Height) * 0.42, brush, pixelsPerDip);
        context.DrawText(formatted, new WpfPoint(bounds.Left + ((bounds.Width - formatted.Width) / 2d), bounds.Top + ((bounds.Height - formatted.Height) / 2d)));
    }

    private static void AppendSvgCircledLetter(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, string letter)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38;
        AppendSvgEllipse(builder, Center(bounds), radius, radius, color, strokeWidth);
        var fontSize = Math.Min(bounds.Width, bounds.Height) * 0.42;
        builder.AppendLine($"  <text x=\"{Format(bounds.Left + (bounds.Width / 2d))}\" y=\"{Format(bounds.Top + (bounds.Height / 2d) + (fontSize * 0.36))}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"700\" fill=\"{color}\">{letter}</text>");
    }

    private static void DrawGdiCircledLetter(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingFont font, DrawingRectangleF bounds, string letter)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38f;
        graphics.DrawEllipse(pen, bounds.Left + (bounds.Width / 2f) - radius, bounds.Top + (bounds.Height / 2f) - radius, radius * 2f, radius * 2f);
        using var format = new DrawingStringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        graphics.DrawString(letter, font, brush, bounds, format);
    }

    private static void DrawWpfCylindricity(DrawingContext context, WpfPen pen, Rect bounds)
    {
        context.DrawLine(pen, PointAt(bounds, 0.24, 0.80), PointAt(bounds, 0.43, 0.18));
        context.DrawLine(pen, PointAt(bounds, 0.57, 0.82), PointAt(bounds, 0.76, 0.20));
        context.DrawEllipse(null, pen, PointAt(bounds, 0.50, 0.53), bounds.Width * 0.19, bounds.Height * 0.19);
    }

    private static void AppendSvgCylindricity(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        AppendSvgLine(builder, PointAt(bounds, 0.24, 0.80), PointAt(bounds, 0.43, 0.18), color, strokeWidth);
        AppendSvgLine(builder, PointAt(bounds, 0.57, 0.82), PointAt(bounds, 0.76, 0.20), color, strokeWidth);
        AppendSvgEllipse(builder, PointAt(bounds, 0.50, 0.53), bounds.Width * 0.19, bounds.Height * 0.19, color, strokeWidth);
    }

    private static void DrawGdiCylindricity(DrawingGraphics graphics, DrawingPen pen, DrawingRectangleF bounds)
    {
        graphics.DrawLine(pen, PointAt(bounds, 0.24f, 0.80f), PointAt(bounds, 0.43f, 0.18f));
        graphics.DrawLine(pen, PointAt(bounds, 0.57f, 0.82f), PointAt(bounds, 0.76f, 0.20f));
        var center = PointAt(bounds, 0.50f, 0.53f);
        graphics.DrawEllipse(pen, center.X - bounds.Width * 0.19f, center.Y - bounds.Height * 0.19f, bounds.Width * 0.38f, bounds.Height * 0.38f);
    }

    private static StreamGeometry CreateSurfaceProfileGeometry(Rect bounds)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, 0.18, 0.65), false, true);
        ctx.BezierTo(PointAt(bounds, 0.32, 0.2), PointAt(bounds, 0.68, 0.2), PointAt(bounds, 0.82, 0.65), true, true);
        ctx.LineTo(PointAt(bounds, 0.18, 0.65), true, true);
        geometry.Freeze();
        return geometry;
    }

    private static void AppendSvgSurfaceProfile(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        builder.AppendLine($"  <path d=\"M {SvgPoint(bounds, 0.18, 0.65)} C {SvgPoint(bounds, 0.32, 0.2)} {SvgPoint(bounds, 0.68, 0.2)} {SvgPoint(bounds, 0.82, 0.65)} Z\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
    }

    private static void DrawWpfPrefixedSymbol(DrawingContext context, Rect bounds, WpfPen pen, System.Windows.Media.Brush brush, string prefix, RenderSymbol symbol, double pixelsPerDip)
    {
        var formatted = new FormattedText(prefix, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, TextTypeface, Math.Min(bounds.Width, bounds.Height) * 0.48, brush, pixelsPerDip);
        var textWidth = formatted.Width;
        var symbolSize = bounds.Height * 0.7;
        var totalWidth = textWidth + symbolSize;
        var startX = bounds.Left + (bounds.Width - totalWidth) / 2d;
        context.DrawText(formatted, new WpfPoint(startX, bounds.Top + (bounds.Height - formatted.Height) / 2d));
        var symbolBounds = new Rect(startX + textWidth, bounds.Top + (bounds.Height - symbolSize) / 2d, symbolSize, symbolSize);
        DrawWpfSymbol(context, symbol, symbolBounds, pen, brush, pixelsPerDip);
    }

    private static void AppendSvgPrefixedSymbol(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, string prefix, RenderSymbol symbol)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * 0.48;
        var textWidth = prefix.Length * fontSize * 0.55;
        var symbolSize = bounds.Height * 0.7;
        var totalWidth = textWidth + symbolSize;
        var startX = bounds.Left + (bounds.Width - totalWidth) / 2d;
        builder.AppendLine($"  <text x=\"{Format(startX + textWidth / 2d)}\" y=\"{Format(bounds.Top + bounds.Height * 0.65)}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{prefix}</text>");
        var symbolBounds = new Rect(startX + textWidth, bounds.Top + (bounds.Height - symbolSize) / 2d, symbolSize, symbolSize);
        AppendSvgSymbol(builder, symbol, symbolBounds, color, strokeWidth);
    }

    private static void DrawGdiPrefixedSymbol(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingFont font, DrawingRectangleF bounds, string prefix, RenderSymbol symbol)
    {
        var textSize = graphics.MeasureString(prefix, font);
        var symbolSize = bounds.Height * 0.7f;
        var totalWidth = textSize.Width + symbolSize;
        var startX = bounds.Left + (bounds.Width - totalWidth) / 2f;
        using var format = new DrawingStringFormat { LineAlignment = System.Drawing.StringAlignment.Center };
        graphics.DrawString(prefix, font, brush, new DrawingRectangleF(startX, bounds.Top, textSize.Width, bounds.Height), format);
        var symbolBounds = new DrawingRectangleF(startX + textSize.Width, bounds.Top + (bounds.Height - symbolSize) / 2f, symbolSize, symbolSize);
        DrawGdiSymbol(graphics, symbol, symbolBounds, pen, brush, font);
    }
    private static void DrawWpfDatumFeatureSymbol(DrawingContext context, Rect bounds, WpfPen pen, System.Windows.Media.Brush brush, DatumFeatureSymbolStyle style)
    {
        switch (style)
        {
            case DatumFeatureSymbolStyle.Direct:
                context.DrawLine(pen, PointAt(bounds, 0.22, 0.72), PointAt(bounds, 0.78, 0.72));
                context.DrawGeometry(brush, pen, CreatePolygonGeometry(bounds, 0.50, 0.20, 0.34, 0.50, 0.66, 0.50));
                break;
            case DatumFeatureSymbolStyle.LeaderLeft:
                context.DrawLine(pen, PointAt(bounds, 0.72, 0.28), PointAt(bounds, 0.44, 0.28));
                context.DrawLine(pen, PointAt(bounds, 0.44, 0.28), PointAt(bounds, 0.30, 0.58));
                context.DrawGeometry(brush, pen, CreatePolygonGeometry(bounds, 0.18, 0.70, 0.32, 0.54, 0.32, 0.84));
                break;
            case DatumFeatureSymbolStyle.LeaderRight:
                context.DrawLine(pen, PointAt(bounds, 0.28, 0.28), PointAt(bounds, 0.56, 0.28));
                context.DrawLine(pen, PointAt(bounds, 0.56, 0.28), PointAt(bounds, 0.70, 0.58));
                context.DrawGeometry(brush, pen, CreatePolygonGeometry(bounds, 0.82, 0.70, 0.68, 0.54, 0.68, 0.84));
                break;
            case DatumFeatureSymbolStyle.LeaderDown:
                context.DrawLine(pen, PointAt(bounds, 0.50, 0.18), PointAt(bounds, 0.50, 0.56));
                context.DrawGeometry(brush, pen, CreatePolygonGeometry(bounds, 0.50, 0.82, 0.34, 0.58, 0.66, 0.58));
                break;
        }
    }

    private static void AppendSvgDatumFeatureSymbol(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, DatumFeatureSymbolStyle style)
    {
        switch (style)
        {
            case DatumFeatureSymbolStyle.Direct:
                AppendSvgLine(builder, PointAt(bounds, 0.22, 0.72), PointAt(bounds, 0.78, 0.72), color, strokeWidth);
                builder.AppendLine($"  <polygon points=\"{SvgPoint(bounds, 0.50, 0.20)} {SvgPoint(bounds, 0.34, 0.50)} {SvgPoint(bounds, 0.66, 0.50)}\" fill=\"{color}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
                break;
            case DatumFeatureSymbolStyle.LeaderLeft:
                AppendSvgLine(builder, PointAt(bounds, 0.72, 0.28), PointAt(bounds, 0.44, 0.28), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.44, 0.28), PointAt(bounds, 0.30, 0.58), color, strokeWidth);
                builder.AppendLine($"  <polygon points=\"{SvgPoint(bounds, 0.18, 0.70)} {SvgPoint(bounds, 0.32, 0.54)} {SvgPoint(bounds, 0.32, 0.84)}\" fill=\"{color}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
                break;
            case DatumFeatureSymbolStyle.LeaderRight:
                AppendSvgLine(builder, PointAt(bounds, 0.28, 0.28), PointAt(bounds, 0.56, 0.28), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.56, 0.28), PointAt(bounds, 0.70, 0.58), color, strokeWidth);
                builder.AppendLine($"  <polygon points=\"{SvgPoint(bounds, 0.82, 0.70)} {SvgPoint(bounds, 0.68, 0.54)} {SvgPoint(bounds, 0.68, 0.84)}\" fill=\"{color}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
                break;
            case DatumFeatureSymbolStyle.LeaderDown:
                AppendSvgLine(builder, PointAt(bounds, 0.50, 0.18), PointAt(bounds, 0.50, 0.56), color, strokeWidth);
                builder.AppendLine($"  <polygon points=\"{SvgPoint(bounds, 0.50, 0.82)} {SvgPoint(bounds, 0.34, 0.58)} {SvgPoint(bounds, 0.66, 0.58)}\" fill=\"{color}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
                break;
        }
    }

    private static void DrawGdiDatumFeatureSymbol(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingRectangleF bounds, DatumFeatureSymbolStyle style)
    {
        switch (style)
        {
            case DatumFeatureSymbolStyle.Direct:
                graphics.DrawLine(pen, PointAt(bounds, 0.22f, 0.72f), PointAt(bounds, 0.78f, 0.72f));
                graphics.FillPolygon(brush, [PointAt(bounds, 0.50f, 0.20f), PointAt(bounds, 0.34f, 0.50f), PointAt(bounds, 0.66f, 0.50f)]);
                graphics.DrawPolygon(pen, [PointAt(bounds, 0.50f, 0.20f), PointAt(bounds, 0.34f, 0.50f), PointAt(bounds, 0.66f, 0.50f)]);
                break;
            case DatumFeatureSymbolStyle.LeaderLeft:
                graphics.DrawLine(pen, PointAt(bounds, 0.72f, 0.28f), PointAt(bounds, 0.44f, 0.28f));
                graphics.DrawLine(pen, PointAt(bounds, 0.44f, 0.28f), PointAt(bounds, 0.30f, 0.58f));
                graphics.FillPolygon(brush, [PointAt(bounds, 0.18f, 0.70f), PointAt(bounds, 0.32f, 0.54f), PointAt(bounds, 0.32f, 0.84f)]);
                graphics.DrawPolygon(pen, [PointAt(bounds, 0.18f, 0.70f), PointAt(bounds, 0.32f, 0.54f), PointAt(bounds, 0.32f, 0.84f)]);
                break;
            case DatumFeatureSymbolStyle.LeaderRight:
                graphics.DrawLine(pen, PointAt(bounds, 0.28f, 0.28f), PointAt(bounds, 0.56f, 0.28f));
                graphics.DrawLine(pen, PointAt(bounds, 0.56f, 0.28f), PointAt(bounds, 0.70f, 0.58f));
                graphics.FillPolygon(brush, [PointAt(bounds, 0.82f, 0.70f), PointAt(bounds, 0.68f, 0.54f), PointAt(bounds, 0.68f, 0.84f)]);
                graphics.DrawPolygon(pen, [PointAt(bounds, 0.82f, 0.70f), PointAt(bounds, 0.68f, 0.54f), PointAt(bounds, 0.68f, 0.84f)]);
                break;
            case DatumFeatureSymbolStyle.LeaderDown:
                graphics.DrawLine(pen, PointAt(bounds, 0.50f, 0.18f), PointAt(bounds, 0.50f, 0.56f));
                graphics.FillPolygon(brush, [PointAt(bounds, 0.50f, 0.82f), PointAt(bounds, 0.34f, 0.58f), PointAt(bounds, 0.66f, 0.58f)]);
                graphics.DrawPolygon(pen, [PointAt(bounds, 0.50f, 0.82f), PointAt(bounds, 0.34f, 0.58f), PointAt(bounds, 0.66f, 0.58f)]);
                break;
        }
    }

    private static StreamGeometry CreatePolygonGeometry(Rect bounds, params double[] coordinates)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, coordinates[0], coordinates[1]), false, true);
        for (var index = 2; index < coordinates.Length; index += 2)
        {
            ctx.LineTo(PointAt(bounds, coordinates[index], coordinates[index + 1]), true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    private static StreamGeometry CreateBezierGeometry(Rect bounds, double x1, double y1, double cx1, double cy1, double cx2, double cy2, double x2, double y2)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, x1, y1), false, false);
        ctx.BezierTo(PointAt(bounds, cx1, cy1), PointAt(bounds, cx2, cy2), PointAt(bounds, x2, y2), true, true);
        geometry.Freeze();
        return geometry;
    }

    private static void DrawWpfArrow(DrawingContext context, WpfPen pen, WpfPoint tip, WpfPoint anchor)
    {
        var direction = anchor - tip;
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(12d, segmentLength * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = tip + (direction * headLength) + (normal * headWidth);
        var right = tip + (direction * headLength) - (normal * headWidth);
        context.DrawLine(pen, tip, left);
        context.DrawLine(pen, tip, right);
    }

    private static void AppendSvgArrow(System.Text.StringBuilder builder, WpfPoint tip, WpfPoint anchor, string color, double strokeWidth)
    {
        var direction = anchor - tip;
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(12d, segmentLength * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = tip + (direction * headLength) + (normal * headWidth);
        var right = tip + (direction * headLength) - (normal * headWidth);
        AppendSvgLine(builder, tip, left, color, strokeWidth);
        AppendSvgLine(builder, tip, right, color, strokeWidth);
    }

    private static void DrawGdiArrow(DrawingGraphics graphics, DrawingPen pen, DrawingPointF tip, DrawingPointF anchor)
    {
        var direction = new Vector(anchor.X - tip.X, anchor.Y - tip.Y);
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(12d, segmentLength * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = new DrawingPointF((float)(tip.X + (direction.X * headLength) + (normal.X * headWidth)), (float)(tip.Y + (direction.Y * headLength) + (normal.Y * headWidth)));
        var right = new DrawingPointF((float)(tip.X + (direction.X * headLength) - (normal.X * headWidth)), (float)(tip.Y + (direction.Y * headLength) - (normal.Y * headWidth)));
        graphics.DrawLine(pen, tip, left);
        graphics.DrawLine(pen, tip, right);
    }

    private static void DrawWpfFilledArrow(DrawingContext context, WpfPen pen, System.Windows.Media.Brush brush, WpfPoint tip, WpfPoint anchor)
    {
        var direction = anchor - tip;
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(10d, segmentLength * 0.7d));
        var headWidth = headLength * 0.42d;
        var left = tip + (direction * headLength) + (normal * headWidth);
        var right = tip + (direction * headLength) - (normal * headWidth);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(tip, true, true);
            ctx.LineTo(left, true, true);
            ctx.LineTo(right, true, true);
        }
        geometry.Freeze();
        context.DrawGeometry(brush, pen, geometry);
    }

    private static void AppendSvgFilledArrow(System.Text.StringBuilder builder, WpfPoint tip, WpfPoint anchor, string color, double strokeWidth)
    {
        var direction = anchor - tip;
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(10d, segmentLength * 0.7d));
        var headWidth = headLength * 0.42d;
        var left = tip + (direction * headLength) + (normal * headWidth);
        var right = tip + (direction * headLength) - (normal * headWidth);
        builder.AppendLine($"  <polygon points=\"{Format(tip.X)} {Format(tip.Y)} {Format(left.X)} {Format(left.Y)} {Format(right.X)} {Format(right.Y)}\" fill=\"{color}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linejoin=\"round\" />");
    }

    private static void DrawGdiFilledArrow(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingPointF tip, DrawingPointF anchor)
    {
        var direction = new Vector(anchor.X - tip.X, anchor.Y - tip.Y);
        if (direction.LengthSquared < 0.001d)
        {
            return;
        }

        var segmentLength = direction.Length;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        var headLength = Math.Max(5d, Math.Min(10d, segmentLength * 0.7d));
        var headWidth = headLength * 0.42d;
        var left = new DrawingPointF((float)(tip.X + (direction.X * headLength) + (normal.X * headWidth)), (float)(tip.Y + (direction.Y * headLength) + (normal.Y * headWidth)));
        var right = new DrawingPointF((float)(tip.X + (direction.X * headLength) - (normal.X * headWidth)), (float)(tip.Y + (direction.Y * headLength) - (normal.Y * headWidth)));
        graphics.FillPolygon(brush, [tip, left, right]);
        graphics.DrawPolygon(pen, [tip, left, right]);
    }
    private static void AppendSvgLine(System.Text.StringBuilder builder, WpfPoint start, WpfPoint end, string color, double strokeWidth)
    {
        builder.AppendLine($"  <line x1=\"{Format(start.X)}\" y1=\"{Format(start.Y)}\" x2=\"{Format(end.X)}\" y2=\"{Format(end.Y)}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" />");
    }

    private static void AppendSvgEllipse(System.Text.StringBuilder builder, WpfPoint center, double rx, double ry, string color, double strokeWidth)
    {
        builder.AppendLine($"  <ellipse cx=\"{Format(center.X)}\" cy=\"{Format(center.Y)}\" rx=\"{Format(rx)}\" ry=\"{Format(ry)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
    }

    private static string SvgPoint(Rect bounds, double x, double y)
    {
        var point = PointAt(bounds, x, y);
        return $"{Format(point.X)} {Format(point.Y)}";
    }

    private static WpfPoint Center(Rect bounds)
    {
        return new WpfPoint(bounds.Left + (bounds.Width / 2d), bounds.Top + (bounds.Height / 2d));
    }

    private static WpfPoint PointAt(Rect bounds, double x, double y)
    {
        return new WpfPoint(bounds.Left + (bounds.Width * x), bounds.Top + (bounds.Height * y));
    }

    private static DrawingPointF PointAt(DrawingRectangleF bounds, float x, float y)
    {
        return new DrawingPointF(bounds.Left + (bounds.Width * x), bounds.Top + (bounds.Height * y));
    }

    private static string Format(double value)
    {
        return value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}








