using Microsoft.Maui.Graphics;

namespace FishingLogBook.Mobile.Native.Controls;

public static class FishDrawing
{
    private const float SourceWidth = 32f;
    private const float SourceHeight = 20f;

    public static void Draw(ICanvas canvas, RectF bounds, Color body, Color eye, bool facesRight)
    {
        var scale = Math.Min(bounds.Width / SourceWidth, bounds.Height / SourceHeight);
        var drawnWidth = SourceWidth * scale;
        var drawnHeight = SourceHeight * scale;
        var left = bounds.X + ((bounds.Width - drawnWidth) / 2f);
        var top = bounds.Y + ((bounds.Height - drawnHeight) / 2f);
        canvas.SaveState();
        if (facesRight)
        {
            canvas.Translate(left + drawnWidth, top);
            canvas.Scale(-scale, scale);
        }
        else
        {
            canvas.Translate(left, top);
            canvas.Scale(scale, scale);
        }

        var fish = new PathF();
        fish.MoveTo(2f, 10f);
        fish.CurveTo(7f, 2f, 17f, 2f, 22f, 10f);
        fish.CurveTo(17f, 18f, 7f, 18f, 2f, 10f);
        fish.Close();
        var tail = new PathF();
        tail.MoveTo(22f, 10f);
        tail.LineTo(30f, 4f);
        tail.LineTo(30f, 16f);
        tail.Close();
        canvas.FillColor = body;
        canvas.FillPath(fish);
        canvas.FillPath(tail);
        canvas.FillColor = eye;
        canvas.FillCircle(8f, 8.5f, 1.2f);
        canvas.RestoreState();
    }
}
