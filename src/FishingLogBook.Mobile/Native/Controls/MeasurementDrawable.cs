using FishingLogBook.Mobile.Features.Measurements.Models;
using Microsoft.Maui.Graphics;

namespace FishingLogBook.Mobile.Native.Controls;

public sealed class MeasurementDrawable : IDrawable
{
    public const float DialWidth = 240f;
    public const float DialHeight = 150f;
    public const float TapeWidth = 240f;
    public const float TapeHeight = 92f;
    private const float DialCentreX = 120f;
    private const float DialCentreY = 70f;
    private const float TapeStripStart = 50f;
    private const float TapeStripBase = 12f;
    private const float TapeStripTravel = 166f;

    public MeasurementKindEnum Kind { get; set; }
    public float Fraction { get; set; }
    public Color Accent { get; set; } = Colors.Indigo;
    public Color Surface { get; set; } = Colors.White;
    public float Scale { get; private set; } = 1f;
    public float OffsetX { get; private set; }
    public float OffsetY { get; private set; }

    public PointF DialCentre => new(OffsetX + (DialCentreX * Scale), OffsetY + (DialCentreY * Scale));

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var width = Kind == MeasurementKindEnum.Weight ? DialWidth : TapeWidth;
        var height = Kind == MeasurementKindEnum.Weight ? DialHeight : TapeHeight;
        Scale = Math.Min(dirtyRect.Width / width, dirtyRect.Height / height);
        OffsetX = (dirtyRect.Width - (width * Scale)) / 2f;
        OffsetY = (dirtyRect.Height - (height * Scale)) / 2f;
        canvas.SaveState();
        canvas.Translate(OffsetX, OffsetY);
        canvas.Scale(Scale, Scale);
        if (Kind == MeasurementKindEnum.Weight)
        {
            DrawDial(canvas);
        }
        else
        {
            DrawTape(canvas);
        }

        canvas.RestoreState();
    }

    private void DrawDial(ICanvas canvas)
    {
        var handle = new PathF();
        handle.MoveTo(98f, 18f);
        handle.CurveTo(98f, 1f, 142f, 1f, 142f, 18f);
        handle.LineTo(135f, 18f);
        handle.CurveTo(135f, 9f, 105f, 9f, 105f, 18f);
        handle.Close();
        canvas.FillColor = Accent;
        canvas.FillPath(handle);
        canvas.FillRoundedRectangle(112f, 14f, 16f, 10f, 4f);
        canvas.FillColor = Accent.WithAlpha(0.92f);
        canvas.FillCircle(DialCentreX, DialCentreY, 53f);
        canvas.FillColor = Surface;
        canvas.FillCircle(DialCentreX, DialCentreY, 46f);
        canvas.StrokeColor = Accent.WithAlpha(0.28f);
        canvas.StrokeSize = 2f;
        canvas.DrawCircle(DialCentreX, DialCentreY, 46f);
        canvas.StrokeColor = Accent;
        canvas.StrokeLineCap = LineCap.Round;
        for (var index = 0; index < 24; index++)
        {
            var angle = index * 15;
            canvas.SaveState();
            canvas.Rotate(angle, DialCentreX, DialCentreY);
            canvas.StrokeSize = 3f;
            canvas.DrawLine(DialCentreX, 24f, DialCentreX, angle % 45 == 0 ? 36f : 31f);
            canvas.RestoreState();
        }

        canvas.SaveState();
        canvas.Rotate(Fraction * 359f, DialCentreX, DialCentreY);
        canvas.StrokeSize = 3.5f;
        canvas.StrokeColor = Accent;
        canvas.DrawLine(DialCentreX, DialCentreY, DialCentreX, 37f);
        canvas.RestoreState();
        canvas.FillColor = Accent;
        canvas.FillCircle(DialCentreX, DialCentreY, 6f);
        canvas.FillRoundedRectangle(114f, 121f, 12f, 9f, 3f);
        var hook = new PathF();
        hook.MoveTo(120f, 128f);
        hook.LineTo(120f, 133f);
        hook.CurveTo(120f, 142f, 135f, 142f, 135f, 133f);
        hook.LineTo(135f, 131f);
        canvas.StrokeSize = 5f;
        canvas.StrokeColor = Accent;
        canvas.DrawPath(hook);
    }

    private void DrawTape(ICanvas canvas)
    {
        var stripWidth = TapeStripBase + (Math.Clamp(Fraction, 0f, 1f) * TapeStripTravel);
        canvas.FillColor = Accent.WithAlpha(0.92f);
        canvas.FillRoundedRectangle(10f, 16f, 48f, 60f, 12f);
        FishDrawing.Draw(canvas, new RectF(17f, 31f, 34f, 30f), Surface, Accent, false);
        canvas.FillColor = Blend(Surface, Accent, 0.14f);
        canvas.FillRoundedRectangle(TapeStripStart, 27f, stripWidth, 38f, 3f);
        canvas.StrokeColor = Accent;
        canvas.StrokeSize = 1.5f;
        canvas.DrawRoundedRectangle(TapeStripStart, 27f, stripWidth, 38f, 3f);
        canvas.StrokeColor = Blend(Accent, Colors.Black, 0.3f);
        for (var index = 0; index < 18; index++)
        {
            var x = 56f + (index * 10f);
            canvas.DrawLine(x, 27f, x, index % 5 == 0 ? 48f : 40f);
        }

        var end = TapeStripStart + stripWidth;
        var hook = new PathF();
        hook.MoveTo(end, 24f);
        hook.LineTo(end, 69f);
        hook.LineTo(end + 8f, 69f);
        hook.LineTo(end + 8f, 61f);
        hook.LineTo(end + 5f, 61f);
        hook.LineTo(end + 5f, 32f);
        hook.LineTo(end + 8f, 32f);
        hook.LineTo(end + 8f, 24f);
        hook.Close();
        canvas.FillColor = Accent;
        canvas.FillPath(hook);
    }

    private static Color Blend(Color background, Color foreground, float amount)
    {
        return new Color(
            background.Red + ((foreground.Red - background.Red) * amount),
            background.Green + ((foreground.Green - background.Green) * amount),
            background.Blue + ((foreground.Blue - background.Blue) * amount),
            1f);
    }
}
