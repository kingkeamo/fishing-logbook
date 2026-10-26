using FishingLogBook.Mobile.Presentation;
using Microsoft.Maui.Graphics;

namespace FishingLogBook.Mobile.Native.Controls;

public sealed class LoaderDrawable : IDrawable
{
    private static readonly LoaderPalette Light = new(
        Navy: Color.FromArgb("#0B1F3A"),
        Teal: Color.FromArgb("#0F766E"),
        Rod: Color.FromArgb("#5EEAD4"),
        Green: Color.FromArgb("#16A34A"),
        Water: Color.FromArgb("#38BDF8"),
        Hull: Color.FromArgb("#0B1F3A"),
        Head: Color.FromArgb("#334155"),
        Lower: Color.FromArgb("#0B1F3A"),
        Line: Color.FromArgb("#0B1F3A"));

    private static readonly LoaderPalette Dark = new(
        Navy: Color.FromArgb("#0B1F3A"),
        Teal: Color.FromArgb("#0F766E"),
        Rod: Color.FromArgb("#99F6E4"),
        Green: Color.FromArgb("#16A34A"),
        Water: Color.FromArgb("#38BDF8"),
        Hull: Color.FromArgb("#E0F2FE"),
        Head: Color.FromArgb("#E0F2FE"),
        Lower: Color.FromArgb("#38BDF8"),
        Line: Color.FromArgb("#E0F2FE"));

    private static readonly Color Worm = Color.FromArgb("#E11D48");
    private static readonly Color Pencil = Color.FromArgb("#F8FAFC");

    public LoaderKindEnum Kind { get; set; }
    public double Phase { get; set; }
    public bool IsDark { get; set; }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var (originX, originY, size) = Kind switch
        {
            LoaderKindEnum.Boat => (-4f, -20f, 136f),
            LoaderKindEnum.Bobber => (-4f, -12f, 112f),
            _ => (0f, 0f, 96f)
        };
        var scale = Math.Min(dirtyRect.Width, dirtyRect.Height) / size;
        var palette = IsDark ? Dark : Light;
        canvas.SaveState();
        canvas.Translate((dirtyRect.Width - (size * scale)) / 2f, (dirtyRect.Height - (size * scale)) / 2f);
        canvas.Scale(scale, scale);
        canvas.Translate(-originX, -originY);
        canvas.StrokeLineCap = LineCap.Round;
        canvas.StrokeLineJoin = LineJoin.Round;
        switch (Kind)
        {
            case LoaderKindEnum.LeapingFish:
                DrawLeapingFish(canvas, palette);
                break;
            case LoaderKindEnum.Boat:
                DrawBoat(canvas, palette);
                break;
            default:
                DrawBobber(canvas, palette);
                break;
        }

