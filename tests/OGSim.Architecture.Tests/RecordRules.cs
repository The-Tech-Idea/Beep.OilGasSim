// Finding 212 — the rules that are statements about the RECORD rather than the
// code. Every other rule in this suite asserts on the engine; these assert that
// the design set describes the engine consistently with itself.
//
// They live here because the register they guard belongs to no module — it is a
// consolidation of eighteen SDDs into one page of MASTER_TRACKER.md, and a rule
// about a cross-cutting document has no other home. They are also the only rules
// in this suite that read files the compiler never sees, so the usual preference
// for metadata over text (EngineCorpus's header) cannot apply: markdown has no
// metadata form.
//
// WHAT THEY ARE FOR. SDD-013's open items were closed eight at a time during
// R20d.12 and the consolidated register was never updated, so a page whose
// entire purpose is that nothing is missing from it was missing nine items —
// including S013-10, "the largest gap left", which had in fact been BUILT and
// still read as outstanding. Consolidation is a mechanism with two halves and
// only the first was ever performed. Prose cannot enforce the second.
//
// WHAT THEY CANNOT DO, which is the more useful half to write down. S013-6 was
// invisible to every count — mine included — because its row had lost the
// newline joining it to S013-5, so two items rendered and parsed as one. A
// completeness rule compares two lists; it cannot notice an item absent from
// BOTH. That is why WellFormed asserts the shape of every row rather than
// trusting it: the shape check is the only one of these rules that can see a
// defect the register and the SDDs agree about.

namespace OGSim.Architecture.Tests;

public class RecordRules
{
    /// <summary>An open item as its SDD declares it. <paramref name="Status"/> is
    /// the last cell: <c>✅</c> means closed, anything else means open. It is the
    /// ONE place status lives — it used to be written three ways (a struck-through
    /// body, the word "closed", a tick) with one item declaring it in prose only,
    /// and no single reading of the table was correct.</summary>
    private readonly record struct OpenItem(string Id, string Status, string Source, int Line)
    {
        public bool IsClosed => Status == Closed;
    }

    private const string Closed = "✅";
    private const string RegisterHeading = "## Open items";
    private const string ClosedTableHeading = "**Closed by work already done**";

    // --------------------------------------------------------------- shape

