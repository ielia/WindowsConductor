using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class WagnerFischerTests
{
    [Test]
    public void ExactMatch_Distance0()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("Hello World", "World");
        Assert.That(dist, Is.EqualTo(0));
        Assert.That("Hello World"[start..end], Is.EqualTo("World"));
    }

    [Test]
    public void ExactMatch_CaseInsensitive()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("Hello WORLD", "world");
        Assert.That(dist, Is.EqualTo(0));
        Assert.That(start, Is.EqualTo(6));
        Assert.That(end, Is.EqualTo(11));
    }

    [Test]
    public void SingleEdit_Substitution()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("H3llo", "Hello");
        Assert.That(dist, Is.EqualTo(1));
        Assert.That("H3llo"[start..end], Is.EqualTo("H3llo"));
    }

    [Test]
    public void SingleEdit_Insertion()
    {
        var (dist, _, _) = WcElementOcrText.FindBestSubstring("Hllo World", "Hello");
        Assert.That(dist, Is.EqualTo(1));
    }

    [Test]
    public void SingleEdit_Deletion()
    {
        var (dist, _, _) = WcElementOcrText.FindBestSubstring("Heello World", "Hello");
        Assert.That(dist, Is.EqualTo(1));
    }

    [Test]
    public void EmptyNeedle_Distance0()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("Hello", "");
        Assert.That(dist, Is.EqualTo(0));
        Assert.That(start, Is.EqualTo(0));
        Assert.That(end, Is.EqualTo(0));
    }

    [Test]
    public void EmptyHaystack_DistanceEqualsNeedleLength()
    {
        var (dist, _, _) = WcElementOcrText.FindBestSubstring("", "abc");
        Assert.That(dist, Is.EqualTo(3));
    }

    [Test]
    public void BestSubstring_PicksShortest()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("abcdef", "cd");
        Assert.That(dist, Is.EqualTo(0));
        Assert.That("abcdef"[start..end], Is.EqualTo("cd"));
    }

    [Test]
    public void FuzzySubstring_InMiddle()
    {
        var (dist, start, end) = WcElementOcrText.FindBestSubstring("xxFoxxx", "Foo");
        Assert.That(dist, Is.EqualTo(1));
        // "Fo" (distance 1: missing 'o') is as good as "Fox" (distance 1: x→o); W-F picks first shortest
        Assert.That(start, Is.GreaterThanOrEqualTo(2));
        Assert.That(end, Is.LessThanOrEqualTo(5));
    }
}

[TestFixture]
[Category("Unit")]
public class OcrFindBestByEditsTests
{
    private static readonly FakeTransport Transport = new();
    private static readonly WcElement FakeElement = new("el1", Transport);

    private static readonly BoundingRect RectA = new(0, 0, 50, 20);
    private static readonly BoundingRect RectB = new(55, 0, 60, 20);
    private static readonly BoundingRect RectC = new(120, 0, 40, 20);
    private static readonly BoundingRect RectD = new(165, 0, 35, 20);

    private static readonly BoundingRect LineRect1 = new(0, 0, 160, 20);
    private static readonly BoundingRect LineRect2 = new(0, 25, 160, 20);
    private static readonly BoundingRect LineRect3 = new(0, 50, 160, 20);
    private static readonly BoundingRect LineRect4 = new(0, 75, 160, 20);

    private static readonly BoundingRect ResultRect = new(0, 0, 200, 100);

    private static WcElementOcrWord Word(string text, BoundingRect? box = null) =>
        new(FakeElement, box ?? RectA, null, text);

    private static WcElementOcrLine Line(string text, BoundingRect? box = null, params WcElementOcrWord[] words) =>
        new(FakeElement, box ?? LineRect1, text, null, words);

    private static WcElementOcrResult Result(string text, BoundingRect? box = null, params WcElementOcrLine[] lines) =>
        new(FakeElement, box ?? ResultRect, text, null, lines);

    private static BoundingRect ExpectedUnion(params WcElementOcrText[] fragments) =>
        WcElementOcrText.UnionRect(fragments);

