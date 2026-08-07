// R1.12 — what the architecture suite examines.
//
// Design 12 §2 is explicit that these checks assert on COMPILED METADATA rather
// than file text: the previous generation's guard tests asserted on source
// strings, broke whenever a file was renamed, and could be defeated by a
// comment. Metadata is rename-safe and comment-proof.
//
// Some rules are nevertheless statements about SOURCE SYNTAX and have no
// metadata form — "every catch routes through the fault policy" is about a catch
// clause, and a compiled catch is indistinguishable from any other branch.
// SDD-000 open item S000-2 asked which way to go. The answer this suite settles
// on is BOTH, chosen per rule: reflection where the rule is about shape, Roslyn
// syntax where the rule is about what was written. Neither is used where the
// other is the honest instrument.

using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OGSim.Architecture.Tests;

internal static class EngineCorpus
{
    /// <summary>
    /// Loaded BY NAME rather than through a type, because OGSim.Subsurface has
    /// no public type to name — which is exactly what
    /// <c>Truth_SubsurfaceExposesNoPublicType</c> asserts. A corpus that could
    /// only reach an assembly through its public surface would silently skip the
    /// one assembly whose whole point is not having one.
    ///
    /// <para>Loaded from the output DIRECTORY rather than through this
    /// assembly's reference list, because the compiler omits a reference no type
    /// uses — and no type here can use one. The project reference is what puts
    /// the file next to us; this is what opens it.</para>
    ///
    /// <para>Declared BEFORE <see cref="Assemblies"/>: static initialisers run in
    /// declaration order, so the other way round the corpus would hold a null and
    /// every metadata rule would quietly stop covering this assembly.</para>
    /// </summary>
    public static Assembly Subsurface { get; } = LoadBesideUs("OGSim.Subsurface");

    private static Assembly LoadBesideUs(string assemblyName)
    {
        string path = Path.Combine(AppContext.BaseDirectory, assemblyName + ".dll");

        Assert.True(File.Exists(path),
            $"{assemblyName}.dll is not in the test output at {path}; without it this rule " +
            "would pass by having nothing to check");

        return Assembly.LoadFrom(path);
    }

    /// <summary>Every engine assembly currently built. The suite scales with the
    /// solution: a new module is covered the day its project is referenced.</summary>
    public static IReadOnlyList<Assembly> Assemblies { get; } =
    [
        typeof(OGSim.Kernel.Money).Assembly,
        typeof(OGSim.Contracts.IEngine).Assembly,
        typeof(OGSim.Flow.FlowSolver).Assembly,
        typeof(OGSim.Wells.Completion).Assembly,
        typeof(OGSim.Facilities.Tank).Assembly,
        typeof(OGSim.Operations.Operation).Assembly,
        typeof(OGSim.Company.CostLedger).Assembly,
        Subsurface,
    ];

    public static IEnumerable<Type> Types =>
        Assemblies.SelectMany(a => a.GetTypes()).Where(t => !IsCompilerGenerated(t));

    private static bool IsCompilerGenerated(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null
        || type.Name.Contains('<');

    // ------------------------------------------------------------- source

    public sealed record SourceFile(string Path, string Name, SyntaxNode Root, string Text);

    private static IReadOnlyList<SourceFile>? _sources;

    /// <summary>Every .cs file under src/, parsed. bin/ and obj/ are excluded:
    /// generated assembly-info would otherwise trip half these rules.</summary>
    public static IReadOnlyList<SourceFile> Sources => _sources ??= LoadSources();

    private static IReadOnlyList<SourceFile> LoadSources()
    {
        string src = Path.Combine(RepositoryRoot(), "src");
        Assert.True(Directory.Exists(src), $"engine source not found at {src}");

        var files = new List<SourceFile>();
        foreach (string path in Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                     .OrderBy(p => p, StringComparer.Ordinal))
        {
            string normalised = path.Replace('\\', '/');
            if (normalised.Contains("/bin/") || normalised.Contains("/obj/")) continue;

            string text = File.ReadAllText(path);
            files.Add(new SourceFile(
                normalised, Path.GetFileName(path),
                CSharpSyntaxTree.ParseText(text).GetRoot(), text));
        }

        Assert.NotEmpty(files);
        return files;
    }

    /// <summary>Anchored to this file's own compile-time path, so the suite finds
    /// the tree from any working directory and any test runner.</summary>
    private static string RepositoryRoot([CallerFilePath] string thisFile = "")
    {
        DirectoryInfo directory = new FileInfo(thisFile).Directory!;   // .../tests/OGSim.Architecture.Tests
        return directory.Parent!.Parent!.FullName;                     // .../
    }

    /// <summary>Reports every offender at once. A rule that failed on the first
    /// violation would take one run per violation to clear, which is the same
    /// complaint R1 §2.9 makes about composition.</summary>
    public static void AssertNone(IEnumerable<string> violations, string rule)
    {
        string[] found = [.. violations.OrderBy(v => v, StringComparer.Ordinal)];
        Assert.True(found.Length == 0,
            $"{rule} — {found.Length} violation(s):{Environment.NewLine}  " +
            string.Join(Environment.NewLine + "  ", found));
    }

    public static string Where(SourceFile file, SyntaxNode node) =>
        $"{file.Name}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}";

    public static IEnumerable<T> NodesOf<T>(SourceFile file) where T : SyntaxNode =>
        file.Root.DescendantNodes().OfType<T>();
}