        canvas.RestoreState();
    }

    private static PathF Wave(float x, float y, float width, float firstControl, float secondControl, float amplitude, int segments)
    {
        var path = new PathF();
        path.MoveTo(x, y);
        for (var index = 0; index < segments; index++)
        {
            var start = x + (index * width);
            path.CurveTo(start + firstControl, y - amplitude, start + secondControl, y + amplitude, start + width, y);
        }

        return path;
    }

    private static void DrawFish(ICanvas canvas, Color body, Color eye, float length, float height, float tail, float eyeX, float eyeY, float eyeRadius)
    {
        canvas.FillColor = body;
        canvas.FillEllipse(-length, -height, length * 2f, height * 2f);
        var fin = new PathF();
        fin.MoveTo(-(length - 2f), 0f);
        fin.LineTo(-length - tail, -(height - 2f));
        fin.LineTo(-length - tail, height - 2f);
        fin.Close();
        canvas.FillPath(fin);
        canvas.FillColor = eye;
        canvas.FillCircle(eyeX, eyeY, eyeRadius);
    }

    private void DrawLeapingFish(ICanvas canvas, LoaderPalette palette)
    {
        canvas.StrokeColor = palette.Water;
        canvas.StrokeSize = 3f;
        canvas.SaveState();
        canvas.Translate(0f, (float)LoaderMotion.WaveOffset(Phase));
        canvas.DrawPath(Wave(8f, 70f, 24f, 8f, 16f, 6f, 4));
        canvas.RestoreState();
        canvas.SaveState();
        canvas.Alpha = 0.6f;
        canvas.Translate(0f, (float)LoaderMotion.WaveOffset(Phase, 0.5));
        canvas.DrawPath(Wave(8f, 78f, 24f, 8f, 16f, 5f, 4));
        canvas.RestoreState();

        DrawSplash(canvas, palette, 20f, LoaderMotion.LeapSplashLeave(Phase));
        DrawSplash(canvas, palette, 76f, LoaderMotion.LeapSplashLand(Phase));

        var jumper = LoaderMotion.LeapingFish(Phase);
        canvas.SaveState();
        canvas.Alpha = (float)jumper.Opacity;
        canvas.Translate((float)jumper.X, (float)jumper.Y);
        canvas.Rotate((float)jumper.Rotation);
        DrawFish(canvas, palette.Green, palette.Navy, 14f, 7f, 8f, 7f, -2f, 1.4f);
        canvas.RestoreState();
    }

    private static void DrawSplash(ICanvas canvas, LoaderPalette palette, float centreX, LoaderPose pose)
    {
        if (pose.Opacity <= 0.001d)
        {
            return;
        }

        canvas.SaveState();
        canvas.Alpha = (float)pose.Opacity;
        canvas.StrokeColor = palette.Water;
        canvas.StrokeSize = 2f;
        var scale = (float)pose.Scale;
        canvas.DrawEllipse(centreX - (8f * scale), 70f - (3f * scale), 16f * scale, 6f * scale);
        canvas.RestoreState();
    }

    private void DrawBoat(ICanvas canvas, LoaderPalette palette)
    {
        var wake = new PathF();
        wake.MoveTo(4f, 80f);
        for (var index = 0; index < 3; index++)
        {
            var start = 4f + (index * 28f);
            wake.CurveTo(start + 10f, 75f, start + 18f, 85f, start + 28f, 80f);
        }

        canvas.SaveState();
        canvas.StrokeColor = palette.Water;
        canvas.StrokeSize = 3f;
        canvas.StrokeDashPattern = [12f, 8f];
        canvas.StrokeDashOffset = (float)LoaderMotion.WakeDashOffset(Phase);
        canvas.DrawPath(wake);
        canvas.RestoreState();

        var drift = LoaderMotion.BoatDrift(Phase);
        canvas.SaveState();
        canvas.Translate(22f, 72f);
        canvas.Rotate((float)LoaderMotion.BoatPitch(Phase));
        canvas.Translate((float)drift.X, (float)drift.Y);
        canvas.Translate(-22f, -72f);

        var hull = new PathF();
        hull.MoveTo(30f, 54f);
        hull.LineTo(30f, 68f);
        hull.CurveTo(44f, 76f, 66f, 74f, 82f, 58f);
        hull.LineTo(88f, 42f);
        hull.CurveTo(76f, 38f, 58f, 48f, 34f, 52f);
        hull.Close();
        canvas.FillColor = palette.Hull;
        canvas.FillPath(hull);

        var rod = new PathF();
        rod.MoveTo(76f, 48f);
        rod.CurveTo(90f, 30f, 104f, 16f, 118f, 10f);
        canvas.StrokeColor = palette.Rod;
        canvas.StrokeSize = 2.5f;
        canvas.DrawPath(rod);

        var line = new PathF();
        line.MoveTo(118f, 10f);
        line.QuadTo(124f, 26f, 118f, 40f);
        canvas.StrokeColor = palette.Water;
        canvas.StrokeSize = 1.5f;
        canvas.DrawPath(line);

        canvas.FillColor = palette.Head;
        canvas.FillCircle(40f, 34f, 5.5f);

        var body = new PathF();
        body.MoveTo(40f, 39f);
        body.QuadTo(38f, 46f, 36f, 54f);
        body.LineTo(50f, 54f);
        body.QuadTo(48f, 46f, 40f, 39f);
        body.Close();
        canvas.FillColor = palette.Green;
        canvas.FillPath(body);

        canvas.StrokeColor = palette.Green;
        canvas.StrokeSize = 3f;
        canvas.DrawLine(38f, 44f, 26f, 52f);

        canvas.FillColor = palette.Teal;
        canvas.FillEllipse(24f - 6.5f, 56f - 7.5f, 13f, 15f);
        canvas.FillColor = palette.Lower;
        canvas.FillEllipse(24f - 3.2f, 66f - 4.5f, 6.4f, 9f);

        var prop = (float)LoaderMotion.PropScale(Phase);
        canvas.FillColor = palette.Water;
        canvas.FillEllipse(24f - 5f, 72f - (1.6f * prop), 10f, 3.2f * prop);
        canvas.RestoreState();
    }

    private void DrawBobber(ICanvas canvas, LoaderPalette palette)
    {
        canvas.StrokeColor = palette.Water;
        canvas.StrokeSize = 3f;
        canvas.DrawPath(Wave(0f, 54f, 32f, 12f, 20f, 7f, 3));

        var rig = LoaderMotion.BobberRig(Phase);
        canvas.SaveState();
        canvas.Translate(0f, (float)rig.Y);
        canvas.Rotate((float)rig.Rotation, 48f, 54f);

        var fromRod = new PathF();
        fromRod.MoveTo(14f, -10f);
        fromRod.QuadTo(28f, 6f, 48f, 16f);
        canvas.StrokeColor = palette.Line;
        canvas.StrokeSize = 0.7f;
        canvas.DrawPath(fromRod);

        canvas.FillColor = palette.Line;
        canvas.FillRoundedRectangle(46.5f, 14f, 3f, 10f, 1.2f);
        canvas.FillColor = Pencil;
        canvas.FillRectangle(45.5f, 22f, 5f, 20f);

        var tip = new PathF();
        tip.MoveTo(45.5f, 42f);
        tip.LineTo(50.5f, 42f);
        tip.LineTo(50.5f, 54f);
        tip.LineTo(48f, 62f);
        tip.LineTo(45.5f, 54f);
        tip.Close();
        canvas.FillColor = palette.Green;
        canvas.FillPath(tip);

        var leader = new PathF();
        leader.MoveTo(48f, 62f);
        leader.QuadTo(49f, 69f, 48f, 73f);
        canvas.StrokeColor = palette.Line;
        canvas.StrokeSize = 0.8f;
        canvas.DrawPath(leader);

        var hook = new PathF();
        hook.MoveTo(48f, 73f);
        hook.LineTo(48f, 78f);
        const float centreX = 50.535f;
        const float centreY = 79.185f;
        const float radius = 2.8f;
        var startAngle = Math.Atan2(78d - centreY, 48d - centreX);
        var endAngle = Math.Atan2(76.8d - centreY, 52d - centreX);
        var sweep = startAngle - endAngle;
        if (sweep < 0d)
        {
            sweep += 2d * Math.PI;
        }

        for (var step = 1; step <= 24; step++)
        {
            var angle = startAngle - (sweep * step / 24d);
            hook.LineTo(centreX + (radius * (float)Math.Cos(angle)), centreY + (radius * (float)Math.Sin(angle)));
        }

        canvas.StrokeColor = palette.Teal;
        canvas.StrokeSize = 1.5f;
        canvas.DrawPath(hook);

        if (LoaderMotion.IsBaitVisible(Phase))
        {
            var worm = new PathF();
            worm.MoveTo(50f, 77f);
            worm.QuadTo(52.5f, 78.2f, 51.5f, 80.2f);
            worm.QuadTo(48.7f, 81.8f, 51f, 83.4f);
            canvas.StrokeColor = Worm;
            canvas.StrokeSize = 1.35f;
            canvas.DrawPath(worm);
        }

        canvas.RestoreState();

        var taker = LoaderMotion.BobberFish(Phase);
        if (taker.Opacity > 0.001d)
        {
            canvas.SaveState();
            canvas.Alpha = (float)taker.Opacity;
            canvas.Translate((float)taker.X, (float)taker.Y);
            DrawFish(canvas, palette.Green, palette.Navy, 11f, 5f, 7f, 6f, -1.8f, 1.2f);
            canvas.RestoreState();
        }
    }

    private sealed record LoaderPalette(Color Navy, Color Teal, Color Rod, Color Green, Color Water, Color Hull, Color Head, Color Lower, Color Line);
}