    private static void AssertMatchRect(WcElementOcrMatch match)
    {
        var expected = ExpectedUnion(match.Fragments.ToArray());
        Assert.That(match.BoundingRect, Is.EqualTo(expected),
            "Match BoundingRect should be the union of all fragment BoundingRects");
    }

    private static void AssertSliceUsesWordRect(WcElementOcrWordSlice slice)
    {
        Assert.That(slice.BoundingRect, Is.EqualTo(slice.OriginalWord.BoundingRect),
            "Slice BoundingRect should equal the OriginalWord's BoundingRect");
    }

    // ── Null returns ────────────────────────────────────────────────────────

    [Test]
    public void Word_NoMatch_ReturnsNull()
    {
        var word = Word("Hello");
        Assert.That(word.FindBestByEdits("xyz", 0), Is.Null);
    }

    [Test]
    public void Word_ExceedsMaxEdits_ReturnsNull()
    {
        var word = Word("Hello");
        Assert.That(word.FindBestByEdits("xyz", 1), Is.Null);
    }

    [Test]
    public void EmptyText_ReturnsNull()
    {
        var word = Word("");
        Assert.That(word.FindBestByEdits("abc"), Is.Null);
    }

    [Test]
    public void EmptySearch_ReturnsNull()
    {
        var word = Word("Hello");
        Assert.That(word.FindBestByEdits(""), Is.Null);
    }

    // ── Word-level matches ──────────────────────────────────────────────────

