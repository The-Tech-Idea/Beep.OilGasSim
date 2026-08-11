// R1.12 — the rules that are statements about SHAPE, asserted over compiled
// metadata (design 12 §2). Each test is named for the law it enforces, which is
// what makes design 22 §6.1's "every law has a test" check mechanical.

using System.Reflection;
using System.Runtime.CompilerServices;
using OGSim.Kernel;

namespace OGSim.Architecture.Tests;

public class MetadataRules
{
    private static IEnumerable<Type> EngineTypes => EngineCorpus.Types;

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance |
        BindingFlags.Static | BindingFlags.DeclaredOnly;

    // ------------------------------------------------------------- L2

    [Fact] // No static mutable state. A static field that can be written is a
           // back channel between modules that composition cannot see.
    public void L2_NoStaticMutableState()
    {
        var violations = new List<string>();
        foreach (Type type in EngineTypes)
            foreach (FieldInfo field in type.GetFields(AllDeclared))
            {
                if (!field.IsStatic || field.IsLiteral || field.IsInitOnly) continue;
                violations.Add($"{type.FullName}.{field.Name}");
            }

        EngineCorpus.AssertNone(violations, "L2 — no static mutable state");
    }

    [Fact] // No singletons. `Instance` is the name the pattern always takes.
    public void L2_NoInstanceMember()
    {
        var violations = new List<string>();
        foreach (Type type in EngineTypes)
            foreach (MemberInfo member in type.GetMembers(AllDeclared))
            {
                if (!string.Equals(member.Name, "Instance", StringComparison.Ordinal)) continue;
                violations.Add($"{type.FullName}.{member.Name}");
            }

        EngineCorpus.AssertNone(violations, "L2 — no member named Instance");
    }

    [Fact] // No dependency has a default. Forgetting to supply a collaborator must
           // be a compile error, never a silently-wrong object.
    public void L2_NoOptionalContractParameter()
    {
        var violations = new List<string>();
        foreach (Type type in EngineTypes)
            foreach (ConstructorInfo constructor in type.GetConstructors(AllDeclared))
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    if (!parameter.IsOptional) continue;
                    if (!parameter.ParameterType.IsInterface) continue;
                    violations.Add($"{type.FullName}..ctor({parameter.ParameterType.Name} {parameter.Name})");
                }

