#nullable enable

using System.Collections.Generic;
using System.IO;
using Godot;
using OGSim.Kernel;

namespace OilfieldDays.Host;

/// <summary>
/// The game's content, read off disk by the host and handed to the engine.
///
/// <para><b>The engine never touches a disk</b> (SDD-004 §7, and the note on
/// <c>EngineSettings.Content</c>): an <see cref="IContentSource"/> hands over
/// files it has already read, which is what keeps the engine assemblies free of
/// I/O and lets a host ship content from a folder, a pack file or a download
/// without composition knowing which. Godot's <c>FileAccess</c> reads through
/// <c>res://</c> in the editor and out of the .pck in an export, so one path
/// works for both.</para>
///
/// <para>Base content declares order 0. A mod would be a second source at a
/// higher order, and the loader's override rules would take it from there.</para>
/// </summary>
public sealed class GodotContentSource : IContentSource
{
	private const string Root = "res://content";

	private readonly List<ContentFile> _files = new();

	private GodotContentSource(string name, int order)
	{
		Name = name;
		DeclaredOrder = order;
	}

	public string Name { get; }

	public int DeclaredOrder { get; }

	public IReadOnlyList<ContentFile> Files => _files;

	/// <summary>
	/// Read the content folders the engine has kinds for.
	/// </summary>
	/// <remarks>
	/// <para><b>Not everything under <c>content/</c>.</b> The loader rejects an
	/// entry whose kind is not registered, and rejection is a refusal to start
	/// rather than a warning (design 10 §3, G2) — so handing over
	/// <c>materials/</c>, <c>property-kinds/</c> or <c>rock-types/</c> today
	/// stops the game with "unknown content kind 'material'". Those files are
	/// authored and waiting for the phase that registers their kinds.</para>
	///
	/// <para>The set is the same one the engine's own composition tests ship,
	/// which is the honest definition of "what the engine can currently load".
	/// When a kind is registered, its folder is added here and nothing else
	/// changes.</para>
	/// </remarks>
	public static GodotContentSource Shipped()
	{
		var source = new GodotContentSource("base", 0);

		foreach (string folder in Loadable)
			source.ReadFolder($"{Root}/{folder}", folder + "/", required: true);

		return source;
	}

	/// <summary>
	/// This product's style overlay — `content/styles/{id}/` at order 1, so an
	/// entry there REPLACES the base entry wholesale (SDD-004 §7). Null when
	/// the style ships no departures.
	/// </summary>
	public static GodotContentSource? StyleOverrides(ContentId style)
	{
		var source = new GodotContentSource("style:" + style.Value, 1);

		foreach (string folder in Loadable)
			source.ReadFolder($"{Root}/styles/{style.Value}/{folder}", folder + "/", required: false);

		return source.Count == 0 ? null : source;
	}

	/// <summary>The folders whose kinds the engine registers today.</summary>
	private static readonly string[] Loadable =
	{
		"facilities",
		"technologies",
		"terrain-classes",
		"contracts",
		"wells",
		"fluid-systems",

		// The game catalogues (plans 28), same set as RepositoryContent.
		"activities",
		"equipment",
		"relations",
		"game-styles",
		"starting-states",
		"world-templates",
	};

	/// <summary>How many files were found. Zero means the engine will refuse to start.</summary>
	public int Count => _files.Count;

	private void ReadFolder(string path, string prefix, bool required)
	{
		using DirAccess? directory = DirAccess.Open(path);

		if (directory is null)
		{
			if (ReadGlobalizedFolder(path, prefix, required))
				return;

			if (required)
				GD.PushError($"[content] cannot open {path}: {DirAccess.GetOpenError()}");

			return;
		}

		// Sorted, because load order decides which of two entries with one id
		// wins, and a host that handed them over in directory order would make
		// that depend on the filesystem.
		string[] folders = directory.GetDirectories();
		System.Array.Sort(folders, System.StringComparer.Ordinal);

		string[] files = directory.GetFiles();
		System.Array.Sort(files, System.StringComparer.Ordinal);

		foreach (string file in files)
		{
			// An exported project keeps text files as they are, but Godot may
			// still hand back the import stub for anything it converted, so only
			// JSON is taken and the rest is left alone.
			if (!file.EndsWith(".json", System.StringComparison.Ordinal))
				continue;

			using Godot.FileAccess? handle = Godot.FileAccess.Open($"{path}/{file}", Godot.FileAccess.ModeFlags.Read);

			if (handle is null)
			{
				GD.PushError($"[content] cannot read {path}/{file}: {Godot.FileAccess.GetOpenError()}");
				continue;
			}

			_files.Add(new ContentFile(prefix + file, handle.GetAsText()));
		}

		foreach (string folder in folders)
			ReadFolder($"{path}/{folder}", $"{prefix}{folder}/", required);

		// Nested folders are still walked, so a kind that grows sub-folders
		// needs no change here.
	}

	private bool ReadGlobalizedFolder(string path, string prefix, bool required)
	{
		string native = ProjectSettings.GlobalizePath(path);

		if (!Directory.Exists(native))
			return !required;

		string[] files = Directory.GetFiles(native, "*.json", SearchOption.TopDirectoryOnly);
		System.Array.Sort(files, System.StringComparer.Ordinal);

		foreach (string file in files)
			_files.Add(new ContentFile(prefix + Path.GetFileName(file), File.ReadAllText(file)));

		string[] folders = Directory.GetDirectories(native);
		System.Array.Sort(folders, System.StringComparer.Ordinal);

		foreach (string folder in folders)
		{
			string name = Path.GetFileName(folder);
			ReadGlobalizedFolder($"{path}/{name}", $"{prefix}{name}/", required);
		}

		return true;
	}
}