    [Test]
    public void Word_ExactMatch_ReturnsSelf()
    {
        var word = Word("Hello", RectA);
        var match = word.FindBestByEdits("Hello");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(1));
        Assert.That(match.Fragments[0], Is.SameAs(word));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectA));
    }

    [Test]
    public void Word_ExactSubstring_ReturnsSlice()
    {
        var word = Word("Hello", RectA);
        var match = word.FindBestByEdits("ell");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(1));
        var slice = match.Fragments[0] as WcElementOcrWordSlice;
        Assert.That(slice, Is.Not.Null);
        Assert.That(slice!.Text, Is.EqualTo("ell"));
        Assert.That(slice.FromIndex, Is.EqualTo(1));
        Assert.That(slice.ToIndex, Is.EqualTo(4));
        Assert.That(slice.OriginalWord, Is.SameAs(word));
        AssertSliceUsesWordRect(slice);
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectA));
    }

    [Test]
    public void Word_SubstringWithBudget_ExpandsToWholeWord()
    {
        var word = Word("Hello", RectA);
        var match = word.FindBestByEdits("ell", maxEdits: 2);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(1));
        Assert.That(match.Fragments[0], Is.SameAs(word));
        Assert.That(match.Text, Is.EqualTo("Hello"));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectA));
    }

    [Test]
    public void Word_SubstringWithInsufficientBudget_ReturnsSlice()
    {
        var word = Word("Hello", RectA);
        var match = word.FindBestByEdits("ell", maxEdits: 1);
        Assert.That(match, Is.Not.Null);
        var slice = match!.Fragments[0] as WcElementOcrWordSlice;
        Assert.That(slice, Is.Not.Null);
        Assert.That(slice!.Text, Is.EqualTo("ell"));
        AssertSliceUsesWordRect(slice);
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectA));
    }

    // ── Line-level matches ──────────────────────────────────────────────────

    [Test]
    public void Line_MatchSingleWord_ReturnsWord()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var line = Line("Hello World", LineRect1, w1, w2);
        var match = line.FindBestByEdits("World");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(1));
        Assert.That(match.Fragments[0], Is.SameAs(w2));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectB));
    }

    [Test]
    public void Line_MatchAcrossWords_ReturnsSliceAndWord()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var line = Line("Hello World", LineRect1, w1, w2);
        var match = line.FindBestByEdits("lo World");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(2));
        Assert.That(match.Fragments[0], Is.InstanceOf<WcElementOcrWordSlice>());
        Assert.That(match.Fragments[0].Text, Is.EqualTo("lo"));
        AssertSliceUsesWordRect((WcElementOcrWordSlice)match.Fragments[0]);
        Assert.That(match.Fragments[1], Is.SameAs(w2));
        AssertMatchRect(match);
        // Union of RectA (slice uses word rect) and RectB
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w1, w2)));
    }

    [Test]
    public void Line_BudgetExpandsBothBoundaries()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var line = Line("Hello World", LineRect1, w1, w2);
        var match = line.FindBestByEdits("lo Worl", maxEdits: 4);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Fragments, Has.Count.EqualTo(2));
        Assert.That(match.Fragments[0], Is.SameAs(w1));
        Assert.That(match.Fragments[1], Is.SameAs(w2));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w1, w2)));
    }

    [Test]
    public void Line_UserExample_MaxEdits2()
    {
        var w1 = Word("H3llo", RectA);
        var w2 = Word("Cruel", RectB);
        var w3 = Word("Wrldd", RectC);
        var line = Line("H3llo Cruel Wrldd", LineRect1, w1, w2, w3);
        var match = line.FindBestByEdits("low cruel world", maxEdits: 2);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(2));
        Assert.That(match.Fragments, Has.Count.EqualTo(3));
        Assert.That(match.Fragments[0], Is.InstanceOf<WcElementOcrWordSlice>());
        Assert.That(match.Fragments[0].Text, Is.EqualTo("lo"));
        AssertSliceUsesWordRect((WcElementOcrWordSlice)match.Fragments[0]);
        Assert.That(match.Fragments[1], Is.SameAs(w2));
        Assert.That(match.Fragments[2], Is.InstanceOf<WcElementOcrWordSlice>());
        Assert.That(match.Fragments[2].Text, Is.EqualTo("Wrld"));
        AssertSliceUsesWordRect((WcElementOcrWordSlice)match.Fragments[2]);
        AssertMatchRect(match);
        // Slices use word rects, so union is RectA ∪ RectB ∪ RectC
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w1, w2, w3)));
    }

    [Test]
    public void Line_UserExample_MaxEdits3to5_KeepsCheaperBoundaryWhole()
    {
        var w1 = Word("H3llo", RectA);
        var w2 = Word("Cruel", RectB);
        var w3 = Word("Wrldd", RectC);
        var line = Line("H3llo Cruel Wrldd", LineRect1, w1, w2, w3);

        for (int maxEdits = 3; maxEdits <= 5; maxEdits++)
        {
            var match = line.FindBestByEdits("low cruel world", maxEdits: maxEdits);
            Assert.That(match, Is.Not.Null, $"maxEdits={maxEdits}");
            Assert.That(match!.Fragments[0], Is.InstanceOf<WcElementOcrWordSlice>(),
                $"maxEdits={maxEdits}: left boundary should be sliced");
            AssertSliceUsesWordRect((WcElementOcrWordSlice)match.Fragments[0]);
            Assert.That(match.Fragments[1], Is.SameAs(w2), $"maxEdits={maxEdits}");
            Assert.That(match.Fragments[2], Is.SameAs(w3),
                $"maxEdits={maxEdits}: right boundary should be kept whole");
            AssertMatchRect(match);
            Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w1, w2, w3)),
                $"maxEdits={maxEdits}: rect should be union of all fragment rects");
        }
    }

    [Test]
    public void Line_UserExample_MaxEdits6Plus_KeepsAllWhole()
    {
        var w1 = Word("H3llo", RectA);
        var w2 = Word("Cruel", RectB);
        var w3 = Word("Wrldd", RectC);
        var line = Line("H3llo Cruel Wrldd", LineRect1, w1, w2, w3);
        var match = line.FindBestByEdits("low cruel world", maxEdits: 6);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Fragments, Has.Count.EqualTo(3));
        Assert.That(match.Fragments[0], Is.SameAs(w1));
        Assert.That(match.Fragments[1], Is.SameAs(w2));
        Assert.That(match.Fragments[2], Is.SameAs(w3));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w1, w2, w3)));
    }

    // ── Result-level matches ────────────────────────────────────────────────

    [Test]
    public void Result_MatchWithinSingleLine_ReturnsWordFragments()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var w3 = Word("Foo", RectC);
        var line1 = Line("Hello World", LineRect1, w1, w2);
        var line2 = Line("Foo", LineRect2, w3);
        var result = Result("Hello World Foo", ResultRect, line1, line2);

        var match = result.FindBestByEdits("World");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Fragments, Has.Count.EqualTo(1));
        Assert.That(match.Fragments[0], Is.SameAs(w2));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectB));
    }

    [Test]
    public void Result_MatchSpanningLines_BudgetKeepsLinesWhole()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var w3 = Word("Foo", RectC);
        var w4 = Word("Bar", RectD);
        var line1 = Line("Hello World", LineRect1, w1, w2);
        var line2 = Line("Foo Bar", LineRect2, w3, w4);
        var result = Result("Hello World Foo Bar", ResultRect, line1, line2);

        var match = result.FindBestByEdits("World Foo", maxEdits: 10);
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(2));
        Assert.That(match.Fragments[0], Is.SameAs(line1));
        Assert.That(match.Fragments[1], Is.SameAs(line2));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(line1, line2)));
    }

    [Test]
    public void Result_MatchSpanningLines_NoBudget_DrillsIntoBothLines()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var w3 = Word("Foo", RectC);
        var w4 = Word("Bar", RectD);
        var line1 = Line("Hello World", LineRect1, w1, w2);
        var line2 = Line("Foo Bar", LineRect2, w3, w4);
        var result = Result("Hello World Foo Bar", ResultRect, line1, line2);

        var match = result.FindBestByEdits("World Foo");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(2));
        Assert.That(match.Fragments[0], Is.SameAs(w2));
        Assert.That(match.Fragments[1], Is.SameAs(w3));
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w2, w3)));
    }

    [Test]
    public void Result_MatchSpanningLines_DrillsIntoBothLinesAndSlicesWords()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var w3 = Word("Foo", new BoundingRect(0, 25, 30, 20));
        var w4 = Word("Bar", new BoundingRect(35, 25, 30, 20));
        var w5 = Word("Alpha", new BoundingRect(0, 50, 50, 20));
        var w6 = Word("Beta", new BoundingRect(55, 50, 40, 20));
        var w7 = Word("Gamma", new BoundingRect(0, 75, 50, 20));
        var w8 = Word("Delta", new BoundingRect(55, 75, 45, 20));
        var line1 = Line("Hello World", LineRect1, w1, w2);
        var line2 = Line("Foo Bar", LineRect2, w3, w4);
        var line3 = Line("Alpha Beta", LineRect3, w5, w6);
        var line4 = Line("Gamma Delta", LineRect4, w7, w8);
        var result = Result("Hello World Foo Bar Alpha Beta Gamma Delta", ResultRect, line1, line2, line3, line4);

        var match = result.FindBestByEdits("rld foo bar alpha be");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Distance, Is.EqualTo(0));
        Assert.That(match.Fragments, Has.Count.EqualTo(4));

        var slice0 = (WcElementOcrWordSlice)match.Fragments[0];
        Assert.That(slice0.Text, Is.EqualTo("rld"));
        Assert.That(slice0.OriginalWord, Is.SameAs(w2));
        AssertSliceUsesWordRect(slice0);

        Assert.That(match.Fragments[1], Is.SameAs(line2));
        Assert.That(match.Fragments[2], Is.SameAs(w5));

        var slice3 = (WcElementOcrWordSlice)match.Fragments[3];
        Assert.That(slice3.Text, Is.EqualTo("Be"));
        Assert.That(slice3.OriginalWord, Is.SameAs(w6));
        AssertSliceUsesWordRect(slice3);

        AssertMatchRect(match);
        // Union of w2's rect (via slice), line2's rect, w5's rect, w6's rect (via slice)
        Assert.That(match.BoundingRect, Is.EqualTo(ExpectedUnion(w2, line2, w5, w6)));
    }

    // ── Slice re-matching ───────────────────────────────────────────────────

    [Test]
    public void Slice_FindBestByEdits_ReturnsNarrowerSlice()
    {
        var word = Word("Hello", RectA);
        var slice = new WcElementOcrWordSlice(FakeElement, RectA, null, "ello", word, 1, 5);
        var match = slice.FindBestByEdits("ell");
        Assert.That(match, Is.Not.Null);
        Assert.That(match!.Fragments, Has.Count.EqualTo(1));
        var innerSlice = match.Fragments[0] as WcElementOcrWordSlice;
        Assert.That(innerSlice, Is.Not.Null);
        Assert.That(innerSlice!.Text, Is.EqualTo("ell"));
        Assert.That(innerSlice.OriginalWord, Is.SameAs(word));
        Assert.That(innerSlice.FromIndex, Is.EqualTo(1));
        Assert.That(innerSlice.ToIndex, Is.EqualTo(4));
        AssertSliceUsesWordRect(innerSlice);
        AssertMatchRect(match);
        Assert.That(match.BoundingRect, Is.EqualTo(RectA));
    }

    // ── Match re-matching ───────────────────────────────────────────────────

    [Test]
    public void Match_FindBestByEdits_SearchesWithinMatch()
    {
        var w1 = Word("Hello", RectA);
        var w2 = Word("World", RectB);
        var matchRect = ExpectedUnion(w1, w2);
        var line = Line("Hello World", LineRect1, w1, w2);
        var ocrMatch = new WcElementOcrMatch(FakeElement, matchRect, "Hello World", null, line, 0, 11, [w1, w2], 0);
        var inner = ocrMatch.FindBestByEdits("World");
        Assert.That(inner, Is.Not.Null);
        Assert.That(inner!.Fragments, Has.Count.EqualTo(1));
        Assert.That(inner.Fragments[0], Is.SameAs(w2));
        AssertMatchRect(inner);
        Assert.That(inner.BoundingRect, Is.EqualTo(RectB));
    }
}