        EngineCorpus.AssertNone(violations, "L2 — no optional parameter of contract type");
    }

    // ------------------------------------------------------------- L1

    [Fact] // No concrete type is ever a dependency: a collaborator is an interface,
           // supplied at construction, or the module boundary is not real.
    public void L1_NoConstructorTakesAConcreteTypeFromAnotherAssembly()
    {
        var violations = new List<string>();
        foreach (Type type in EngineTypes)
        {
            // Layer 4's exemption, and its ONLY one (design 03 §8): composition
            // exists to name concrete types, so forbidding it to name one would
            // forbid it to do its job. Every other rule in this file still
            // applies to this assembly unchanged.
            if (type.Assembly == EngineCorpus.Composition) continue;

            foreach (ConstructorInfo constructor in type.GetConstructors(AllDeclared))
                foreach (ParameterInfo parameter in constructor.GetParameters())
                {
                    Type parameterType = Unwrap(parameter.ParameterType);
                    if (!IsEngineType(parameterType)) continue;
                    if (parameterType.IsInterface || parameterType.IsEnum || parameterType.IsValueType) continue;
                    if (parameterType.Assembly == type.Assembly) continue;   // same module: allowed
                    if (IsRecordLikeData(parameterType)) continue;           // manifests and results are data

                    violations.Add($"{type.FullName}..ctor takes concrete {parameterType.FullName}");
                }
        }

        EngineCorpus.AssertNone(violations, "L1 — no concrete cross-module dependency");
    }

    // ------------------------------------------------------------- layering

    [Fact] // Dependency direction is strictly downward (design 03 §2). The kernel
           // references nothing of ours; contracts reference only the kernel.
    public void Layering_DependenciesPointDownwardOnly()
    {
        var allowed = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["OGSim.Kernel"] = [],
            ["OGSim.Contracts"] = ["OGSim.Kernel"],

            // Domain modules (design 03 §2/§8). Each references the two layers
            // below it and NEVER a sibling: a module that could name another
            // module's types would make the composition graph a suggestion.
            ["OGSim.Flow"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Subsurface"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Wells"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Facilities"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Operations"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Company"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Information"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.World"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Capabilities"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Integrity"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Persistence"] = ["OGSim.Kernel", "OGSim.Contracts"],
            ["OGSim.Objectives"] = ["OGSim.Kernel", "OGSim.Contracts"],

            // Layer 4 (design 03 §8). The ONE assembly that may reference every
            // module, because it is the one that decides what implements what.
            // Listing the modules explicitly rather than allowing "anything
            // OGSim.*" keeps the direction checkable: composition may reference
            // a module, and a module still may not reference composition.
            ["OGSim.Composition"] =
            [
                "OGSim.Kernel", "OGSim.Contracts",
                "OGSim.Flow", "OGSim.Subsurface", "OGSim.Wells", "OGSim.Facilities",
                "OGSim.Operations", "OGSim.Company", "OGSim.Information", "OGSim.World",
                "OGSim.Capabilities", "OGSim.Integrity", "OGSim.Objectives",
            ],
        };

        var violations = new List<string>();
        foreach (Assembly assembly in EngineCorpus.Assemblies)
        {
            string name = assembly.GetName().Name!;
            if (!allowed.TryGetValue(name, out string[]? permitted))
            {
                violations.Add($"{name} has no declared layer position");
                continue;
            }

            foreach (AssemblyName referenced in assembly.GetReferencedAssemblies())
            {
                string referencedName = referenced.Name!;
                if (!referencedName.StartsWith("OGSim.", StringComparison.Ordinal)) continue;
                if (permitted.Contains(referencedName, StringComparer.Ordinal)) continue;
                violations.Add($"{name} references {referencedName}");
            }
        }

        EngineCorpus.AssertNone(violations, "Layering — dependencies point downward only");
    }

    [Fact] // The engine has no rendering vocabulary at all (design 03 §2): no
           // colours, sprites, scene nodes or screen coordinates.
    public void Layering_NoPresentationConcepts()
    {
        string[] presentation =
            ["Color", "Colour", "Sprite", "Texture", "Bitmap", "Brush", "Pixel",
             "SceneNode", "Viewport", "Screen", "Font", "Icon"];

        var violations = new List<string>();
        foreach (Type type in EngineTypes)
        {
            foreach (string term in presentation)
                if (type.Name.Contains(term, StringComparison.Ordinal))
                    violations.Add($"type {type.FullName}");

            foreach (PropertyInfo property in type.GetProperties(AllDeclared))
                foreach (string term in presentation)
                    if (property.Name.Contains(term, StringComparison.Ordinal))
                        violations.Add($"{type.FullName}.{property.Name}");
        }

        EngineCorpus.AssertNone(violations, "Layer separation — no presentation concepts");
    }

    // ------------------------------------------------------------- events

    [Fact] // EM1 / design 16 §1: notifications never carry control flow. The
           // no-subscriber rule is enforced as an ABSENCE — there is nothing to
           // call, so engine code cannot react to an event even by mistake.
    public void EM1_TheEventBusOffersNoSubscription()
    {
        var violations = new List<string>();
        foreach (MemberInfo member in typeof(IEventBus).GetMembers())
            if (member.Name.Contains("Subscribe", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("Handler", StringComparison.OrdinalIgnoreCase)
                || member.Name.Contains("Notify", StringComparison.OrdinalIgnoreCase))
                violations.Add($"IEventBus.{member.Name}");

        foreach (Type type in EngineTypes)
            foreach (EventInfo declared in type.GetEvents(AllDeclared))
                violations.Add($"{type.FullName} declares CLR event {declared.Name}");

        EngineCorpus.AssertNone(violations, "EM1 — no subscription surface");
    }

    // ------------------------------------------------------------- effects

    [Fact] // Design 07 §1 = 13 §2.1: technology and environment speak ONE closed
           // effect vocabulary, and a bare multiplier is deliberately not in it.
    public void R17V13_TheEffectHierarchyIsSealedAndCarriesNoBareMultiplier()
    {
        Type effect = typeof(Effect);
        Type[] kinds = [.. EngineTypes.Where(t => t.IsSubclassOf(effect))];

        var violations = new List<string>();
        Assert.NotEmpty(kinds);

        foreach (Type kind in kinds)
        {
            if (!kind.IsSealed) violations.Add($"{kind.Name} is not sealed");
            if (kind.Name.Contains("Multiplier", StringComparison.OrdinalIgnoreCase)
                || kind.Name.Contains("Scale", StringComparison.OrdinalIgnoreCase)
                || kind.Name.Contains("Factor", StringComparison.OrdinalIgnoreCase))
                violations.Add($"{kind.Name} reads as a bare multiplier");
        }

        EngineCorpus.AssertNone(violations, "Effect vocabulary is closed");
    }

    // ------------------------------------------------------------- naming

    [Fact] // Glossary rule N3: if a contract name needs Manager/Helper/Service/
           // Handler/Util/Data/Info, the concept behind it is not clear yet.
    public void N3_ContractNamesCarryNoWeaselWords()
    {
        string[] banned = ["Manager", "Helper", "Util", "Service", "Handler", "Data", "Info"];

        var violations = new List<string>();
        foreach (Type type in EngineTypes)
        {
            if (!type.IsInterface) continue;
            foreach (string word in banned)
                if (ContainsAsWord(type.Name, word))
                    violations.Add($"{type.FullName} contains '{word}'");
        }

        EngineCorpus.AssertNone(violations, "N3 — contract names carry no weasel words");
    }

    /// <summary>
    /// The banned word as a WHOLE PascalCase word, not as a substring
    /// (finding 146).
    ///
    /// <para>A substring match reads <c>IInformationValueModel</c> as carrying
    /// "Info" — and "value of information" is the industry's own term, the title
    /// of SDD-008 §7 and the name of a field already on the read model. Glossary
    /// rule N4 says industry terms beat invented ones, so a substring match puts
    /// N3 and N4 in direct conflict and N4 has to win: renaming the concept to
    /// dodge a letter sequence would be the invented name N4 forbids.</para>
    ///
    /// <para>What N3 is actually for is the lazy suffix — <c>IWellInfo</c>,
    /// <c>IPumpData</c>, <c>IFlowService</c> — where the word IS the concept
    /// because no better one was found. Those all still fail: the word stands
    /// alone as a PascalCase component.</para>
    /// </summary>
    private static bool ContainsAsWord(string typeName, string word)
    {
        int at = typeName.IndexOf(word, StringComparison.Ordinal);

        while (at >= 0)
        {
            int after = at + word.Length;

            // A following lowercase letter means the word continues — "Info" in
            // "Information" — so this occurrence is part of a longer word.
            bool endsHere = after == typeName.Length || !char.IsLower(typeName[after]);

            if (endsHere) return true;

            at = typeName.IndexOf(word, at + 1, StringComparison.Ordinal);
        }

        return false;
    }

    /// <summary>
    /// The word-boundary relaxation above must not have opened the door
    /// (finding 146). Every name N3 exists to catch still fails; only a banned
    /// word buried inside a longer real word passes.
    /// </summary>
    [Fact]
    public void N3_StillCatchesTheNamesItExistsFor()
    {
        string[] mustFail =
        [
            "IWellInfo", "IPumpData", "IFlowService", "IRigManager",
            "IStreamHandler", "IUnitHelper", "IMathUtil",
            "IDataStore", "IInfoSource",
        ];

        foreach (string name in mustFail)
            Assert.True(
                ContainsAsWord(name, "Manager") || ContainsAsWord(name, "Helper")
                || ContainsAsWord(name, "Util") || ContainsAsWord(name, "Service")
                || ContainsAsWord(name, "Handler") || ContainsAsWord(name, "Data")
                || ContainsAsWord(name, "Info"),
                $"{name} must still be refused by N3");

        // And the real words that merely CONTAIN a banned sequence pass, because
        // the sequence is not a word there.
        Assert.False(ContainsAsWord("IInformationValueModel", "Info"));
        Assert.False(ContainsAsWord("IDataset", "Data"));
        Assert.False(ContainsAsWord("IServiceable", "Service"));

        // `IServiceRentalLedger` would still FAIL, and correctly: "Service"
        // stands alone there. A rental from a service company is
        // `ServiceRental` — a record, which this rule does not police — and an
        // interface reaching for the same word should be asked whether
        // `IRentalLedger` says it better. This was a bad example on the first
        // pass, and the assertion above caught it.
        Assert.True(ContainsAsWord("IServiceRentalLedger", "Service"));
    }

    [Fact] // SDD-000 §8 rule F-6: identity is EntityId<T>, and there is no second scheme.
           // Contract pass 10 found CompartmentId, PerforationId and OperationId each
           // invented in an SDD, used across sections and declared nowhere — three
           // documents independently reaching for a per-entity id type.
    public void F6_IdentityIsEntityIdAndNothingElse()
    {
        // Each exemption is a DIFFERENT kind of identity, and that is why it is
        // allowed to end in Id:
        //   EntityId<T>  the one entity scheme (SDD-001 §2)
        //   ContentId    content-catalogue identity — a kebab-case string, not a number
        //   TechnologyId a typed wrapper over ContentId; technologies are content
        //   AuditId      a position in an append-only trail, not an entity
        //   EventId      a per-tick publish sequence, not an entity
        //   MaterialId   a catalogue ORDINAL, explicitly never persisted (SDD-004 §6)
        //   PortId       an element-local index, meaningless outside its element
        string[] allowed =
            ["EntityId", "ContentId", "TechnologyId", "AuditId", "EventId", "MaterialId", "PortId"];

        var violations = new List<string>();
        foreach (Type type in EngineTypes)
        {
            if (!type.IsPublic && !type.IsNestedPublic) continue;

            // An ENUM cannot be an identity scheme, whatever it is called: its
            // membership is fixed, finite and named at compile time, and minting
            // a new member at runtime — which is the whole job of an identity —
            // is impossible. StageId (the 14 tick stages) and StreamId (the 8
            // RNG streams) are closed vocabularies that happen to end in "Id".
            if (type.IsEnum) continue;

            string name = type.Name;
            int tick = name.IndexOf('`');           // strip generic arity: EntityId`1
            if (tick >= 0) name = name[..tick];

            if (!name.EndsWith("Id", StringComparison.Ordinal)) continue;
            if (allowed.Contains(name, StringComparer.Ordinal)) continue;

            violations.Add($"{type.FullName} — a second identity scheme; use EntityId<T>");
        }

        EngineCorpus.AssertNone(violations, "F-6 — identity is EntityId<T>");
    }

    /// <summary>
    /// The truth boundary (R5 §2.5, SDD-003 §1/§3). Subsurface truth is
    /// <c>internal</c> and the assembly boundary is what enforces it.
    ///
    /// <para>This is R5's most consequential law and the reason its risk table
    /// says to establish the boundary in R5 rather than in R14. The player's
    /// belief about a reservoir IS the game: if any consumer could read a
    /// compartment's pressure directly, every exploration and appraisal decision
    /// downstream would be theatre. R14 then inherits a boundary that already
    /// holds instead of retrofitting one around code that grew up without it.</para>
    ///
    /// <para>Stated as "no public type at all" rather than as a list of
    /// forbidden members, because the moment there is one public type there is a
    /// door, and the next person to need something through it will widen the
    /// door rather than question the need.</para>
    /// </summary>
    [Fact]
    public void Truth_SubsurfaceExposesNoPublicType()
    {
        var violations = new List<string>();

        foreach (Type type in EngineCorpus.Subsurface.GetTypes())
        {
            if (!type.IsPublic && !type.IsNestedPublic) continue;
            if (type.GetCustomAttribute<CompilerGeneratedAttribute>() is not null) continue;

            violations.Add($"{type.FullName} is public; subsurface truth is internal " +
                           "(SDD-003 §1) and beliefs about it live in OGSim.Information");
        }

        EngineCorpus.AssertNone(violations, "truth boundary — OGSim.Subsurface exposes nothing");
    }

    /// <summary>
    /// THE HOST SURFACE NAMES NO DOMAIN MODULE (design 03 §2, §8).
    ///
    /// <para>A host reads <c>FieldReadModel</c> and nothing else. If a member of
    /// it is typed as something from `OGSim.Company` or `OGSim.Wells`, then
    /// rendering a number means referencing a domain module — which is the one
    /// direction the layering does not allow, and it happens by accident because
    /// the type was already in scope where the read model is assembled.</para>
    ///
    /// <para>This rule exists because it caught nothing. `ReservesEstimate` was
    /// declared in `OGSim.Company` at R20d.13 and put straight onto the read
    /// model; the build was green, every test passed, and the violation was
    /// found by reading the law rather than by running anything. A law enforced
    /// by memory is a law that holds until somebody is tired.</para>
    /// </summary>
    [Fact]
    public void Layering_TheReadModelNamesNoDomainModule()
    {
        Type readModel = EngineCorpus.Composition.GetType("OGSim.Composition.FieldReadModel")
            ?? throw new InvalidOperationException("FieldReadModel is not where it was");

        EngineCorpus.AssertNone(
            DomainLeaks(readModel), "layering — the host surface names no domain module");
    }

    /// <summary>
    /// AND THE RULE ABOVE ACTUALLY CATCHES. A rule that has never failed is a
    /// rule nobody has tested; this hands it the shape it exists to refuse and
    /// checks that it says so, which is the same standard
    /// <see cref="N3_StillCatchesTheNamesItExistsFor"/> holds its own rule to.
    /// </summary>
    [Fact]
    public void Layering_TheReadModelRuleStillCatchesWhatItExistsFor()
    {
        IReadOnlyList<string> caught = DomainLeaks(typeof(SurfaceWithADomainLeak));

        Assert.NotEmpty(caught);
        Assert.Contains(caught, complaint => complaint.Contains("OGSim.Company", StringComparison.Ordinal));
    }

    /// <summary>The exact shape R20d.13 shipped by accident: a read-model member
    /// typed from a domain module.</summary>
    private sealed record SurfaceWithADomainLeak(
        Money Cash, OGSim.Company.MarketState Market);

    private static IReadOnlyList<string> DomainLeaks(Type surface)
    {
        var violations = new List<string>();

        foreach (PropertyInfo member in surface.GetProperties())
        {
            foreach (Type named in Mentioned(member.PropertyType))
            {
                string? assembly = named.Assembly.GetName().Name;

                if (assembly is null) continue;
                if (!assembly.StartsWith("OGSim.", StringComparison.Ordinal)) continue;

                // Contracts and the kernel are BELOW everything, and the
                // composition layer is where a read model legitimately lives.
                if (assembly is "OGSim.Contracts" or "OGSim.Kernel" or "OGSim.Composition")
                    continue;

                violations.Add(
                    $"{surface.Name}.{member.Name} is typed {named.Name} from {assembly}; " +
                    "a host would have to reference a domain module to read it, which is " +
                    "the one direction design 03 §2 does not allow");
            }
        }

        return violations;
    }

    /// <summary>Every type a member's signature mentions, including the ones
    /// inside a generic — a list of the wrong thing is still the wrong
    /// thing.</summary>
    private static IEnumerable<Type> Mentioned(Type type)
    {
        Type named = Unwrap(type);

        yield return named;

        if (!named.IsGenericType) yield break;

        foreach (Type argument in named.GetGenericArguments())
            foreach (Type inner in Mentioned(argument))
                yield return inner;
    }

    // ------------------------------------------------------------- helpers

    private static Type Unwrap(Type type) =>
        type.IsByRef || type.IsArray ? type.GetElementType() ?? type : type;

    private static bool IsEngineType(Type type) =>
        type.Assembly.GetName().Name?.StartsWith("OGSim.", StringComparison.Ordinal) == true;

    /// <summary>Records and manifests are values, not collaborators — passing one
    /// is passing data, which L1 is not about.</summary>
    private static bool IsRecordLikeData(Type type) =>
        type.GetMethod("<Clone>$", AllDeclared) is not null
        || type.GetMethods(AllDeclared).Any(m => m.Name == "op_Equality");
}
