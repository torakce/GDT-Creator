using System.Globalization;
using System.IO;
using System.Security;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DrawingBrush = System.Drawing.Brush;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingPen = System.Drawing.Pen;
using DrawingPointF = System.Drawing.PointF;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingStringFormat = System.Drawing.StringFormat;
using GdtCreator.Core.Rendering;

namespace GdtCreator.Avalonia.Rendering;

public static class SymbolRenderer
{
    private static readonly Typeface TextTypeface = new("Bahnschrift");
    private static readonly Typeface SymbolTypeface = new("Segoe UI Symbol");
    private static readonly Color DefaultContentColor = Color.FromRgb(16, 42, 67);
    private static readonly DrawingColor DefaultDrawingContentColor = DrawingColor.FromArgb(16, 42, 67);

    public static void DrawSymbolIcon(DrawingContext context, RenderSymbol symbol, Rect bounds, double strokeThickness)
    {
        var brush = new SolidColorBrush(DefaultContentColor);
        var pen = CreatePen(strokeThickness, DefaultContentColor);
        DrawSymbol(context, symbol, bounds, pen, brush);
    }
    public static void DrawModel(DrawingContext context, ToleranceRenderModel model, Rect bounds, double pixelsPerDip)
    {
        var scale = Math.Min(bounds.Width / model.Width, bounds.Height / model.Height);
        scale = double.IsFinite(scale) && scale > 0d ? scale : 1d;

        var originX = bounds.X + ((bounds.Width - (model.Width * scale)) / 2d);
        var originY = bounds.Y + ((bounds.Height - (model.Height * scale)) / 2d);
        var frameY = model.TopTextHeight > 0d ? model.TopTextHeight + model.TextGap : 0d;
        var contentColor = ParseColor(model.ContentColorHex);
        var brush = new SolidColorBrush(contentColor);
        var pen = CreatePen(Math.Max(1d, model.StrokeThickness * scale), contentColor);

        if (!string.IsNullOrWhiteSpace(model.TopText))
        {
            var topText = CreateFormattedText(model.TopText, brush, 16d * scale);
            context.DrawText(topText, new Point(originX, originY + (((model.TopTextHeight * scale) - topText.Height) / 2d)));
        }

        var frameRect = new Rect(originX, originY + (frameY * scale), model.FrameWidth * scale, model.FrameHeight * scale);
        context.DrawRectangle(Brushes.White, pen, frameRect);

        var cursor = frameRect.X;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            var cellWidth = cell.Width * scale;
            if (index > 0)
            {
                context.DrawLine(pen, new Point(cursor, frameRect.Y), new Point(cursor, frameRect.Bottom));
            }

            DrawCell(context, cell, cursor, frameRect.Y, cellWidth, frameRect.Height, scale, contentColor);
            cursor += cellWidth;
        }