    [Fact] // The rule the other three rest on. A malformed row does not merely read
           // badly — it merges with its neighbour and the neighbour STOPS EXISTING
           // for every reader, so no comparison of lists can find it.
    public void Record_EveryOpenItemRowIsWellFormed()
    {
        var violations = new List<string>();
        foreach ((string path, string[] lines) in SddFiles())
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (!LooksLikeItemRow(line)) continue;

                string[] cells = line.Split('|');
                if (cells.Length != 5)
                    violations.Add(
                        $"{Path.GetFileName(path)}:{i + 1} — a row with {cells.Length} cells " +
                        $"instead of 5. A second '| Sxxx-N |' on one line means two items " +
                        $"parse as one and the later one is invisible: {Excerpt(line)}");
            }
        }

        EngineCorpus.AssertNone(violations,
            "Every open-item row carries exactly one item and one status cell");
    }

    // -------------------------------------------------------- completeness

    [Fact] // Consolidation's second half. Raising an item in an SDD and never
           // registering it leaves "what is undecided?" answerable only by reading
           // all eighteen files, which is the thing the register exists to end.
    public void Record_EveryOpenItemRaisedInAnSddIsRegisteredHere()
    {
        HashSet<string> register = IdsIn(RegisterSection());
        var violations = new List<string>();

        foreach (OpenItem item in Items())
            if (!register.Contains(item.Id))
                violations.Add(
                    $"{item.Id} is raised in {item.Source}:{item.Line} and appears nowhere in " +
                    $"the register. Add it to MASTER_TRACKER.md's '{RegisterHeading}' section");

        EngineCorpus.AssertNone(violations,
            "Every open item raised in an SDD is registered in MASTER_TRACKER.md");
    }

    [Fact] // Closure propagates in BOTH directions. An item closed at source and
           // still listed open advertises work as outstanding that has shipped;
           // an item listed closed and still open at source does the reverse.
    public void Record_ClosureAgreesBetweenTheSddAndTheRegister()
    {
        HashSet<string> closedTable = IdsIn(ClosedTable());
        var violations = new List<string>();

        foreach (OpenItem item in Items())
        {
            bool listedClosed = closedTable.Contains(item.Id);

            if (item.IsClosed && !listedClosed)
                violations.Add(
                    $"{item.Id} is closed in {item.Source}:{item.Line} but is not in the " +
                    $"register's '{ClosedTableHeading}' table — the register still advertises " +
                    $"it as outstanding work");

            if (!item.IsClosed && listedClosed)
                violations.Add(
                    $"{item.Id} is listed closed in the register but its status cell in " +
                    $"{item.Source}:{item.Line} reads '{item.Status}' rather than '{Closed}'");
        }

        EngineCorpus.AssertNone(violations,
            "An item is closed in its SDD exactly when the register says so");
    }

    // --------------------------------------------------------------- count

    [Fact] // The counts in the header were hand-written prose for the whole life
           // of the document and were wrong every way at once: 51 open against a
           // section claiming 49 against tables holding 29.
           //
           // BLIND SPOT, stated because the failure message names three places to
           // update and this checks ONE. The register's opening sentence spells
           // its counts as WORDS ("Sixty-two are raised … fourteen closed"), so it
           // can drift while this stays green. Parsing English numerals to close
           // that would be a second, looser reading of the same fact — the thing
           // finding 212 is about — so the sentence is left to the reader and the
           // gap is written down instead of papered over.
    public void Record_TheHeadlineCountsMatchWhatTheSddsActuallyRaise()
    {
        OpenItem[] items = [.. Items()];
        int raised = items.Length;
        int open = items.Count(i => !i.IsClosed);

        const string anchor = "open SDD items of";
        string claim = Tracker().First(l => l.Contains(anchor, StringComparison.Ordinal));
        (int claimedOpen, int claimedRaised) = CountsAround(claim, anchor);

        Assert.True(claimedOpen == open && claimedRaised == raised,
            $"The tracker's headline says {claimedOpen} open of {claimedRaised} raised; the " +
            $"SDDs raise {raised} and leave {open} open. Update the header row, the register's " +
            $"opening sentence and this count together — they are three statements of one fact");
    }

    // -------------------------------------------------- verification ids

    [Fact] // A verification id in a test name is a CLAIM that the design set asks
           // for this check. The convention exists so the mapping can be trusted,
           // which makes an id pointing at nothing worse than no id at all —
           // finding 209 caught three tests carrying the WRONG one, and finding
           // 213 caught 32 carrying ids no document declared.
    public void Record_EveryVerificationIdInATestNameIsDeclared()
    {
        HashSet<string> declared = DeclaredVerificationIds();
        var violations = new List<string>();

        foreach ((string path, string[] lines) in TestFiles())
            for (int i = 0; i < lines.Length; i++)
            {
                string? id = VerificationIdIn(lines[i]);

                if (id is null) continue;

                // Declared as `R20d-V1`; written in a method name as `R20dV1`,
                // because a hyphen is not an identifier character. The hyphen is
                // put back rather than searched for both ways — one spelling of
                // one rule (finding 212's lesson about three status markers).
                string cited = Hyphenate(id);

                if (!declared.Contains(cited))
                    violations.Add(
                        $"{Path.GetFileName(path)}:{i + 1} cites {cited}, which no " +
                        $"document in plans/ declares. Either the phase never wrote its " +
                        $"verification table, or the id is a transcription of another " +
                        $"one — both make the test report a coverage that does not exist");
            }

        EngineCorpus.AssertNone(violations,
            "Every verification id a test name claims is declared in the design set");
    }

    /// <summary>The id a test method name opens with, or null. Only the
    /// PHASE-PREFIXED family is judged (<c>R20dV1</c>, <c>R24V18</c>): the domain
    /// suites — FV, MX, MB, PV, IR, CAL — are declared in design documents under
    /// their own conventions, and treating one namespace's spelling as the rule
    /// for all of them is what produced <c>R24V4</c> in the first place.</summary>
    private static string? VerificationIdIn(string line)
    {
        const string marker = "public void R";

        int at = line.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0) return null;

        int start = at + marker.Length - 1;          // the 'R'
        int i = start + 1;

        while (i < line.Length && char.IsAsciiDigit(line[i])) i++;
        if (i == start + 1) return null;             // no phase number

        while (i < line.Length && char.IsAsciiLetterLower(line[i])) i++;   // R20d, R12b
        if (i >= line.Length || line[i] != 'V') return null;

        int digits = ++i;
        while (i < line.Length && char.IsAsciiDigit(line[i])) i++;

        return i == digits ? null : line[start..i];
    }

    /// <summary><c>R20dV1</c> becomes <c>R20d-V1</c>.</summary>
    private static string Hyphenate(string id)
    {
        int v = id.LastIndexOf('V');

        return id[..v] + "-" + id[v..];
    }

    /// <summary>
    /// Every id the design set DECLARES, read as the opening cell of a table row
    /// — <c>| R6-V14 | Commingling backpressure | … |</c> — which is how every
    /// phase document states one.
    ///
    /// <para>A prose mention deliberately does NOT count. The first version of
    /// this rule searched the concatenated documents for the id as a substring,
    /// and two things were wrong with it at once: <c>R20d-V1</c> matched inside
    /// <c>R20d-V13</c>, the same prefix trap as finding 212's `S013-1`; and one
    /// row's sentence *"the refusal in R20d-V1 is a decision rather than a dead
    /// end"* was enough to declare V1, so a cross-reference could satisfy the
    /// check for an id nothing defined. A declaration is a row, so the rule reads
    /// rows.</para>
    /// </summary>
    private static HashSet<string> DeclaredVerificationIds()
    {
        var found = new HashSet<string>(StringComparer.Ordinal);

        foreach (string path in Directory.EnumerateFiles(
            Plans(), "*.md", SearchOption.AllDirectories))
            foreach (string line in File.ReadAllLines(path))
            {
                if (!line.StartsWith("| ", StringComparison.Ordinal)) continue;

                int end = line.IndexOf('|', 1);
                if (end < 0) continue;

                string cell = line[1..end].Trim();

                if (cell.Length > 0 && cell[0] == 'R' && cell.Contains("-V", StringComparison.Ordinal))
                    found.Add(cell);
            }

        return found;
    }

    private static IEnumerable<(string Path, string[] Lines)> TestFiles()
    {
        string tests = Path.Combine(EngineCorpus.RepositoryRoot(), "tests");

        foreach (string path in Directory.EnumerateFiles(tests, "*.cs", SearchOption.AllDirectories)
                                         .OrderBy(p => p, StringComparer.Ordinal))
        {
            string normalised = path.Replace('\\', '/');
            if (normalised.Contains("/bin/") || normalised.Contains("/obj/")) continue;

            yield return (path, File.ReadAllLines(path));
        }
    }

    // ---------------------------------------------------------------- read

    private static IEnumerable<OpenItem> Items()
    {
        foreach ((string path, string[] lines) in SddFiles())
            for (int i = 0; i < lines.Length; i++)
            {
                if (!LooksLikeItemRow(lines[i])) continue;

                string[] cells = lines[i].Split('|');
                if (cells.Length != 5) continue;   // reported by the shape rule

                yield return new OpenItem(
                    cells[1].Trim(), cells[3].Trim(), Path.GetFileName(path), i + 1);
            }
    }

    /// <summary>A row opening an item: <c>| Sxxx-N |</c>. Deliberately hand-parsed
    /// rather than pattern-matched — the shape it accepts is the shape the count
    /// commands printed in the register accept, and two spellings of one rule is
    /// how the three status markers happened.</summary>
    private static bool LooksLikeItemRow(string line)
    {
        if (!line.StartsWith("| S", StringComparison.Ordinal)) return false;

        int at = 3;
        int digits = 0;
        while (at < line.Length && char.IsAsciiDigit(line[at])) { at++; digits++; }
        if (digits != 3 || at >= line.Length || line[at] != '-') return false;

        at++;
        digits = 0;
        while (at < line.Length && char.IsAsciiDigit(line[at])) { at++; digits++; }
        return digits > 0 && at < line.Length && line[at] == ' ';
    }

    /// <summary>Every id in a span of text, matched to its full length so that
    /// <c>S013-1</c> does not match inside <c>S013-10</c>. A plain
    /// <c>Contains</c> here made the completeness rule PASS while reading a
    /// register that was missing the item — the failure mode this whole file
    /// exists to catch, reproduced inside the check for it.</summary>
    private static HashSet<string> IdsIn(string text)
    {
        var found = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i + 1 < text.Length; i++)
        {
            if (text[i] != 'S' || !char.IsAsciiDigit(text[i + 1])) continue;

            int at = i + 1;
            while (at < text.Length && char.IsAsciiDigit(text[at])) at++;
            if (at - (i + 1) != 3 || at >= text.Length || text[at] != '-') continue;

            int digits = ++at;
            while (at < text.Length && char.IsAsciiDigit(text[at])) at++;
            if (at == digits) continue;

            found.Add(text[i..at]);
            i = at - 1;
        }

        return found;
    }

    /// <summary>The two counts written either side of <paramref name="anchor"/>.
    /// Anchored rather than "the first two numbers in the line", because the row
    /// this reads opens "24 design + 1 research" and those are the numbers a
    /// positional parser finds.</summary>
    private static (int Before, int After) CountsAround(string line, string anchor)
    {
        int at = line.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(at >= 0, $"Expected '{anchor}' in: {Excerpt(line)}");

        return (LastNumberIn(line[..at]), FirstNumberIn(line[(at + anchor.Length)..]));
    }

    private static int LastNumberIn(string text)
    {
        int end = text.Length;
        while (end > 0 && !char.IsAsciiDigit(text[end - 1])) end--;

        int start = end;
        while (start > 0 && char.IsAsciiDigit(text[start - 1])) start--;

        Assert.True(end > start, $"Expected a count before the anchor in: {Excerpt(text)}");
        return int.Parse(text[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static int FirstNumberIn(string text)
    {
        int start = 0;
        while (start < text.Length && !char.IsAsciiDigit(text[start])) start++;

        int end = start;
        while (end < text.Length && char.IsAsciiDigit(text[end])) end++;

        Assert.True(end > start, $"Expected a count after the anchor in: {Excerpt(text)}");
        return int.Parse(text[start..end], System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string RegisterSection()
    {
        string[] lines = Tracker();
        int start = IndexOfLineStarting(lines, RegisterHeading);

        int end = start + 1;
        while (end < lines.Length && lines[end] != "---") end++;

        return string.Join('\n', lines[start..end]);
    }

    private static string ClosedTable()
    {
        string[] lines = Tracker();
        int start = IndexOfLineStarting(lines, ClosedTableHeading);

        int end = start + 1;
        while (end < lines.Length && !lines[end].StartsWith("**", StringComparison.Ordinal)) end++;

        return string.Join('\n', lines[start..end]);
    }

    private static int IndexOfLineStarting(string[] lines, string prefix)
    {
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith(prefix, StringComparison.Ordinal)) return i;

        throw new InvalidOperationException(
            $"MASTER_TRACKER.md has no line starting '{prefix}'. These rules read the register " +
            $"by its headings; renaming one silently narrows what they cover, so this refuses " +
            $"rather than passing over a section it cannot find");
    }

    private static string[] Tracker() =>
        File.ReadAllLines(Path.Combine(Plans(), "MASTER_TRACKER.md"));

    private static IEnumerable<(string Path, string[] Lines)> SddFiles()
    {
        foreach (string path in Directory.EnumerateFiles(Path.Combine(Plans(), "sdd"), "SDD-*.md")
                                         .OrderBy(p => p, StringComparer.Ordinal))
            yield return (path, File.ReadAllLines(path));
    }

    /// <summary><see cref="EngineCorpus.RepositoryRoot"/> anchors on its CALLER's
    /// compile-time path, so this resolves correctly only because this file sits
    /// beside EngineCorpus.cs. Moving it into a subdirectory would silently walk
    /// up from the wrong place and these rules would read a tree that is not the
    /// repository — so the directory is checked rather than assumed.</summary>
    private static string Plans()
    {
        string plans = Path.Combine(EngineCorpus.RepositoryRoot(), "plans");

        Assert.True(Directory.Exists(plans),
            $"Resolved the design set to '{plans}', which does not exist. RepositoryRoot() " +
            $"walks up two directories from the calling FILE, so these rules require " +
            $"RecordRules.cs to stay beside EngineCorpus.cs");

        return plans;
    }

    private static string Excerpt(string line) =>
        line.Length <= 90 ? line : line[..90] + "…";
}
