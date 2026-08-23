// The host reads the files; the engine reads no disk (SDD-004 §7).

using OGSim.Kernel;

namespace OGSim.Engineer;

/// <summary>
/// The repository's own <c>content/</c>, read off disk.
/// </summary>
/// <remarks>
/// <para><b>A client's job, not the engine's.</b> The engine never opens a file
/// — a source hands it text somebody else has already read, which is what lets
/// the Godot host serve the same content out of <c>res://</c> and a console
/// client serve it out of a directory, with the engine unable to tell.</para>
///
/// <para>Anchored on this file's compile-time path so it finds the directory
/// from any working directory, the same way the test suites do. A shipped build
/// would carry its content beside the executable; this is a development client
/// and says so rather than pretending otherwise.</para>
/// </remarks>
public static class RepositoryContent
{
    /// <summary>
    /// Every kind the engine loads.
    /// </summary>
    /// <remarks>
    /// The list is spelled out rather than discovered by enumerating the
    /// directory, because a kind the engine does not know is a refusal to start
    /// and finding that out from whatever happens to be on disk would make the
    /// startup depend on a stray folder.
    /// </remarks>
    private static readonly string[] Kinds =
    {
        "facilities", "technologies", "terrain-classes", "contracts", "wells", "fluid-systems",

        // The game catalogues (plans 28) — validated by the same loader, and
        // the relations' cross-references resolve because everything they name
        // loads in this same set.
        "activities", "equipment", "relations", "game-styles", "starting-states",
        "world-templates",
    };

    public static IContentSource Read(
        [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        // src/OGSim.Engineer -> src -> the repository root
        DirectoryInfo here = new FileInfo(thisFile).Directory!;
        string root = Path.Combine(here.Parent!.Parent!.FullName, "content");

        var files = new List<ContentFile>();

        for (var i = 0; i < Kinds.Length; i++)
        {
            string at = Path.Combine(root, Kinds[i]);

            if (!Directory.Exists(at))
                throw new DirectoryNotFoundException(
                    $"content/{Kinds[i]} is missing; the engine refuses to start without it");

            foreach (string path in Directory.EnumerateFiles(at, "*.json")
                                             .OrderBy(p => p, StringComparer.Ordinal))
                files.Add(new ContentFile(
                    Kinds[i] + "/" + Path.GetFileName(path), File.ReadAllText(path)));
        }

        return new DirectorySource("repository", 0, files);
    }

    /// <summary>
    /// A style's departures in VALUES — `content/styles/{id}/`, mirroring the
    /// same kind directories, at order 1 so an entry there REPLACES the base
    /// entry wholesale (SDD-004 §7's override rule). Null when the style has
    /// none: Oilfield Engineer IS the base values, so it ships no overlay.
    /// </summary>
    /// <remarks>
    /// This is how a style re-prices without a second owner: the base entry
    /// stays the engine's own value, the overlay entry is the style's, and the
    /// loader's override semantics pick exactly one — never a merge, never a
    /// branch in code.
    /// </remarks>
    public static IContentSource? StyleOverrides(
        ContentId style,
        [System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        DirectoryInfo here = new FileInfo(thisFile).Directory!;
        string root = Path.Combine(
            here.Parent!.Parent!.FullName, "content", "styles", style.Value);

        if (!Directory.Exists(root)) return null;

        var files = new List<ContentFile>();

        for (int i = 0; i < Kinds.Length; i++)
        {
            string at = Path.Combine(root, Kinds[i]);
            if (!Directory.Exists(at)) continue;

            foreach (string path in Directory.EnumerateFiles(at, "*.json")
                                             .OrderBy(p => p, StringComparer.Ordinal))
                files.Add(new ContentFile(
                    Kinds[i] + "/" + Path.GetFileName(path), File.ReadAllText(path)));
        }

        return files.Count == 0 ? null : new DirectorySource("style:" + style.Value, 1, files);
    }
}

/// <summary>Files already read, handed over in a fixed order.</summary>
internal sealed class DirectorySource(
    string name, int order, IReadOnlyList<ContentFile> files) : IContentSource
{
    public string Name => name;

    /// <summary>0 for base content; a style overlay declares 1 (SDD-004 §7).</summary>
    public int DeclaredOrder => order;

    public IReadOnlyList<ContentFile> Files => files;
}