[TestFixture]
[Category("Unit")]
public class WagnerFischerFindAllTests
{
    [Test]
    public void ExactMatches_MultipleNonOverlapping()
    {
        var results = WcElementOcrText.FindAllSubstrings("xxabcxxabcxx", "abc", 0);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0], Is.EqualTo((0, 2, 5)));
        Assert.That(results[1], Is.EqualTo((0, 7, 10)));
    }

    [Test]
    public void UserExample_PicksBestOnOverlap()
    {
        // "xxabbabcxxababcbcxx", needle "abc", maxEdits 2
        // The backtrace finds "cbc" at 14-17 (dist=1) for the endpoint at 17, which overlaps
        // with "abc" at 12-15 (dist=0). Since the exact match wins, "cbc" is discarded.
        const string haystack = "xxabbabcxxababcbcxx";
        var results = WcElementOcrText.FindAllSubstrings(haystack, "abc", 2);
        Assert.That(results, Has.Count.EqualTo(4));
        Assert.That(results[0], Is.EqualTo((1, 2, 4)));
        Assert.That(haystack[results[0].Start..results[0].End], Is.EqualTo("ab"));
        Assert.That(results[1], Is.EqualTo((0, 5, 8)));
        Assert.That(haystack[results[1].Start..results[1].End], Is.EqualTo("abc"));
        Assert.That(results[2], Is.EqualTo((1, 10, 12)));
        Assert.That(haystack[results[2].Start..results[2].End], Is.EqualTo("ab"));
        Assert.That(results[3], Is.EqualTo((0, 12, 15)));
        Assert.That(haystack[results[3].Start..results[3].End], Is.EqualTo("abc"));
        // Verify ordered by position and non-overlapping
        for (int i = 1; i < results.Count; i++)
            Assert.That(results[i].Start, Is.GreaterThanOrEqualTo(results[i - 1].End),
                $"Match {i} overlaps with match {i - 1}");
    }

    [Test]
    public void NoMatches_ReturnsEmpty()
    {
        var results = WcElementOcrText.FindAllSubstrings("xxxxxxxx", "abc", 0);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void EmptyNeedle_ReturnsSingleMatch()
    {
        var results = WcElementOcrText.FindAllSubstrings("abc", "", 0);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Distance, Is.EqualTo(0));
    }

    [Test]
    public void EmptyHaystack_WithinMaxEdits_ReturnsSingleMatch()
    {
        var results = WcElementOcrText.FindAllSubstrings("", "ab", 2);
        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Distance, Is.EqualTo(2));
    }

    [Test]
    public void EmptyHaystack_ExceedsMaxEdits_ReturnsEmpty()
    {
        var results = WcElementOcrText.FindAllSubstrings("", "abc", 1);
        Assert.That(results, Is.Empty);
    }

    [Test]
    public void AdjacentExactMatches()
    {
        var results = WcElementOcrText.FindAllSubstrings("abcabc", "abc", 0);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0], Is.EqualTo((0, 0, 3)));
        Assert.That(results[1], Is.EqualTo((0, 3, 6)));
    }

    [Test]
    public void OverlappingCandidates_BestDistanceWins()
    {
        // "xabxabc" with needle "abc", maxEdits 2
        // "abx" at 1 (dist=1) and "abc" at 4 (dist=0) don't overlap → both kept
        var results = WcElementOcrText.FindAllSubstrings("xabxabc", "abc", 2);
        Assert.That(results, Has.Some.Matches<(int Distance, int Start, int End)>(
            r => r.Distance == 0 && r.Start == 4 && r.End == 7));
        for (int i = 1; i < results.Count; i++)
            Assert.That(results[i].Start, Is.GreaterThanOrEqualTo(results[i - 1].End));
    }

    [Test]
    public void ResultsOrderedByStartPosition()
    {
        var results = WcElementOcrText.FindAllSubstrings("abcxxabcxxabc", "abc", 0);
        Assert.That(results, Has.Count.EqualTo(3));
        for (int i = 1; i < results.Count; i++)
            Assert.That(results[i].Start, Is.GreaterThan(results[i - 1].Start));
    }

    [Test]
    public void CaseInsensitive()
    {
        var results = WcElementOcrText.FindAllSubstrings("xxABCxxabcxx", "Abc", 0);
        Assert.That(results, Has.Count.EqualTo(2));
        Assert.That(results[0].Distance, Is.EqualTo(0));
        Assert.That(results[1].Distance, Is.EqualTo(0));
    }

    [Test]
    public void SingleCharNeedle_FindsAll()
    {
        var results = WcElementOcrText.FindAllSubstrings("abacada", "a", 0);
        Assert.That(results, Has.Count.EqualTo(4));
    }
}