        if (!string.IsNullOrWhiteSpace(model.BottomText))
        {
            var bottomText = CreateFormattedText(model.BottomText, brush, 16d * scale);
            var bottomY = originY + ((frameY + model.FrameHeight + model.TextGap) * scale) + (((model.BottomTextHeight * scale) - bottomText.Height) / 2d);
            context.DrawText(bottomText, new Point(originX, bottomY));
        }
    }

    public static RenderTargetBitmap CreateBitmap(ToleranceRenderModel model, double scale)
    {
        var pixelWidth = Math.Max(1, (int)Math.Ceiling(model.Width * scale));
        var pixelHeight = Math.Max(1, (int)Math.Ceiling(model.Height * scale));
        var bitmap = new RenderTargetBitmap(new PixelSize(pixelWidth, pixelHeight), new Vector(96d, 96d));

        using (var drawingContext = bitmap.CreateDrawingContext())
        {
            drawingContext.DrawRectangle(Brushes.White, null, new Rect(0, 0, pixelWidth, pixelHeight));
            DrawModel(drawingContext, model, new Rect(0, 0, pixelWidth, pixelHeight), 1d);
        }

        return bitmap;
    }

    public static string BuildSvg(ToleranceRenderModel model, double scale)
    {
        var width = model.Width * scale;
        var height = model.Height * scale;
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

        builder.AppendLine($"  <rect x=\"0\" y=\"{Format(frameY)}\" width=\"{Format(frameWidth)}\" height=\"{Format(frameHeight)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");

        double cursor = 0d;
        foreach (var (cell, index) in model.Cells.Select((cell, index) => (cell, index)))
        {
            var scaledCellWidth = cell.Width * scale;
            if (index > 0)
            {
                builder.AppendLine($"  <line x1=\"{Format(cursor)}\" y1=\"{Format(frameY)}\" x2=\"{Format(cursor)}\" y2=\"{Format(frameY + frameHeight)}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
            }

            var tokenWidth = RenderLayout.GetTokenSequenceWidth(cell.Tokens) * scale;
            var tokenCursor = cursor + ((scaledCellWidth - tokenWidth) / 2d);
            foreach (var token in cell.Tokens)
            {
                var advance = RenderLayout.GetTokenAdvance(token) * scale;
                var tokenRect = CreateSymbolBounds(tokenCursor, advance, frameHeight);
                tokenRect = new Rect(tokenRect.X, tokenRect.Y + frameY, tokenRect.Width, tokenRect.Height);
                if (token.IsSymbol)
                {
                    AppendSvgSymbol(builder, token.Symbol!.Value, tokenRect, color, strokeWidth);
                }
                else
                {
                    var text = SecurityElement.Escape(token.Text) ?? string.Empty;
                    var fontSize = RenderLayout.TextFontSize * scale;
                    builder.AppendLine($"  <text x=\"{Format(tokenCursor + (advance / 2d))}\" y=\"{Format(frameY + (frameHeight / 2d) + (fontSize / 3.2d))}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
                }

                tokenCursor += advance + (RenderLayout.TokenGap * scale);
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
            using var contextFont = new DrawingFont("Bahnschrift", (float)(16d * scale), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
            using var contentFont = new DrawingFont("Bahnschrift", (float)(RenderLayout.TextFontSize * scale), System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);

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
                graphics.DrawString(model.TopText, contextFont, textBrush, new DrawingRectangleF(0f, 0f, width, (float)(model.TopTextHeight * scale)), topFormat);
            }

            graphics.DrawRectangle(pen, 0f, frameY, frameWidth - pen.Width, frameHeight - pen.Width);

            float cursor = 0f;
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
                    tokenRect = new DrawingRectangleF(tokenRect.X, tokenRect.Y + frameY, tokenRect.Width, tokenRect.Height);
                    if (token.IsSymbol)
                    {
                        DrawGdiSymbol(graphics, token.Symbol!.Value, tokenRect, pen, textBrush, contentFont);
                    }
                    else
                    {
                        using var format = new DrawingStringFormat
                        {
                            Alignment = System.Drawing.StringAlignment.Center,
                            LineAlignment = System.Drawing.StringAlignment.Center
                        };
                        graphics.DrawString(token.Text, contentFont, textBrush, new DrawingRectangleF(tokenCursor, frameY, advance, frameHeight), format);
                    }

                    tokenCursor += advance + (float)(RenderLayout.TokenGap * scale);
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
                graphics.DrawString(model.BottomText, contextFont, textBrush, new DrawingRectangleF(0f, bottomY, width, (float)(model.BottomTextHeight * scale)), bottomFormat);
            }
        }
        finally
        {
            referenceGraphics.ReleaseHdc(hdc);
        }

        return stream.ToArray();
    }

    private static void DrawCell(DrawingContext context, ToleranceCell cell, double originX, double originY, double width, double height, double scale, Color contentColor)
    {
        var tokenWidth = RenderLayout.GetTokenSequenceWidth(cell.Tokens) * scale;
        var cursor = originX + ((width - tokenWidth) / 2d);
        var brush = new SolidColorBrush(contentColor);
        var pen = CreatePen(Math.Max(1d, RenderMetrics.StrokeThickness * scale), contentColor);

        foreach (var token in cell.Tokens)
        {
            var advance = RenderLayout.GetTokenAdvance(token) * scale;
            var tokenRect = CreateSymbolBounds(cursor, advance, height);
            tokenRect = new Rect(tokenRect.X, tokenRect.Y + originY, tokenRect.Width, tokenRect.Height);
            if (token.IsSymbol)
            {
                DrawSymbol(context, token.Symbol!.Value, tokenRect, pen, brush);
            }
            else
            {
                var formatted = CreateFormattedText(token.Text ?? string.Empty, brush, RenderLayout.TextFontSize * scale);
                context.DrawText(formatted, new Point(cursor + ((advance - formatted.Width) / 2d), originY + ((height - formatted.Height) / 2d)));
            }

            cursor += advance + (RenderLayout.TokenGap * scale);
        }
    }

    private static Pen CreatePen(double strokeThickness, Color color)
    {
        return new Pen(new SolidColorBrush(color), strokeThickness)
        {
            LineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
    }

    private static FormattedText CreateFormattedText(string text, IBrush brush, double fontSize)
    {
        return new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, TextTypeface, fontSize, brush);
    }

    private static Color ParseColor(string? colorHex)
    {
        try
        {
            return Color.Parse(colorHex ?? "#102A43");
        }
        catch
        {
            return DefaultContentColor;
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

    private static void DrawSymbol(DrawingContext context, RenderSymbol symbol, Rect bounds, Pen pen, IBrush brush)
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
                DrawAvaloniaCylindricity(context, pen, bounds);
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
                DrawAvaloniaRunout(context, pen, bounds, false);
                break;
            case RenderSymbol.TotalRunout:
                DrawAvaloniaRunout(context, pen, bounds, true);
                break;
            case RenderSymbol.Diameter:
                context.DrawEllipse(null, pen, Center(bounds), bounds.Width * 0.28, bounds.Height * 0.28);
                context.DrawLine(pen, PointAt(bounds, 0.24, 0.76), PointAt(bounds, 0.76, 0.24));
                break;
            case RenderSymbol.MaximumMaterialCondition:
                DrawCircledLetter(context, bounds, pen, brush, "M");
                break;
            case RenderSymbol.LeastMaterialCondition:
                DrawCircledLetter(context, bounds, pen, brush, "L");
                break;
            case RenderSymbol.ProjectedToleranceZone:
                DrawCircledLetter(context, bounds, pen, brush, "P");
                break;
            case RenderSymbol.FreeState:
                DrawCircledLetter(context, bounds, pen, brush, "F");
                break;
            case RenderSymbol.SphericalDiameter:
                DrawPrefixedSymbol(context, bounds, pen, brush, "S", RenderSymbol.Diameter);
                break;
            case RenderSymbol.SphericalRadius:
                DrawStandaloneTextSymbol(context, bounds, brush, "SR", 0.56d);
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
                builder.AppendLine($"  <path d=\"M {SvgPoint(bounds, 0.18, 0.32)} L {SvgPoint(bounds, 0.78, 0.32)} L {SvgPoint(bounds, 0.62, 0.72)} L {SvgPoint(bounds, 0.02, 0.72)} Z\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linejoin=\"round\" />");
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
                AppendSvgStandaloneTextSymbol(builder, bounds, color, "SR", 0.56d);
                break;
        }
    }

    private static void DrawAvaloniaRunout(DrawingContext context, Pen pen, Rect bounds, bool total)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(PointAt(bounds, 0.15, 0.75), false);
            ctx.ArcTo(PointAt(bounds, 0.78, 0.32), new Size(bounds.Width * 0.55, bounds.Height * 0.55), 0d, false, SweepDirection.CounterClockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
        DrawAvaloniaArrow(context, pen, PointAt(bounds, 0.78, 0.32), PointAt(bounds, 0.60, 0.22));
        if (total)
        {
            DrawAvaloniaArrow(context, pen, PointAt(bounds, 0.15, 0.75), PointAt(bounds, 0.30, 0.82));
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

    private static void DrawCircledLetter(DrawingContext context, Rect bounds, Pen pen, IBrush brush, string letter)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38;
        context.DrawEllipse(null, pen, Center(bounds), radius, radius);
        var formatted = CreateFormattedText(letter, brush, Math.Min(bounds.Width, bounds.Height) * 0.42);
        context.DrawText(formatted, new Point(bounds.X + ((bounds.Width - formatted.Width) / 2d), bounds.Y + ((bounds.Height - formatted.Height) / 2d)));
    }

    private static void AppendSvgCircledLetter(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, string letter)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38;
        AppendSvgEllipse(builder, Center(bounds), radius, radius, color, strokeWidth);
        var fontSize = Math.Min(bounds.Width, bounds.Height) * 0.42;
        builder.AppendLine($"  <text x=\"{Format(bounds.X + (bounds.Width / 2d))}\" y=\"{Format(bounds.Y + (bounds.Height / 2d) + (fontSize * 0.36))}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"700\" fill=\"{color}\">{letter}</text>");
    }

    private static void DrawAvaloniaCylindricity(DrawingContext context, Pen pen, Rect bounds)
    {
        var cx = bounds.X + bounds.Width * 0.5;
        var rx = bounds.Width * 0.25;
        var ry = bounds.Height * 0.14;
        var topY = bounds.Y + bounds.Height * 0.24;
        var botY = bounds.Y + bounds.Height * 0.76;
        context.DrawLine(pen, new Point(cx - rx, topY), new Point(cx - rx, botY));
        context.DrawLine(pen, new Point(cx + rx, topY), new Point(cx + rx, botY));
        context.DrawEllipse(null, pen, new Point(cx, topY), rx, ry);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(cx - rx, botY), false);
            ctx.ArcTo(new Point(cx + rx, botY), new Size(rx, ry), 0d, true, SweepDirection.Clockwise);
            ctx.EndFigure(false);
        }
        context.DrawGeometry(null, pen, geometry);
    }

    private static void AppendSvgCylindricity(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        var cx = bounds.X + bounds.Width * 0.5;
        var rx = bounds.Width * 0.25;
        var ry = bounds.Height * 0.14;
        var topY = bounds.Y + bounds.Height * 0.24;
        var botY = bounds.Y + bounds.Height * 0.76;
        AppendSvgLine(builder, new Point(cx - rx, topY), new Point(cx - rx, botY), color, strokeWidth);
        AppendSvgLine(builder, new Point(cx + rx, topY), new Point(cx + rx, botY), color, strokeWidth);
        AppendSvgEllipse(builder, new Point(cx, topY), rx, ry, color, strokeWidth);
        builder.AppendLine($"  <path d=\"M {Format(cx - rx)} {Format(botY)} A {Format(rx)} {Format(ry)} 0 1 0 {Format(cx + rx)} {Format(botY)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
    }

    private static StreamGeometry CreateSurfaceProfileGeometry(Rect bounds)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, 0.18, 0.65), false);
        ctx.CubicBezierTo(PointAt(bounds, 0.32, 0.2), PointAt(bounds, 0.68, 0.2), PointAt(bounds, 0.82, 0.65));
        ctx.LineTo(PointAt(bounds, 0.18, 0.65));
        ctx.EndFigure(true);
        return geometry;
    }

    private static void AppendSvgSurfaceProfile(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth)
    {
        builder.AppendLine($"  <path d=\"M {SvgPoint(bounds, 0.18, 0.65)} C {SvgPoint(bounds, 0.32, 0.2)} {SvgPoint(bounds, 0.68, 0.2)} {SvgPoint(bounds, 0.82, 0.65)} Z\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" stroke-linejoin=\"round\" />");
    }

    private static void DrawPrefixedSymbol(DrawingContext context, Rect bounds, Pen pen, IBrush brush, string prefix, RenderSymbol symbol)
    {
        var formatted = CreateFormattedText(prefix, brush, Math.Min(bounds.Width, bounds.Height) * 0.48);
        var textWidth = formatted.Width;
        var symbolSize = bounds.Height * 0.7;
        var totalWidth = textWidth + symbolSize;
        var startX = bounds.X + ((bounds.Width - totalWidth) / 2d);
        context.DrawText(formatted, new Point(startX, bounds.Y + ((bounds.Height - formatted.Height) / 2d)));
        var symbolBounds = new Rect(startX + textWidth, bounds.Y + ((bounds.Height - symbolSize) / 2d), symbolSize, symbolSize);
        DrawSymbol(context, symbol, symbolBounds, pen, brush);
    }

    private static void DrawStandaloneTextSymbol(DrawingContext context, Rect bounds, IBrush brush, string text, double scale)
    {
        var formatted = CreateFormattedText(text, brush, Math.Min(bounds.Width, bounds.Height) * scale);
        context.DrawText(formatted, new Point(bounds.X + ((bounds.Width - formatted.Width) / 2d), bounds.Y + ((bounds.Height - formatted.Height) / 2d)));
    }

    private static void AppendSvgPrefixedSymbol(System.Text.StringBuilder builder, Rect bounds, string color, double strokeWidth, string prefix, RenderSymbol symbol)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * 0.48;
        var textWidth = prefix.Length * fontSize * 0.55;
        var symbolSize = bounds.Height * 0.7;
        var totalWidth = textWidth + symbolSize;
        var startX = bounds.X + (bounds.Width - totalWidth) / 2d;
        builder.AppendLine($"  <text x=\"{Format(startX + (textWidth / 2d))}\" y=\"{Format(bounds.Y + bounds.Height * 0.65)}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{prefix}</text>");
        AppendSvgSymbol(builder, symbol, new Rect(startX + textWidth, bounds.Y + ((bounds.Height - symbolSize) / 2d), symbolSize, symbolSize), color, strokeWidth);
    }

    private static void AppendSvgStandaloneTextSymbol(System.Text.StringBuilder builder, Rect bounds, string color, string text, double scale)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * scale;
        var baselineY = bounds.Y + (bounds.Height / 2d) + (fontSize * 0.32d);
        builder.AppendLine($"  <text x=\"{Format(bounds.X + (bounds.Width / 2d))}\" y=\"{Format(baselineY)}\" text-anchor=\"middle\" font-family=\"Bahnschrift, Segoe UI, sans-serif\" font-size=\"{Format(fontSize)}\" font-weight=\"600\" fill=\"{color}\">{text}</text>");
    }

    private static StreamGeometry CreatePolygonGeometry(Rect bounds, params double[] coordinates)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, coordinates[0], coordinates[1]), false);
        for (var index = 2; index < coordinates.Length; index += 2)
        {
            ctx.LineTo(PointAt(bounds, coordinates[index], coordinates[index + 1]));
        }
        ctx.EndFigure(true);
        return geometry;
    }

    private static StreamGeometry CreateBezierGeometry(Rect bounds, double x1, double y1, double cx1, double cy1, double cx2, double cy2, double x2, double y2)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(PointAt(bounds, x1, y1), false);
        ctx.CubicBezierTo(PointAt(bounds, cx1, cy1), PointAt(bounds, cx2, cy2), PointAt(bounds, x2, y2));
        ctx.EndFigure(false);
        return geometry;
    }

    private static void DrawAvaloniaArrow(DrawingContext context, Pen pen, Point tip, Point anchor)
    {
        var dx = anchor.X - tip.X;
        var dy = anchor.Y - tip.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001d)
        {
            return;
        }

        dx /= length;
        dy /= length;
        var nx = -dy;
        var ny = dx;
        var headLength = Math.Max(5d, Math.Min(12d, length * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = new Point(tip.X + (dx * headLength) + (nx * headWidth), tip.Y + (dy * headLength) + (ny * headWidth));
        var right = new Point(tip.X + (dx * headLength) - (nx * headWidth), tip.Y + (dy * headLength) - (ny * headWidth));
        context.DrawLine(pen, tip, left);
        context.DrawLine(pen, tip, right);
    }

    private static void AppendSvgArrow(System.Text.StringBuilder builder, Point tip, Point anchor, string color, double strokeWidth)
    {
        var dx = anchor.X - tip.X;
        var dy = anchor.Y - tip.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001d)
        {
            return;
        }

        dx /= length;
        dy /= length;
        var nx = -dy;
        var ny = dx;
        var headLength = Math.Max(5d, Math.Min(12d, length * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = new Point(tip.X + (dx * headLength) + (nx * headWidth), tip.Y + (dy * headLength) + (ny * headWidth));
        var right = new Point(tip.X + (dx * headLength) - (nx * headWidth), tip.Y + (dy * headLength) - (ny * headWidth));
        AppendSvgLine(builder, tip, left, color, strokeWidth);
        AppendSvgLine(builder, tip, right, color, strokeWidth);
    }

    private static void DrawGlyphSymbol(DrawingContext context, Rect bounds, IBrush brush, string glyph, Typeface typeface, double scale)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * scale;
        var formatted = new FormattedText(glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, typeface, fontSize, brush);
        context.DrawText(formatted, new Point(bounds.X + ((bounds.Width - formatted.Width) / 2d), bounds.Y + ((bounds.Height - formatted.Height) / 2d)));
    }

    private static void AppendSvgGlyph(System.Text.StringBuilder builder, Rect bounds, string color, string glyph, double scale)
    {
        var fontSize = Math.Min(bounds.Width, bounds.Height) * scale;
        var centerX = bounds.X + (bounds.Width / 2d);
        var baselineY = bounds.Y + (bounds.Height / 2d) + (fontSize * 0.34d);
        builder.AppendLine($"  <text x=\"{Format(centerX)}\" y=\"{Format(baselineY)}\" text-anchor=\"middle\" font-family=\"Segoe UI Symbol, Noto Sans Symbols, sans-serif\" font-size=\"{Format(fontSize)}\" fill=\"{color}\">{glyph}</text>");
    }

    private static void DrawGdiGlyph(DrawingGraphics graphics, DrawingRectangleF bounds, DrawingBrush brush, string glyph)
    {
        using var font = new DrawingFont("Segoe UI Symbol", Math.Min(bounds.Width, bounds.Height) * 0.9f, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Pixel);
        using var format = new DrawingStringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        graphics.DrawString(glyph, font, brush, bounds, format);
    }

    private static void AppendSvgLine(System.Text.StringBuilder builder, Point start, Point end, string color, double strokeWidth)
    {
        builder.AppendLine($"  <line x1=\"{Format(start.X)}\" y1=\"{Format(start.Y)}\" x2=\"{Format(end.X)}\" y2=\"{Format(end.Y)}\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" stroke-linecap=\"round\" />");
    }

    private static void AppendSvgEllipse(System.Text.StringBuilder builder, Point center, double rx, double ry, string color, double strokeWidth)
    {
        builder.AppendLine($"  <ellipse cx=\"{Format(center.X)}\" cy=\"{Format(center.Y)}\" rx=\"{Format(rx)}\" ry=\"{Format(ry)}\" fill=\"none\" stroke=\"{color}\" stroke-width=\"{Format(strokeWidth)}\" />");
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

    private static void DrawGdiArrow(DrawingGraphics graphics, DrawingPen pen, DrawingPointF tip, DrawingPointF anchor)
    {
        var dx = anchor.X - tip.X;
        var dy = anchor.Y - tip.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length < 0.001d)
        {
            return;
        }

        dx /= (float)length;
        dy /= (float)length;
        var nx = -dy;
        var ny = dx;
        var headLength = Math.Max(5d, Math.Min(12d, length * 0.8d));
        var headWidth = headLength * 0.55d;
        var left = new DrawingPointF((float)(tip.X + (dx * headLength) + (nx * headWidth)), (float)(tip.Y + (dy * headLength) + (ny * headWidth)));
        var right = new DrawingPointF((float)(tip.X + (dx * headLength) - (nx * headWidth)), (float)(tip.Y + (dy * headLength) - (ny * headWidth)));
        graphics.DrawLine(pen, tip, left);
        graphics.DrawLine(pen, tip, right);
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

    private static void DrawGdiCircledLetter(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingFont font, DrawingRectangleF bounds, string letter)
    {
        var radius = Math.Min(bounds.Width, bounds.Height) * 0.38f;
        graphics.DrawEllipse(pen, bounds.Left + (bounds.Width / 2f) - radius, bounds.Top + (bounds.Height / 2f) - radius, radius * 2f, radius * 2f);
        using var format = new DrawingStringFormat { Alignment = System.Drawing.StringAlignment.Center, LineAlignment = System.Drawing.StringAlignment.Center };
        graphics.DrawString(letter, font, brush, bounds, format);
    }

    private static void DrawGdiPrefixedSymbol(DrawingGraphics graphics, DrawingPen pen, DrawingBrush brush, DrawingFont font, DrawingRectangleF bounds, string prefix, RenderSymbol symbol)
    {
        var textSize = graphics.MeasureString(prefix, font);
        var symbolSize = bounds.Height * 0.7f;
        var totalWidth = textSize.Width + symbolSize;
        var startX = bounds.Left + ((bounds.Width - totalWidth) / 2f);
        using var format = new DrawingStringFormat { LineAlignment = System.Drawing.StringAlignment.Center };
        graphics.DrawString(prefix, font, brush, new DrawingRectangleF(startX, bounds.Top, textSize.Width, bounds.Height), format);
        DrawGdiSymbol(graphics, symbol, new DrawingRectangleF(startX + textSize.Width, bounds.Top + ((bounds.Height - symbolSize) / 2f), symbolSize, symbolSize), pen, brush, font);
    }

    private static void DrawGdiStandaloneTextSymbol(DrawingGraphics graphics, DrawingRectangleF bounds, DrawingBrush brush, string text, float scale)
    {
        using var font = new DrawingFont("Bahnschrift", Math.Min(bounds.Width, bounds.Height) * scale, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Pixel);
        using var format = new DrawingStringFormat
        {
            Alignment = System.Drawing.StringAlignment.Center,
            LineAlignment = System.Drawing.StringAlignment.Center
        };
        graphics.DrawString(text, font, brush, bounds, format);
    }

    private static void DrawGdiSymbol(DrawingGraphics graphics, RenderSymbol symbol, DrawingRectangleF bounds, DrawingPen pen, DrawingBrush brush, DrawingFont font)
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
                DrawGdiCircledLetter(graphics, pen, brush, font, bounds, "M");
                break;
            case RenderSymbol.LeastMaterialCondition:
                DrawGdiCircledLetter(graphics, pen, brush, font, bounds, "L");
                break;
            case RenderSymbol.ProjectedToleranceZone:
                DrawGdiCircledLetter(graphics, pen, brush, font, bounds, "P");
                break;
            case RenderSymbol.FreeState:
                DrawGdiCircledLetter(graphics, pen, brush, font, bounds, "F");
                break;
            case RenderSymbol.SphericalDiameter:
                DrawGdiPrefixedSymbol(graphics, pen, brush, font, bounds, "S", RenderSymbol.Diameter);
                break;
            case RenderSymbol.SphericalRadius:
                DrawGdiStandaloneTextSymbol(graphics, bounds, brush, "SR", 0.56f);
                break;
        }
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

    private static Point Center(Rect bounds) => new(bounds.X + (bounds.Width / 2d), bounds.Y + (bounds.Height / 2d));
    private static Point PointAt(Rect bounds, double x, double y) => new(bounds.X + (bounds.Width * x), bounds.Y + (bounds.Height * y));
    private static DrawingPointF PointAt(DrawingRectangleF bounds, float x, float y) => new(bounds.Left + (bounds.Width * x), bounds.Top + (bounds.Height * y));
    private static string SvgPoint(Rect bounds, double x, double y) => $"{Format(PointAt(bounds, x, y).X)} {Format(PointAt(bounds, x, y).Y)}";
    private static string Format(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}


