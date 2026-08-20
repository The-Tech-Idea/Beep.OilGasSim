#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;

namespace OilfieldDays;

/// <summary>
/// A development-only knife: find every element on a supplied UI atlas and cut
/// it out as its own file.
///
/// <para>The atlases are sheets of finished widgets on a flat dark ground. Rather
/// than measure them by eye off a screenshot — which puts a pixel of the
/// neighbouring ground in every nine-patch and shows up as a seam on screen — the
/// pieces are found by flood-filling the ground away and taking the bounding box
/// of what is left. What comes out is exact, and the index it prints is what the
/// chrome then names its pieces by.</para>
///
/// <para><c>--slice-index</c> reports where every element on every sheet is and
/// writes nothing; <c>--slice</c> cuts the named pieces the chrome asks for. Both
/// write into the project rather than a build, because the output is checked in
/// as art:</para>
///
/// <code>Godot.exe --path &lt;project&gt; -- --slice</code>
/// </summary>
public static class DevAtlasSlice
{
    private const string Source = "res://assets/ui/atlas";
    private const string Out = "res://assets/ui/nine";

    /// <summary>How far a pixel may sit from the sheet's ground and still be ground.</summary>
    private const float GroundTolerance = 0.055f;

    /// <summary>Anything smaller than this is a stray mark, not a widget.</summary>
    private const int SmallestPiece = 14;

    /// <summary>
    /// The pieces the chrome is built from, and where they are on the sheets.
    /// </summary>
    /// <remarks>
    /// <para>The rectangles came from <c>--slice-index</c>, which finds every
    /// element on a sheet by flood-filling its ground away. They are written down
    /// rather than re-detected at load: art is checked in, and a game that
    /// re-derived its own button edges every launch would change appearance the
    /// day someone nudged a sheet.</para>
    ///
    /// <para><b>Cap is the squeeze.</b> The sheets' buttons have their labels
    /// painted into them, and a nine-patch stretches its middle — so a plate
    /// taken whole would smear "Confirm" across the button. A cap of N keeps the
    /// left N and right N pixels, which is the rounded end and its rim, and
    /// throws the lettered middle away. What is left tiles to any width. Zero
    /// means the piece is already clean and is taken as it is.</para>
    /// </remarks>
    private static readonly (string Name, int Sheet, Rect2I From, int Cap)[] Wanted =
    {
        ("panel", 1, new Rect2I(13, 433, 242, 119), 0),
        ("panel-paper", 1, new Rect2I(273, 431, 262, 122), 0),
        ("plate-blue", 2, new Rect2I(73, 42, 253, 86), 46),
        ("plate-slate", 2, new Rect2I(338, 42, 244, 86), 46),
        ("plate-green", 2, new Rect2I(594, 42, 243, 86), 46),
        ("plate-red", 2, new Rect2I(848, 42, 243, 86), 46),
        ("plate-amber", 2, new Rect2I(68, 651, 224, 64), 40),
        // Not the strip at (1135,240): two plates touch there, so the detector
        // boxed them as one and a field cut from it drew as two widgets side by
        // side. This is the single plain plate at the left of the same row.
        ("field", 1, new Rect2I(825, 240, 295, 90), 0),
    };

    public static bool RunIfRequested()
    {
        bool cut = false;
        bool index = false;

        foreach (string argument in OS.GetCmdlineUserArgs())
        {
            if (argument == "--slice")
                cut = true;
            else if (argument == "--slice-index")
                index = true;
        }

        if (index)
        {
            for (int sheet = 1; sheet <= 8; sheet++)
                Index(sheet);
        }

        if (cut)
            Compose();

        return cut || index;
    }

    /// <summary>Cut the named pieces out of the sheets, ready to nine-patch.</summary>
    private static void Compose()
    {
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(Out));