[TestFixture]
[Category("Unit")]
public class OcrFindAllByEditsTests
{
    private static readonly FakeTransport Transport = new();
    private static readonly WcElement FakeElement = new("el1", Transport);

    private static readonly BoundingRect RectA = new(0, 0, 50, 20);
    private static readonly BoundingRect RectB = new(55, 0, 60, 20);
    private static readonly BoundingRect RectC = new(120, 0, 40, 20);

    private static WcElementOcrWord Word(string text, BoundingRect? box = null) =>
        new(FakeElement, box ?? RectA, null, text);

    private static WcElementOcrLine Line(string text, BoundingRect? box = null, params WcElementOcrWord[] words) =>
        new(FakeElement, box ?? new BoundingRect(0, 0, 200, 20), text, null, words);

    [Test]
    public void EmptyText_ReturnsEmpty()
    {
        var word = Word("");
        Assert.That(word.FindAllByEdits("abc"), Is.Empty);
    }

    [Test]
    public void EmptySearch_ReturnsEmpty()
    {
        var word = Word("Hello");
        Assert.That(word.FindAllByEdits(""), Is.Empty);
    }

    [Test]
    public void Word_SingleExactMatch()
    {
        var word = Word("abc", RectA);
        var matches = word.FindAllByEdits("abc");
        Assert.That(matches, Has.Count.EqualTo(1));
        Assert.That(matches[0].Distance, Is.EqualTo(0));
        Assert.That(matches[0].Text, Is.EqualTo("abc"));
    }

