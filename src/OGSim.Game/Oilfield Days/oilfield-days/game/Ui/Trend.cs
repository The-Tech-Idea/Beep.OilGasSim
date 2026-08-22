#nullable enable

using System.Collections.Generic;
using Godot;
using OilfieldDays.App;

namespace OilfieldDays.Ui;

/// <summary>
/// The gameplay production trend: what was produced each month, drawn as a line.
/// </summary>
[Tool]
public sealed partial class Trend : Control
{
    private const int Window = 36;

    private readonly List<double> _points = new();

    /// <summary>How many months are on the chart.</summary>
    public int Count => _points.Count;

    /// <summary>The highest month recorded, which is what the chart scales to.</summary>
    public double Peak { get; private set; }

    public void Push(double value)
    {
        _points.Add(value);

        if (_points.Count > Window)
            _points.RemoveAt(0);

        Peak = 0.0;

        for (int i = 0; i < _points.Count; i++)
        {
            if (_points[i] > Peak)
                Peak = _points[i];
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.05f, 0.09f, 0.13f, 0.85f));

        for (int i = 1; i < 4; i++)
        {
            float y = Size.Y * i / 4.0f;
            DrawLine(new Vector2(0, y), new Vector2(Size.X, y), new Color(1, 1, 1, 0.06f));
        }

        if (_points.Count < 2 || Peak <= 0.0)
            return;

        float step = Size.X / (Window - 1);

        for (int i = 1; i < _points.Count; i++)
        {
            var from = new Vector2((i - 1) * step, Height(_points[i - 1]));
            var to = new Vector2(i * step, Height(_points[i]));

            DrawLine(from, to, KitTheme.Amber, 2.0f, antialiased: true);
        }

        DrawCircle(
            new Vector2((_points.Count - 1) * step, Height(_points[^1])), 3.0f, KitTheme.Amber);
    }

    private float Height(double value) =>
        Size.Y - (float)(value / Peak * (Size.Y - 6.0)) - 3.0f;
}