        foreach ((string name, int sheet, Rect2I from, int cap) in Wanted)
        {
            Image? source = Load(sheet);

            if (source is null)
                continue;

            Image piece = source.GetRegion(from);

            if (cap > 0)
                piece = Squeeze(piece, cap);

            Error error = piece.SavePng($"{Out}/{name}.png");

            GD.Print(error == Error.Ok
                ? $"[slice] {name}.png  {piece.GetWidth()}x{piece.GetHeight()}"
                : $"[slice] could not write {name}.png: {error}");
        }
    }

    /// <summary>Keep both ends of a plate and drop the lettering between them.</summary>
    private static Image Squeeze(Image piece, int cap)
    {
        int height = piece.GetHeight();
        int width = piece.GetWidth();

        if (width <= cap * 2)
            return piece;

        Image squeezed = Image.CreateEmpty(cap * 2, height, false, piece.GetFormat());

        squeezed.BlitRect(piece, new Rect2I(0, 0, cap, height), Vector2I.Zero);
        squeezed.BlitRect(
            piece, new Rect2I(width - cap, 0, cap, height), new Vector2I(cap, 0));

        return squeezed;
    }

    private static Image? Load(int sheet)
    {
        string path = $"{Source}/atlas{sheet.ToString(CultureInfo.InvariantCulture)}.png";
        var texture = GD.Load<Texture2D>(path);

        if (texture is not null)
            return texture.GetImage();

        GD.PushError($"[slice] cannot load {path}");

        return null;
    }

    /// <summary>Report where every element on a sheet is, so pieces can be named.</summary>
    private static void Index(int sheet)
    {
        Image? loaded = Load(sheet);

        if (loaded is null)
            return;

        Image image = loaded;
        int width = image.GetWidth();
        int height = image.GetHeight();

        // The ground is whatever colour the corner is. Every sheet is a widget
        // board with a flat margin, so the corner is never part of a piece.
        Color ground = image.GetPixel(1, 1);

        bool[] solid = new bool[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
                solid[(y * width) + x] = !IsGround(image.GetPixel(x, y), ground);
        }

        List<Rect2I> pieces = Boxes(solid, width, height);

        GD.Print($"[slice] atlas{sheet}: {pieces.Count} pieces");

        for (int i = 0; i < pieces.Count; i++)
        {
            Rect2I box = pieces[i];

            GD.Print($"[slice]   a{sheet}_{i:00}  x={box.Position.X} y={box.Position.Y} " +
                     $"w={box.Size.X} h={box.Size.Y}");
        }
    }

    private static bool IsGround(Color pixel, Color ground) =>
        pixel.A < 0.08f
        || (Mathf.Abs(pixel.R - ground.R) < GroundTolerance
            && Mathf.Abs(pixel.G - ground.G) < GroundTolerance
            && Mathf.Abs(pixel.B - ground.B) < GroundTolerance);

    /// <summary>
    /// Bounding boxes of the connected runs of non-ground pixels.
    /// </summary>
    /// <remarks>
    /// Four-connected and iterative. A recursive flood fill over a 1.5 megapixel
    /// sheet overflows the stack on the first full-width panel, which is the
    /// first thing every one of these atlases has.
    /// </remarks>
    private static List<Rect2I> Boxes(bool[] solid, int width, int height)
    {
        var boxes = new List<Rect2I>();
        bool[] seen = new bool[solid.Length];
        var stack = new Stack<int>();

        for (int start = 0; start < solid.Length; start++)
        {
            if (!solid[start] || seen[start])
                continue;

            int minX = int.MaxValue;
            int minY = int.MaxValue;
            int maxX = int.MinValue;
            int maxY = int.MinValue;

            stack.Push(start);
            seen[start] = true;

            while (stack.Count > 0)
            {
                int at = stack.Pop();
                int x = at % width;
                int y = at / width;

                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;

                Push(stack, seen, solid, width, height, x - 1, y);
                Push(stack, seen, solid, width, height, x + 1, y);
                Push(stack, seen, solid, width, height, x, y - 1);
                Push(stack, seen, solid, width, height, x, y + 1);
            }

            if (maxX - minX + 1 < SmallestPiece || maxY - minY + 1 < SmallestPiece)
                continue;

            boxes.Add(new Rect2I(minX, minY, maxX - minX + 1, maxY - minY + 1));
        }

        // Reading order, so the printed index runs the way the sheet does.
        boxes.Sort((a, b) =>
        {
            int rowA = a.Position.Y / 24;
            int rowB = b.Position.Y / 24;

            return rowA != rowB ? rowA.CompareTo(rowB) : a.Position.X.CompareTo(b.Position.X);
        });

        return boxes;
    }

    private static void Push(
        Stack<int> stack, bool[] seen, bool[] solid, int width, int height, int x, int y)
    {
        if (x < 0 || y < 0 || x >= width || y >= height)
            return;

        int at = (y * width) + x;

        if (seen[at] || !solid[at])
            return;

        seen[at] = true;
        stack.Push(at);
    }
}