    [Test]
    public void Word_NoMatch_ReturnsEmpty()
    {
        var word = Word("xyz");
        Assert.That(word.FindAllByEdits("abc", 0), Is.Empty);
    }

    [Test]
    public void Line_MultipleExactMatches_OrderedByLocation()
    {
        var w1 = Word("abc", new BoundingRect(0, 0, 30, 20));
        var w2 = Word("xx", new BoundingRect(35, 0, 20, 20));
        var w3 = Word("abc", new BoundingRect(60, 0, 30, 20));
        var line = Line("abc xx abc", null, w1, w2, w3);
        var matches = line.FindAllByEdits("abc");
        Assert.That(matches, Has.Count.EqualTo(2));
        Assert.That(matches[0].Distance, Is.EqualTo(0));
        Assert.That(matches[1].Distance, Is.EqualTo(0));
        Assert.That(matches[0].BoundingRect.X, Is.LessThan(matches[1].BoundingRect.X));
    }

    [Test]
    public void Line_FuzzyMatches_BestDistanceWinsOverlap()
    {
        var w1 = Word("xabxabcx", RectA);
        var line = Line("xabxabcx", null, w1);
        var matches = line.FindAllByEdits("abc", 2);
        // "abc" at index 4 (dist=0) should win; "abx" at index 1 (dist=1) may or may not survive depending on overlap
        Assert.That(matches, Has.Some.Matches<WcElementOcrMatch>(m => m.Distance == 0));
        // All matches ordered by location
        for (int i = 1; i < matches.Count; i++)
            Assert.That(matches[i].BoundingRect.X, Is.GreaterThanOrEqualTo(matches[i - 1].BoundingRect.X));
    }

