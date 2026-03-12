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
using GdtCreator.Core.Rendering;

namespace GdtCreator.Wpf.Rendering;

public static class SymbolRenderer
{
    private static readonly Typeface TextTypeface = new(new WpfFontFamily("Bahnschrift"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.SemiCondensed);
    private static readonly WpfColor DefaultWpfContentColor = WpfColor.FromRgb(16, 42, 67);
    private static readonly DrawingColor DefaultDrawingContentColor = DrawingColor.FromArgb(16, 42, 67);

    public static void DrawSymbolIcon(DrawingContext context, RenderSymbol symbol, Rect bounds, double strokeThickness, double pixelsPerDip)
    {
        var brush = CreateWpfBrush(DefaultWpfContentColor);
        var pen = CreateWpfPen(strokeThickness, DefaultWpfContentColor);
        DrawWpfSymbol(context, symbol, bounds, pen, brush, pixelsPerDip);
    }

    public static void DrawModel(DrawingContext context, ToleranceRenderModel model, Rect bounds, double pixelsPerDip)
    {
        var scale = Math.Min(bounds.Width / model.Width, bounds.Height / model.Height);
        scale = double.IsFinite(scale) && scale > 0 ? scale : 1d;

        var originX = bounds.Left + ((bounds.Width - (model.Width * scale)) / 2d);
        var originY = bounds.Top + ((bounds.Height - (model.Height * scale)) / 2d);
        const double frameX = 0d;
        var frameY = model.TopTextHeight > 0d ? model.TopTextHeight + model.TextGap : 0d;
        var contentColor = ParseWpfColor(model.ContentColorHex);
        var captionBrush = CreateWpfBrush(contentColor);

        context.PushTransform(new TranslateTransform(originX, originY));
        context.PushTransform(new ScaleTransform(scale, scale));

        if (!string.IsNullOrWhiteSpace(model.TopText))
        {
            var topText = new FormattedText(model.TopText, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, TextTypeface, 16d, captionBrush, pixelsPerDip);
            context.DrawText(topText, new WpfPoint(0d, (model.TopTextHeight - topText.Height) / 2d));
        }

        context.PushTransform(new TranslateTransform(frameX, frameY));
        var outlinePen = CreateWpfPen(model.StrokeThickness, contentColor);
        context.DrawRectangle(WpfBrushes.White, outlinePen, new Rect(0, 0, model.FrameWidth, model.FrameHeight));

        double cursor = 0d;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            if (index > 0)
            {
                context.DrawLine(outlinePen, new WpfPoint(cursor, 0), new WpfPoint(cursor, model.FrameHeight));
            }

            DrawCell(context, cell, cursor, model.FrameHeight, pixelsPerDip, model.StrokeThickness, contentColor);
            cursor += cell.Width;
        }

        context.Pop();

        if (!string.IsNullOrWhiteSpace(model.BottomText))
        {
            var bottomText = new FormattedText(model.BottomText, CultureInfo.InvariantCulture, WpfFlowDirection.LeftToRight, TextTypeface, 16d, captionBrush, pixelsPerDip);
            var bottomY = frameY + model.FrameHeight + model.TextGap + ((model.BottomTextHeight - bottomText.Height) / 2d);
            context.DrawText(bottomText, new WpfPoint(0d, bottomY));
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
        const double frameX = 0d;
        var frameY = (model.TopTextHeight > 0d ? model.TopTextHeight + model.TextGap : 0d) * scale;
        var frameWidth = model.FrameWidth * scale;
        var frameHeight = model.FrameHeight * scale;
        var strokeWidth = model.StrokeThickness * scale;
        var color = NormalizeSvgColor(model.ContentColorHex);
        var builder = new System.Text.StringBuilder();

        builder.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{Format(width)}\" height=\"{Format(height)}\" viewBox=\"0 0 {Format(width)} {Format(height)}\">");
        builder.AppendLine($"  <rect width=\"{Format(width)}\" height=\"{Format(height)}\" fill=\"#FFFFFF\" />");

        if (!string.IsNullOrWhiteSpace(model.TopText))
        {
            var fontSize = 16d * scale;
            var text = SecurityElement.Escape(model.TopText) ?? string.Empty;
            var y = ((model.TopTextHeight * scale) / 2d) + (fontSize / 3.2d);
            builder.AppendLine($"  <text x=\"0\" y=\"{Format(y)}\" text-anchor=\"start\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
        }

        builder.AppendLine($"  <rect x=\"{Format(frameX)}\" y=\"{Format(frameY)}\" width=\"{Format(frameWidth)}\" height=\"{Format(frameHeight)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");

        double cursor = frameX;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            var scaledCellWidth = cell.Width * scale;
            if (index > 0)
            {
                builder.AppendLine($"  <line x1=\"{Format(cursor)}\" y1=\"{Format(frameY)}\" x2=\"{Format(cursor)}\" y2=\"{Format(frameY + frameHeight)}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
            }

            var tokenWidth = cell.Tokens.Sum(RenderLayout.GetTokenAdvance) * scale;
            var tokenCursor = cursor + ((scaledCellWidth - tokenWidth) / 2d);

            foreach (var token in cell.Tokens)
            {
                var advance = RenderLayout.GetTokenAdvance(token) * scale;
                var tokenRect = new Rect(tokenCursor + ((advance - Math.Min(Math.Max(advance - 4d, 16d), Math.Max(frameHeight - 6d, 16d))) / 2d), frameY + ((frameHeight - Math.Min(Math.Max(advance - 4d, 16d), Math.Max(frameHeight - 6d, 16d))) / 2d), Math.Min(Math.Max(advance - 4d, 16d), Math.Max(frameHeight - 6d, 16d)), Math.Min(Math.Max(advance - 4d, 16d), Math.Max(frameHeight - 6d, 16d)));
                if (token.IsSymbol)
                {
                    AppendSvgSymbol(builder, token.Symbol!.Value, tokenRect, color, strokeWidth);
                }
                else
                {
                    var text = SecurityElement.Escape(token.Text) ?? string.Empty;
                    var fontSize = 16d * scale;
                    builder.AppendLine($"  <text x=\"{Format(tokenCursor + (advance / 2d))}\" y=\"{Format(frameY + (frameHeight / 2d) + (fontSize / 3.2d))}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
                }

                tokenCursor += advance;
            }

            cursor += scaledCellWidth;
        }

        if (!string.IsNullOrWhiteSpace(model.BottomText))
        {
            var fontSize = 16d * scale;
            var text = SecurityElement.Escape(model.BottomText) ?? string.Empty;
            var y = frameY + frameHeight + (model.TextGap * scale) + ((model.BottomTextHeight * scale) / 2d) + (fontSize / 3.2d);
            builder.AppendLine($"  <text x=\"0\" y=\"{Format(y)}\" text-anchor=\"start\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
        }

        builder.AppendLine("</svg>");
        return builder.ToString();
    }

    public static byte[] BuildEmf(ToleranceRenderModel model, double scale)
    {
        var width = (float)(model.Width * scale);
        var height = (float)(model.Height * scale);
        const float frameX = 0f;
        var frameY = (float)((model.TopTextHeight > 0d ? model.TopTextHeight + model.TextGap : 0d) * scale);
        var frameWidth = (float)(model.FrameWidth * scale);
        var frameHeight = (float)(model.FrameHeight * scale);
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
            var contentColor = ParseDrawingColor(model.ContentColorHex);
            using var pen = new DrawingPen(contentColor, (float)(model.StrokeThickness * scale));
            using var textBrush = new System.Drawing.SolidBrush(contentColor);
            using var textFont = new DrawingFont("Bahnschrift", (float)(16d * scale), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);

            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;
            graphics.Clear(DrawingColor.White);

            if (!string.IsNullOrWhiteSpace(model.TopText))
            {
                using var topFormat = new DrawingStringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Near,
                    LineAlignment = System.Drawing.StringAlignment.Center
                };
                graphics.DrawString(model.TopText, textFont, textBrush, new DrawingRectangleF(0f, 0f, width, (float)(model.TopTextHeight * scale)), topFormat);
            }

            graphics.DrawRectangle(pen, frameX, frameY, frameWidth - pen.Width, frameHeight - pen.Width);

            float cursor = frameX;
            foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
            {
                var scaledCellWidth = (float)(cell.Width * scale);
                if (index > 0)
                {
                    graphics.DrawLine(pen, cursor, frameY, cursor, frameY + frameHeight);
                }

                var tokenWidth = (float)(cell.Tokens.Sum(RenderLayout.GetTokenAdvance) * scale);
                var tokenCursor = cursor + ((scaledCellWidth - tokenWidth) / 2f);

                foreach (var token in cell.Tokens)
                {
                    var advance = (float)(RenderLayout.GetTokenAdvance(token) * scale);
                    var tokenRect = CreateSymbolBounds(tokenCursor, advance, frameHeight);
                    tokenRect.Y += frameY;
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
                        graphics.DrawString(token.Text, textFont, textBrush, new DrawingRectangleF(tokenCursor, frameY, advance, frameHeight), format);
                    }

                    tokenCursor += advance;
                }

                cursor += scaledCellWidth;
            }

            if (!string.IsNullOrWhiteSpace(model.BottomText))
            {
                using var bottomFormat = new DrawingStringFormat
                {
                    Alignment = System.Drawing.StringAlignment.Near,
                    LineAlignment = System.Drawing.StringAlignment.Center
                };
                var bottomY = frameY + frameHeight + (float)(model.TextGap * scale);
                graphics.DrawString(model.BottomText, textFont, textBrush, new DrawingRectangleF(0f, bottomY, width, (float)(model.BottomTextHeight * scale)), bottomFormat);
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
        var size = Math.Min(Math.Max(advance - 4d, 16d), Math.Max(cellHeight - 6d, 16d));
        return new Rect(startX + ((advance - size) / 2d), (cellHeight - size) / 2d, size, size);
    }

    private static DrawingRectangleF CreateSymbolBounds(float startX, float advance, float cellHeight)
    {
        var size = MathF.Min(MathF.Max(advance - 4f, 16f), MathF.Max(cellHeight - 6f, 16f));
        return new DrawingRectangleF(startX + ((advance - size) / 2f), (cellHeight - size) / 2f, size, size);
    }
    private static void DrawCell(DrawingContext context, ToleranceCell cell, double originX, double height, double pixelsPerDip, double strokeThickness, WpfColor contentColor)
    {
        var tokenWidth = cell.Tokens.Sum(RenderLayout.GetTokenAdvance);
        var cursor = originX + ((cell.Width - tokenWidth) / 2d);
        var brush = CreateWpfBrush(contentColor);
        var pen = CreateWpfPen(strokeThickness, contentColor);

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
                    16d,
                    brush,
                    pixelsPerDip);
                context.DrawText(formatted, new WpfPoint(cursor + ((advance - formatted.Width) / 2d), (height - formatted.Height) / 2d));
            }

            cursor += advance;
        }
    }

    private static SolidColorBrush CreateWpfBrush(WpfColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static WpfPen CreateWpfPen(double strokeThickness, WpfColor color)
    {
        var pen = new WpfPen(CreateWpfBrush(color), strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        pen.Freeze();
        return pen;
    }

    private static WpfColor ParseWpfColor(string? colorHex)
    {
        try
        {
            return (WpfColor)System.Windows.Media.ColorConverter.ConvertFromString(colorHex ?? "#102A43");
        }
        catch
        {
            return DefaultWpfContentColor;
        }
    }

    private static DrawingColor ParseDrawingColor(string? colorHex)
    {
        try
        {
            return System.Drawing.ColorTranslator.FromHtml(colorHex ?? "#102A43");
        }
        catch
        {
            return DefaultDrawingContentColor;
        }
    }

    private static string NormalizeSvgColor(string? colorHex)
    {
        return string.IsNullOrWhiteSpace(colorHex) ? "#102A43" : colorHex;
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
                DrawWpfCylindricity(context, pen, bounds);
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
                context.DrawLine(pen, PointAt(bounds, 0.20, 0.26), PointAt(bounds, 0.80, 0.26));
                context.DrawLine(pen, PointAt(bounds, 0.20, 0.50), PointAt(bounds, 0.80, 0.50));
                context.DrawLine(pen, PointAt(bounds, 0.20, 0.74), PointAt(bounds, 0.80, 0.74));
                break;
            case RenderSymbol.CircularRunout:
                DrawWpfRunout(context, pen, bounds, false);
                break;
            case RenderSymbol.TotalRunout:
                DrawWpfRunout(context, pen, bounds, true);
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
                AppendSvgCylindricity(builder, bounds, color, strokeWidth);
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
                AppendSvgLine(builder, PointAt(bounds, 0.20, 0.26), PointAt(bounds, 0.80, 0.26), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.20, 0.50), PointAt(bounds, 0.80, 0.50), color, strokeWidth);
                AppendSvgLine(builder, PointAt(bounds, 0.20, 0.74), PointAt(bounds, 0.80, 0.74), color, strokeWidth);
                break;
            case RenderSymbol.CircularRunout:
                AppendSvgRunout(builder, bounds, color, strokeWidth, false);
                break;
            case RenderSymbol.TotalRunout:
                AppendSvgRunout(builder, bounds, color, strokeWidth, true);
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
                DrawGdiCylindricity(graphics, pen, bounds);
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
                graphics.DrawLine(pen, PointAt(bounds, 0.20f, 0.26f), PointAt(bounds, 0.80f, 0.26f));
                graphics.DrawLine(pen, PointAt(bounds, 0.20f, 0.50f), PointAt(bounds, 0.80f, 0.50f));
                graphics.DrawLine(pen, PointAt(bounds, 0.20f, 0.74f), PointAt(bounds, 0.80f, 0.74f));
                break;
            case RenderSymbol.CircularRunout:
                DrawGdiRunout(graphics, pen, bounds, false);
                break;
            case RenderSymbol.TotalRunout:
                DrawGdiRunout(graphics, pen, bounds, true);
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
        }
    }

    private static void DrawWpfRunout(DrawingContext context, WpfPen pen, Rect bounds, bool total)
    {
        // Arc from bottom-left to upper-right
        var arcGeometry = new StreamGeometry();
        using (var ctx = arcGeometry.Open())
        {
            ctx.BeginFigure(PointAt(bounds, 0.15, 0.75), false, false);
            ctx.ArcTo(PointAt(bounds, 0.78, 0.32), new System.Windows.Size(bounds.Width * 0.55, bounds.Height * 0.55), 0, false, SweepDirection.Counterclockwise, true, true);
        }
        arcGeometry.Freeze();
        context.DrawGeometry(null, pen, arcGeometry);
        // Arrow at end (upper-right)
        DrawWpfArrow(context, pen, PointAt(bounds, 0.78, 0.32), PointAt(bounds, 0.60, 0.22));
        if (total)
        {
            // Second arrow at start (lower-left)
            DrawWpfArrow(context, pen, PointAt(bounds, 0.15, 0.75), PointAt(bounds, 0.30, 0.82));
        }
    }

    private static void AppendSvgRunout(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, bool total)
    {
        var p1 = PointAt(bounds, 0.15, 0.75);
        var p2 = PointAt(bounds, 0.78, 0.32);
        var rx = bounds.Width * 0.55;
        var ry = bounds.Height * 0.55;
        builder.AppendLine($"  <path d=\"M {Format(p1.X)} {Format(p1.Y)} A {Format(rx)} {Format(ry)} 0 0 0 {Format(p2.X)} {Format(p2.Y)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" />");
        AppendSvgArrow(builder, PointAt(bounds, 0.78, 0.32), PointAt(bounds, 0.60, 0.22), color, strokeWidth);
        if (total)
        {
            AppendSvgArrow(builder, PointAt(bounds, 0.15, 0.75), PointAt(bounds, 0.30, 0.82), color, strokeWidth);
        }
    }

    private static void DrawGdiRunout(DrawingGraphics graphics, DrawingPen pen, DrawingRectangleF bounds, bool total)
    {
        var rx = bounds.Width * 0.55f;
        var ry = bounds.Height * 0.55f;
        var cx = bounds.Left + bounds.Width * 0.15f + rx * 0.25f;
        var cy = bounds.Top + bounds.Height * 0.75f - ry * 0.5f;
        graphics.DrawArc(pen, cx - rx, cy - ry, rx * 2f, ry * 2f, 190f, -100f);
        DrawGdiArrow(graphics, pen, PointAt(bounds, 0.78f, 0.32f), PointAt(bounds, 0.60f, 0.22f));
        if (total)
        {
            DrawGdiArrow(graphics, pen, PointAt(bounds, 0.15f, 0.75f), PointAt(bounds, 0.30f, 0.82f));
        }
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
        var cx = bounds.Left + bounds.Width * 0.5;
        var rx = bounds.Width * 0.25;
        var ry = bounds.Height * 0.14;
        var topY = bounds.Top + bounds.Height * 0.24;
        var botY = bounds.Top + bounds.Height * 0.76;
        context.DrawLine(pen, new WpfPoint(cx - rx, topY), new WpfPoint(cx - rx, botY));
        context.DrawLine(pen, new WpfPoint(cx + rx, topY), new WpfPoint(cx + rx, botY));
        context.DrawEllipse(null, pen, new WpfPoint(cx, topY), rx, ry);
        var arcGeometry = new StreamGeometry();
        using (var ctx = arcGeometry.Open())
        {
            ctx.BeginFigure(new WpfPoint(cx - rx, botY), false, false);
            ctx.ArcTo(new WpfPoint(cx + rx, botY), new System.Windows.Size(rx, ry), 0, true, SweepDirection.Clockwise, true, true);
        }
        arcGeometry.Freeze();
        context.DrawGeometry(null, pen, arcGeometry);
    }

    private static void AppendSvgCylindricity(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        var cx = bounds.Left + bounds.Width * 0.5;
        var rx = bounds.Width * 0.25;
        var ry = bounds.Height * 0.14;
        var topY = bounds.Top + bounds.Height * 0.24;
        var botY = bounds.Top + bounds.Height * 0.76;
        AppendSvgLine(builder, new WpfPoint(cx - rx, topY), new WpfPoint(cx - rx, botY), color, strokeWidth);
        AppendSvgLine(builder, new WpfPoint(cx + rx, topY), new WpfPoint(cx + rx, botY), color, strokeWidth);
        AppendSvgEllipse(builder, new WpfPoint(cx, topY), rx, ry, color, strokeWidth);
        builder.AppendLine($"  <path d=\"M {Format(cx - rx)} {Format(botY)} A {Format(rx)} {Format(ry)} 0 1 0 {Format(cx + rx)} {Format(botY)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
    }

    private static void DrawGdiCylindricity(DrawingGraphics graphics, DrawingPen pen, DrawingRectangleF bounds)
    {
        var cx = bounds.Left + bounds.Width * 0.5f;
        var rx = bounds.Width * 0.25f;
        var ry = bounds.Height * 0.14f;
        var topY = bounds.Top + bounds.Height * 0.24f;
        var botY = bounds.Top + bounds.Height * 0.76f;
        graphics.DrawLine(pen, cx - rx, topY, cx - rx, botY);
        graphics.DrawLine(pen, cx + rx, topY, cx + rx, botY);
        graphics.DrawEllipse(pen, cx - rx, topY - ry, rx * 2f, ry * 2f);
        graphics.DrawArc(pen, cx - rx, botY - ry, rx * 2f, ry * 2f, 0f, 180f);
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






