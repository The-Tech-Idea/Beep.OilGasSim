#nullable enable

using Godot;
using OGSim.Composition;
using OilfieldDays.App;
using OilfieldDays.World;

namespace OilfieldDays.Screens;

/// <summary>
/// The basin drawn small, with the chosen structure lit - the mockup's placement
/// preview, over the ground the world is actually tiled from.
/// </summary>
[Tool]
public sealed partial class LeaseMap : Control
{
	private FieldReadModel? _snapshot;
	private ulong _selected;

	public void Bind(FieldReadModel snapshot, ulong selected)
	{
		_snapshot = snapshot;
		_selected = selected;
		QueueRedraw();
	}

	public override void _Draw()
	{
		Vector2 size = Size;
		BasinWorld? world = Gameplay.Current?.World;

		DrawRect(new Rect2(Vector2.Zero, size), KitTheme.Void);

		if (world is null || _snapshot is null)
			return;

		int tiles = world.Tiles;
		int step = Mathf.Max(1, tiles / 80);
		float scale = size.X / tiles;

		for (int y = 0; y < tiles; y += step)
		{
			for (int x = 0; x < tiles; x += step)
			{
				Color colour = world.Terrain.At(new Vector2I(x, y)) switch
				{
					Ground.Water => new Color(0.30f, 0.55f, 0.75f),
					Ground.Sand => new Color(0.72f, 0.60f, 0.42f),
					Ground.Rock => new Color(0.42f, 0.42f, 0.44f),
					_ => world.Terrain.IsDry(new Vector2I(x, y))
						? new Color(0.44f, 0.50f, 0.28f)
						: new Color(0.40f, 0.62f, 0.30f),
				};

				DrawRect(new Rect2(x * scale, y * scale, step * scale + 1.0f, step * scale + 1.0f), colour);
			}
		}

		// The grid the mockup draws over the lease, so a placement reads as a
		// square of ground rather than a dot on a picture.
		for (int i = 0; i <= 12; i++)
		{
			float at = size.X / 12.0f * i;
			DrawLine(new Vector2(at, 0), new Vector2(at, size.Y), new Color(1, 1, 1, 0.07f));
			DrawLine(new Vector2(0, at), new Vector2(size.X, at), new Color(1, 1, 1, 0.07f));
		}

		float perMetre = size.X / (tiles / (float)BasinWorld.TilesPerKilometre * (float)BasinWorld.MetresPerCell);

		for (int i = 0; i < _snapshot.Prospects.Count; i++)
		{
			ProspectView prospect = _snapshot.Prospects[i];
			var at = new Vector2((float)prospect.At.X * perMetre, (float)prospect.At.Y * perMetre);
			bool chosen = prospect.Prospect.Value == _selected;

			Color colour = prospect.ProbabilityOfSuccess switch
			{
				< 0.20 => KitTheme.Red,
				< 0.35 => KitTheme.Amber,
				_ => KitTheme.Green,
			};

			if (chosen)
			{
				// The green square the mockup lights under the thing being placed.
				var pad = new Rect2(at - new Vector2(16, 16), new Vector2(32, 32));
				DrawRect(pad, new Color(colour, 0.35f));
				DrawRect(pad, colour, filled: false, width: 2.0f);
			}

			DrawCircle(at, chosen ? 7.0f : 5.0f, colour);
		}
	}
}