    [Test]
    public void Line_NonOverlapping_AllKept()
    {
        var w1 = Word("abc", new BoundingRect(0, 0, 30, 20));
        var w2 = Word("abc", new BoundingRect(35, 0, 30, 20));
        var w3 = Word("abc", new BoundingRect(70, 0, 30, 20));
        var line = Line("abc abc abc", null, w1, w2, w3);
        var matches = line.FindAllByEdits("abc", 0);
        Assert.That(matches, Has.Count.EqualTo(3));
        Assert.That(matches.All(m => m.Distance == 0), Is.True);
    }
}

[TestFixture]
[Category("Unit")]
public class OcrMatchOverlapsTests
{
    private static readonly FakeTransport Transport = new();
    private static readonly WcElement FakeElement = new("el1", Transport);
    private static readonly BoundingRect Rect = new(0, 0, 100, 20);

    private static WcElementOcrMatch MakeMatch(string ocrText, int from, int to)
    {
        var ocr = new WcElementOcrWord(FakeElement, Rect, null, ocrText);
        var matchText = ocrText[from..to];
        return new WcElementOcrMatch(FakeElement, Rect, matchText, null, ocr, from, to, [], 0);
    }

    [Test]
    public void Overlaps_ExactOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "cd" — overlaps at 2-4
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("cd"), Is.True);
    }

    [Test]
    public void Overlaps_ExactEndingOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "ef" — overlaps at 4-6
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("ef"), Is.True);
    }

    [Test]
    public void Overlaps_FuzzyOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "da" with maxDistance 1 — "de" at 3-5 overlaps
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("da", 1), Is.True);
    }

    [Test]
    public void Overlaps_FuzzyExtendedOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "bc" with maxDistance 1 — "bcd" at 1-4 overlaps
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("bc", 1), Is.True);
    }

    [Test]
    public void Overlaps_FuzzyEndingOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "af" with maxDistance 1 — "de" at 4-6 overlaps
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("af", 1), Is.True);
    }

    [Test]
    public void Overlaps_FuzzyEndingExtendedOverlap_ReturnsTrue()
    {
        // "abcdef", match "cd" at 2-4, check "ef" with maxDistance 1 — "def" at 3-6 overlaps
        var match = MakeMatch("abcdef", 2, 4);
        Assert.That(match.Overlaps("ef", 1), Is.True);
    }

    [Test]
    public void Overlaps_NoOverlap_Exact_ReturnsFalse()
    {
        // "abcdef", match "de" at 3-5, check "bc" — exact match at 1-3, no overlap with 3-5
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("bc"), Is.False);
    }

    [Test]
    public void Overlaps_NoOverlap_BecomesFuzzyOverlap_ReturnsTrue()
    {
        // "abcdef", match "de" at 3-5, check "bc" with maxDistance 1 — can match "cd" at 2-4 which overlaps
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("bc", 1), Is.True);
    }

    [Test]
    public void Overlaps_CompletelyBefore_ReturnsFalse()
    {
        // "abcdef", match "ef" at 4-6, check "ab" — at 0-2, no overlap
        var match = MakeMatch("abcdef", 4, 6);
        Assert.That(match.Overlaps("ab"), Is.False);
    }

    [Test]
    public void Overlaps_CompletelyAfter_ReturnsFalse()
    {
        // "abcdef", match "ab" at 0-2, check "ef" — at 4-6, no overlap
        var match = MakeMatch("abcdef", 0, 2);
        Assert.That(match.Overlaps("ef"), Is.False);
    }

    [Test]
    public void Overlaps_NoMatchAtAll_ReturnsFalse()
    {
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("zzz"), Is.False);
    }

    [Test]
    public void Overlaps_CaseInsensitive()
    {
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.Overlaps("CD"), Is.True);
    }

    [Test]
    public void OverlapsAny_OneOverlaps_ReturnsTrue()
    {
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.OverlapsAny([("ab", 0), ("cd", 0)]), Is.True);
    }

    [Test]
    public void OverlapsAny_NoneOverlap_ReturnsFalse()
    {
        // match "ab" at 0-2; "de" at 3-5 and "ef" at 4-6 don't overlap [0,2)
        var match = MakeMatch("abcdef", 0, 2);
        Assert.That(match.OverlapsAny([("de", 0), ("ef", 0)]), Is.False);
    }

    [Test]
    public void OverlapsAny_EmptyArray_ReturnsFalse()
    {
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.OverlapsAny([]), Is.False);
    }

    [Test]
    public void OverlapsAny_FuzzyEntryOverlaps_ReturnsTrue()
    {
        var match = MakeMatch("abcdef", 3, 5);
        Assert.That(match.OverlapsAny([("ab", 0), ("bc", 1)]), Is.True);
    }
}

