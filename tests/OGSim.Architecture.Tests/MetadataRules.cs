// R1.12 — the rules that are statements about SHAPE, asserted over compiled
// metadata (design 12 §2). Each test is named for the law it enforces, which is
// what makes design 22 §6.1's "every law has a test" check mechanical.

using System.Reflection;
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
                if (type.Name.Contains(word, StringComparison.Ordinal))
                    violations.Add($"{type.FullName} contains '{word}'");
        }

        EngineCorpus.AssertNone(violations, "N3 — contract names carry no weasel words");
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
