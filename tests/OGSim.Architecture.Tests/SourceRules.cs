// R1.12 — the rules that are statements about WHAT WAS WRITTEN, asserted over
// Roslyn syntax. A compiled catch clause is indistinguishable from any other
// branch, and a compiled `await` has been rewritten into a state machine, so
// metadata cannot see either. This half of the suite is the answer to SDD-000
// open item S000-2 for exactly those rules and no others.

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace OGSim.Architecture.Tests;

public class SourceRules
{
    private static IReadOnlyList<EngineCorpus.SourceFile> Sources => EngineCorpus.Sources;

    // ------------------------------------------------------------- L4

    [Fact] // No failure is discarded. Every catch routes through IFaultPolicy, or
           // rethrows a classified carrier for a caller that will. There is no
           // catch {}, no catch { return default; }, no swallow-and-continue.
    public void L4_EveryCatchRoutesThroughTheFaultPolicy()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            if (file.Name is "FaultPolicies.cs" or "Faults.cs") continue;   // the policy itself

            foreach (CatchClauseSyntax clause in EngineCorpus.NodesOf<CatchClauseSyntax>(file))
            {
                string body = clause.Block?.ToString() ?? string.Empty;
                bool routes = body.Contains("Report(", StringComparison.Ordinal)
                    || body.Contains("Fault(", StringComparison.Ordinal)
                    || body.Contains("throw;", StringComparison.Ordinal)
                    // A caught exception RECORDED as a LoadFailure has not been
                    // discarded either: design 09 §5.1 C1 makes the load report
                    // the content fault's designed destination, and the report
                    // is returned to the caller rather than swallowed. L4 is
                    // about nothing being lost, not about one function's name.
                    || body.Contains("LoadFailure(", StringComparison.Ordinal);
                if (!routes)
                    violations.Add($"{EngineCorpus.Where(file, clause)} catch does not route to the fault policy");
            }
        }

        EngineCorpus.AssertNone(violations, "L4 — no failure is discarded");
    }

    // ------------------------------------------------------------- L3

    [Fact] // No member exists without behaviour: no stub, no NotImplementedException,
           // no method that returns a constant standing in for work.
    public void L3_NoMemberWithoutBehaviour()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            foreach (ObjectCreationExpressionSyntax creation in
                     EngineCorpus.NodesOf<ObjectCreationExpressionSyntax>(file))
                if (creation.Type.ToString().Contains("NotImplementedException", StringComparison.Ordinal)
                    || creation.Type.ToString().Contains("NotSupportedException", StringComparison.Ordinal))
                    violations.Add($"{EngineCorpus.Where(file, creation)} throws {creation.Type}");

            foreach (MethodDeclarationSyntax method in EngineCorpus.NodesOf<MethodDeclarationSyntax>(file))
            {
                // An interface or abstract declaration has no body by definition.
                if (method.Body is null && method.ExpressionBody is null) continue;
                if (method.Body is { Statements.Count: 0 })
                    violations.Add($"{EngineCorpus.Where(file, method)} {method.Identifier} has an empty body");
            }
        }

        EngineCorpus.AssertNone(violations, "L3 — no member without behaviour");
    }

    // ------------------------------------------------------------- D-2

    [Fact] // System.Math's transcendentals route to the platform libm and are NOT
           // bit-identical across OS or architecture. DetMath is the sole legal
           // site, and it is the exception the rule itself names.
    public void D2_NoPlatformTranscendentalsOutsideDetMath()
    {
        string[] transcendental =
            ["Exp", "Log", "Log2", "Log10", "Pow", "Sqrt", "Cbrt",
             "Sin", "Cos", "Tan", "Asin", "Acos", "Atan", "Atan2",
             "Sinh", "Cosh", "Tanh"];

        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            if (file.Name == "DetMath.cs") continue;

            foreach (MemberAccessExpressionSyntax access in
                     EngineCorpus.NodesOf<MemberAccessExpressionSyntax>(file))
            {
                if (access.Expression.ToString() is not ("Math" or "System.Math" or "MathF")) continue;
                if (!transcendental.Contains(access.Name.Identifier.Text, StringComparer.Ordinal)) continue;
                violations.Add($"{EngineCorpus.Where(file, access)} uses Math.{access.Name.Identifier.Text}");
            }
        }

        EngineCorpus.AssertNone(violations, "D-2 — no platform transcendentals outside DetMath");
    }

    // ------------------------------------------------------------- D-6

    [Fact] // Wall-clock, ambient randomness and identity-by-luck are all silent
           // determinism breaks: the run differs and nothing announces it.
    public void D6_NoBannedAmbientApis()
    {
        (string Expression, string Member)[] banned =
        [
            ("DateTime", "Now"), ("DateTime", "UtcNow"), ("DateTimeOffset", "Now"),
            ("DateTimeOffset", "UtcNow"), ("Guid", "NewGuid"),
            ("Environment", "TickCount"), ("Environment", "TickCount64"),
            ("Random", "Shared"), ("Stopwatch", "StartNew"),
        ];

        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            foreach (MemberAccessExpressionSyntax access in
                     EngineCorpus.NodesOf<MemberAccessExpressionSyntax>(file))
            {
                string expression = access.Expression.ToString();
                string member = access.Name.Identifier.Text;
                foreach ((string bannedExpression, string bannedMember) in banned)
                    if (expression.EndsWith(bannedExpression, StringComparison.Ordinal)
                        && member == bannedMember)
                        violations.Add($"{EngineCorpus.Where(file, access)} uses {expression}.{member}");
            }

            // `new Random(...)` is the same break wearing a constructor.
            foreach (ObjectCreationExpressionSyntax creation in
                     EngineCorpus.NodesOf<ObjectCreationExpressionSyntax>(file))
                if (creation.Type.ToString() is "Random" or "System.Random")
                    violations.Add($"{EngineCorpus.Where(file, creation)} constructs System.Random");
        }

        EngineCorpus.AssertNone(violations, "D-6 — no banned ambient APIs");
    }

    // ------------------------------------------------------------- D-3

    [Fact] // All simulation arithmetic is double. float is not portable enough and
           // decimal is not fast enough; money is a scaled long instead.
    public void D3_NoFloatOrDecimal()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
            foreach (PredefinedTypeSyntax predefined in EngineCorpus.NodesOf<PredefinedTypeSyntax>(file))
            {
                SyntaxKind kind = predefined.Keyword.Kind();
                if (kind is SyntaxKind.FloatKeyword or SyntaxKind.DecimalKeyword)
                    violations.Add($"{EngineCorpus.Where(file, predefined)} uses {predefined.Keyword.Text}");
            }

        EngineCorpus.AssertNone(violations, "D-3 — no float, no decimal");
    }

    // ------------------------------------------------------------- D-7

    [Fact] // No parallelism inside a tick, and async appears nowhere in the engine:
           // a tick is a sequence, and concurrency would make its order emergent.
    public void D7_NoAsyncOrParallelism()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            foreach (AwaitExpressionSyntax await in EngineCorpus.NodesOf<AwaitExpressionSyntax>(file))
                violations.Add($"{EngineCorpus.Where(file, await)} awaits");

            foreach (IdentifierNameSyntax identifier in EngineCorpus.NodesOf<IdentifierNameSyntax>(file))
            {
                string name = identifier.Identifier.Text;
                if (name is "Task" or "ValueTask" or "Parallel" or "Thread" or "ThreadPool")
                    violations.Add($"{EngineCorpus.Where(file, identifier)} references {name}");
            }
        }

        EngineCorpus.AssertNone(violations, "D-7 — no async, no parallelism");
    }

    // ------------------------------------------------------------- D-5

    [Fact] // Dictionary and HashSet may STORE but never be enumerated: their order
           // is an implementation detail, and a tick that iterates one is a tick
           // whose result depends on hash seeds.
    public void D5_NoEnumerationOfHashOrderedCollections()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
        {
            // File-local: which fields are declared as a hash-ordered collection.
            var hashOrdered = new HashSet<string>(StringComparer.Ordinal);
            foreach (FieldDeclarationSyntax field in EngineCorpus.NodesOf<FieldDeclarationSyntax>(file))
            {
                string declaredType = field.Declaration.Type.ToString();
                if (!declaredType.StartsWith("Dictionary<", StringComparison.Ordinal)
                    && !declaredType.StartsWith("HashSet<", StringComparison.Ordinal)) continue;
                foreach (VariableDeclaratorSyntax variable in field.Declaration.Variables)
                    hashOrdered.Add(variable.Identifier.Text);
            }

            foreach (ForEachStatementSyntax loop in EngineCorpus.NodesOf<ForEachStatementSyntax>(file))
            {
                string source = loop.Expression.ToString().TrimStart('_');
                foreach (string name in hashOrdered)
                    if (source == name || source == name.TrimStart('_')
                        || source.StartsWith(name + ".", StringComparison.Ordinal))
                        violations.Add($"{EngineCorpus.Where(file, loop)} enumerates {name}");
            }
        }

        EngineCorpus.AssertNone(violations, "D-5 — hash-ordered collections are never enumerated");
    }

    // ------------------------------------------------------------- R21-V5

    [Fact] // R21-V5. A rejection is the ONE engine string a player reads verbatim
           // (design 09 §5.1: a command refusal is not a fault). It must say what
           // is wrong in domain terms — so a `Detail` that is empty, or that
           // leaks a type name or an exception, is a refusal a host has to
           // rewrite before it can show it, which is exactly what this surface
           // exists to make unnecessary.
    public void R21V5_every_rejection_reads_as_something_a_player_can_act_on()
    {
        var violations = new List<string>();

        foreach (EngineCorpus.SourceFile file in Sources)
            foreach (ObjectCreationExpressionSyntax creation in
                     EngineCorpus.NodesOf<ObjectCreationExpressionSyntax>(file))
            {
                if (creation.Type.ToString() != "RejectionReason") continue;
                if (creation.ArgumentList is not { Arguments.Count: 2 } arguments) continue;

                string locId = arguments.Arguments[0].ToString();

                // ONLY TEXT THIS SCAN CAN SEE. A detail built from a variable is
                // a refusal authored somewhere else and passed through — the
                // operations engine's own reasons are re-wrapped this way — and
                // judging its length or wording here would flag the wrapper for
                // the wrapped text's sins. That leaves a real blind spot: a
                // refusal composed at runtime is unchecked, and the only thing
                // that would catch it is a test that submits the command.
                if (arguments.Arguments[1].Expression is not LiteralExpressionSyntax
                    and not InterpolatedStringExpressionSyntax) continue;

                string detail = arguments.Arguments[1].ToString();

                // The localisation key is what a host swaps for translated text,
                // so a missing one makes the English the only version there will
                // ever be.
                if (!locId.Contains("$loc:", StringComparison.Ordinal))
                    violations.Add($"{EngineCorpus.Where(file, creation)} rejection has no " +
                                   $"$loc key: {locId}");

                // A DOMAIN sentence, not a diagnostic. The three words below are
                // what leaks when a refusal is written from the code's point of
                // view rather than the player's.
                foreach (string leak in (string[])["Exception", "NullReference", "nameof("])
                    if (detail.Contains(leak, StringComparison.Ordinal))
                        violations.Add($"{EngineCorpus.Where(file, creation)} rejection text " +
                                       $"leaks '{leak}', so a host cannot show it as written");

                // Long enough to be a reason. A three-word refusal names the rule
                // it broke and never why, which is the difference between "no"
                // and an answer a player can do something about.
                if (detail.Length < 25)
                    violations.Add($"{EngineCorpus.Where(file, creation)} rejection text is " +
                                   $"too short to be a reason: {detail}");
            }

        EngineCorpus.AssertNone(
            violations, "R21-V5 — every rejection is player-facing text");
    }

    // ------------------------------------------------------------- D-8

    [Fact] // Culture-sensitive formatting turns a decimal point into a comma and a
           // save into a different save. Formatting with an explicit format string
           // must carry InvariantCulture with it.
    public void D8_FormattingIsCultureInvariant()
    {
        var violations = new List<string>();
        foreach (EngineCorpus.SourceFile file in Sources)
            foreach (InvocationExpressionSyntax invocation in
                     EngineCorpus.NodesOf<InvocationExpressionSyntax>(file))
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax access) continue;
                if (access.Name.Identifier.Text is not ("ToString" or "Parse" or "TryParse")) continue;

                // JSON parsing cannot be culture-sensitive: RFC 8259 fixes the
                // number grammar to a '.' separator, and JsonDocument.Parse
                // accepts no culture to get wrong. Exempted by RECEIVER rather
                // than by an allowlist of call sites, so the exemption cannot
                // drift into covering a real numeric parse.
                // Enum.TryParse is the same case: enum member names are ordinal
                // identifiers, not culture-sensitive text, and the overload
                // accepts no culture to get wrong.
                if (access.Expression.ToString() is "JsonDocument" or "JsonSerializer"
                    or "JsonNode" or "JsonElement" or "Enum") continue;

                string arguments = invocation.ArgumentList.ToString();
                bool hasFormatOrParse =
                    access.Name.Identifier.Text != "ToString" || arguments.Contains('"');
                if (!hasFormatOrParse) continue;
                if (arguments.Contains("InvariantCulture", StringComparison.Ordinal)) continue;

                violations.Add($"{EngineCorpus.Where(file, invocation)} " +
                               $"{access.Name.Identifier.Text}{arguments} without InvariantCulture");
            }

        EngineCorpus.AssertNone(violations, "D-8 — formatting and parsing are culture-invariant");
    }

    // ------------------------------------------------------------- F-2

    /// <summary>
    /// F-2's enforceable half, today.
    ///
    /// The rule as written — "no numeric literal in simulation code except 0 and
    /// 1" — cannot be mechanised over the kernel, and trying it produced 38
    /// findings of which every single one was a false positive: enum member
    /// values, `Year * 12`, `vertices.Length &lt; 3`, the shoelace `0.5`. None of
    /// those is a physical claim, which is what F-2 actually protects against. A
    /// check with a 100% false-positive rate does not get tightened, it gets
    /// disabled, so the literal scan is deferred to R5 where formula code first
    /// exists, and SDD-000 §8 now carries the scope (see R1 §7).
    ///
    /// What IS checkable now is F-2's positive half: constants live in one place
    /// and each carries its citation and unit.
    /// </summary>
    [Fact]
    public void F2_EveryPhysicalConstantCarriesItsCitation()
    {
        EngineCorpus.SourceFile file = Sources.Single(f => f.Name == "Quantities.cs");

        ClassDeclarationSyntax constants = EngineCorpus.NodesOf<ClassDeclarationSyntax>(file)
            .Single(c => c.Identifier.Text == "PhysicalConstants");

        var violations = new List<string>();
        int checkedCount = 0;

        foreach (FieldDeclarationSyntax field in constants.Members.OfType<FieldDeclarationSyntax>())
        {
            checkedCount++;
            string name = field.Declaration.Variables[0].Identifier.Text;

            if (!field.Modifiers.Any(SyntaxKind.ConstKeyword))
                violations.Add($"PhysicalConstants.{name} is not const");

            // The citation is the point: a constant whose provenance is not
            // written down is exactly the unreviewable number F-3 forbids.
            string documentation = field.GetLeadingTrivia().ToFullString();
            bool cited = documentation.Contains("SDD-", StringComparison.Ordinal)
                || documentation.Contains("design ", StringComparison.Ordinal);
            if (!cited)
                violations.Add($"PhysicalConstants.{name} carries no SDD or design citation");
        }

        Assert.True(checkedCount > 0, "PhysicalConstants is empty — the rule has nothing to guard");
        EngineCorpus.AssertNone(violations, "F-2 — every physical constant is cited");
    }
}