[TestFixture]
[Category("Unit")]
public class AnySubstringOverlapsTests
{
    [Test]
    public void EmptyNeedle_OverlapsAtStart()
    {
        // Empty needle matches at position 0 (range [0,0)), which doesn't overlap [3,5)
        Assert.That(WcElementOcrMatch.AnySubstringOverlaps("abcdef", "", 0, 3, 5), Is.False);
    }

    [Test]
    public void EmptyHaystack_ReturnsFalse()
    {
        Assert.That(WcElementOcrMatch.AnySubstringOverlaps("", "abc", 0, 0, 0), Is.False);
    }

    [Test]
    public void MatchAtBoundary_Adjacent_ReturnsFalse()
    {
        // "abcdef", needle "cd" matches at [2,4), range is [4,6) — adjacent, no overlap
        Assert.That(WcElementOcrMatch.AnySubstringOverlaps("abcdef", "cd", 0, 4, 6), Is.False);
    }

    [Test]
    public void MatchAtBoundary_OneCharOverlap_ReturnsTrue()
    {
        // "abcdef", needle "cd" matches at [2,4), range is [3,6) — overlaps at position 3
        Assert.That(WcElementOcrMatch.AnySubstringOverlaps("abcdef", "cd", 0, 3, 6), Is.True);
    }
}
