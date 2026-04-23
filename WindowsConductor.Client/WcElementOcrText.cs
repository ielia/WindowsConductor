using static WindowsConductor.Client.Anchor;

namespace WindowsConductor.Client;

/// <summary>OCR text blob</summary>
/// <param name="Element">WindowsConductor Element containing this OCR text blob.</param>
/// <param name="BoundingRect">Bounding rectangle relative to the element's bounding rectangle, anchored North-West.</param>
/// <param name="Angle">Detected angle.</param>
/// <param name="Text">Text recognized.</param>
public abstract record WcElementOcrText(WcElement Element, BoundingRect BoundingRect, double? Angle, string Text)
{
    public Task ClickAsync(CancellationToken ct = default) =>
        Element.ClickAsync(NorthWest, BoundingRect.Center, ct);

    public Task DoubleClickAsync(CancellationToken ct = default) =>
        Element.DoubleClickAsync(NorthWest, BoundingRect.Center, ct);

    public Task RightClickAsync(CancellationToken ct = default) =>
        Element.RightClickAsync(NorthWest, BoundingRect.Center, ct);

    public Task HoverAsync(CancellationToken ct = default) =>
        Element.HoverAsync(NorthWest, BoundingRect.Center, ct);

    /// <summary>Finds the best fuzzy substring match within this OCR text (case-insensitive).</summary>
    /// <returns>The match, or <c>null</c> if no match exists within <paramref name="maxEdits"/>.</returns>
    public WcElementOcrMatch? FindBestByEdits(string text, int maxEdits = 0)
    {
        if (string.IsNullOrEmpty(Text) || string.IsNullOrEmpty(text))
            return null;
        var (distance, start, end) = FindBestSubstring(Text, text);
        if (distance > maxEdits) return null;
        var (fragments, effStart, effEnd) = BuildMatchFragments(start, end, maxEdits - distance);
        var matchText = effStart < effEnd ? Text[effStart..effEnd] : "";
        var matchRect = fragments.Count > 0 ? UnionRect(fragments) : BoundingRect;
        return new WcElementOcrMatch(Element, matchRect, matchText, Angle, fragments, distance);
    }

    internal virtual (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget) => ([], start, end);

    // ── Wagner-Fischer substring search (case-insensitive) ──────────────────

    internal static (int Distance, int Start, int End) FindBestSubstring(string haystack, string needle)
    {
        int n = haystack.Length, m = needle.Length;
        if (m == 0) return (0, 0, 0);
        if (n == 0) return (m, 0, 0);

        var dp = new int[m + 1, n + 1];
        for (int i = 1; i <= m; i++) dp[i, 0] = i;

        for (int j = 1; j <= n; j++)
            for (int i = 1; i <= m; i++)
            {
                int cost = char.ToLowerInvariant(haystack[j - 1]) == char.ToLowerInvariant(needle[i - 1]) ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + cost);
            }

        int bestDist = dp[m, 0], bestEnd = 0;
        for (int j = 1; j <= n; j++)
            if (dp[m, j] < bestDist) { bestDist = dp[m, j]; bestEnd = j; }

        int bi = m, bj = bestEnd;
        while (bi > 0 && bj > 0)
        {
            int cost = char.ToLowerInvariant(haystack[bj - 1]) == char.ToLowerInvariant(needle[bi - 1]) ? 0 : 1;
            if (dp[bi, bj] == dp[bi - 1, bj - 1] + cost) { bi--; bj--; }
            else if (dp[bi, bj] == dp[bi - 1, bj] + 1) bi--;
            else bj--;
        }

        return (bestDist, bj, bestEnd);
    }

    // ── Fragment building helpers ────────────────────────────────────────────

    internal static (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildChildFragments(
        string parentText, IReadOnlyList<WcElementOcrText> children,
        int matchStart, int matchEnd, int budget)
    {
        var spans = ComputeChildSpans(parentText, children);

        int firstIdx = -1, lastIdx = -1;
        for (int i = 0; i < spans.Count; i++)
            if (spans[i].End > matchStart && spans[i].Start < matchEnd)
            {
                if (firstIdx < 0) firstIdx = i;
                lastIdx = i;
            }

        if (firstIdx < 0) return ([], matchStart, matchEnd);

        int leftExtra = Math.Max(0, matchStart - spans[firstIdx].Start);
        int rightExtra = Math.Max(0, spans[lastIdx].End - matchEnd);

        // Single child spans the match
        if (firstIdx == lastIdx)
        {
            if (leftExtra + rightExtra <= budget)
                return ([children[firstIdx]], spans[firstIdx].Start, spans[firstIdx].End);
            var (f, s, e) = children[firstIdx].BuildMatchFragments(
                matchStart - spans[firstIdx].Start, matchEnd - spans[firstIdx].Start, budget);
            return (f, spans[firstIdx].Start + s, spans[firstIdx].Start + e);
        }

        // Two boundaries — greedy cheapest-first to decide keep vs drill
        int remaining = budget;
        bool keepLeft = false, keepRight = false;

        if (leftExtra <= rightExtra)
        {
            if (leftExtra <= remaining) { keepLeft = true; remaining -= leftExtra; }
            if (rightExtra <= remaining) { keepRight = true; remaining -= rightExtra; }
        }
        else
        {
            if (rightExtra <= remaining) { keepRight = true; remaining -= rightExtra; }
            if (leftExtra <= remaining) { keepLeft = true; remaining -= leftExtra; }
        }

        IReadOnlyList<WcElementOcrText>? leftFrags = null, rightFrags = null;
        int effStart = spans[firstIdx].Start, effEnd = spans[lastIdx].End;

        if (keepLeft || leftExtra == 0)
            leftFrags = [children[firstIdx]];
        if (keepRight || rightExtra == 0)
            rightFrags = [children[lastIdx]];

        // Drill non-kept boundaries, cheaper side first for budget priority
        bool needDrillLeft = leftFrags is null;
        bool needDrillRight = rightFrags is null;

        if (needDrillLeft && needDrillRight)
        {
            if (leftExtra <= rightExtra)
            {
                (leftFrags, effStart, remaining) = DrillLeft(children, spans, firstIdx, matchStart, remaining);
                (rightFrags, effEnd, _) = DrillRight(children, spans, lastIdx, matchEnd, remaining);
            }
            else
            {
                (rightFrags, effEnd, remaining) = DrillRight(children, spans, lastIdx, matchEnd, remaining);
                (leftFrags, effStart, _) = DrillLeft(children, spans, firstIdx, matchStart, remaining);
            }
        }
        else if (needDrillLeft)
        {
            (leftFrags, effStart, _) = DrillLeft(children, spans, firstIdx, matchStart, remaining);
        }
        else if (needDrillRight)
        {
            (rightFrags, effEnd, _) = DrillRight(children, spans, lastIdx, matchEnd, remaining);
        }

        var fragments = new List<WcElementOcrText>();
        fragments.AddRange(leftFrags!);
        for (int i = firstIdx + 1; i < lastIdx; i++)
            fragments.Add(children[i]);
        fragments.AddRange(rightFrags!);

        return (fragments, effStart, effEnd);
    }

    private static (IReadOnlyList<WcElementOcrText> Frags, int EffStart, int Remaining) DrillLeft(
        IReadOnlyList<WcElementOcrText> children, List<(int Start, int End)> spans,
        int idx, int matchStart, int budget)
    {
        int localStart = matchStart - spans[idx].Start;
        var (f, s, _) = children[idx].BuildMatchFragments(localStart, children[idx].Text.Length, budget);
        return (f, spans[idx].Start + s, budget - Math.Max(0, localStart - s));
    }

    private static (IReadOnlyList<WcElementOcrText> Frags, int EffEnd, int Remaining) DrillRight(
        IReadOnlyList<WcElementOcrText> children, List<(int Start, int End)> spans,
        int idx, int matchEnd, int budget)
    {
        int localEnd = matchEnd - spans[idx].Start;
        var (f, _, e) = children[idx].BuildMatchFragments(0, localEnd, budget);
        return (f, spans[idx].Start + e, budget - Math.Max(0, e - localEnd));
    }

    private static List<(int Start, int End)> ComputeChildSpans(
        string parentText, IReadOnlyList<WcElementOcrText> children)
    {
        var spans = new List<(int, int)>();
        int pos = 0;
        foreach (var child in children)
        {
            int idx = parentText.IndexOf(child.Text, pos, StringComparison.Ordinal);
            if (idx < 0) idx = pos;
            spans.Add((idx, idx + child.Text.Length));
            pos = idx + child.Text.Length;
        }
        return spans;
    }

    internal static BoundingRect UnionRect(IReadOnlyList<WcElementOcrText> fragments)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var f in fragments)
        {
            minX = Math.Min(minX, f.BoundingRect.X);
            minY = Math.Min(minY, f.BoundingRect.Y);
            maxX = Math.Max(maxX, f.BoundingRect.X + f.BoundingRect.Width);
            maxY = Math.Max(maxY, f.BoundingRect.Y + f.BoundingRect.Height);
        }
        return new BoundingRect(minX, minY, maxX - minX, maxY - minY);
    }
}

/// <summary>OCR word</summary>
public record WcElementOcrWord(WcElement Element, BoundingRect BoundingRect, double? Angle, string Text)
    : WcElementOcrText(Element, BoundingRect, Angle, Text)
{
    internal override (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget)
    {
        if (start + (Text.Length - end) <= budget) return ([this], 0, Text.Length);
        return ([new WcElementOcrWordSlice(Element, BoundingRect, Angle,
            Text[start..end], this, start, end)], start, end);
    }
}

/// <summary>OCR line, a collection of words on the same line</summary>
public record WcElementOcrLine(WcElement Element, BoundingRect BoundingRect, string Text, double? Angle, IReadOnlyList<WcElementOcrWord> Words)
    : WcElementOcrText(Element, BoundingRect, Angle, Text)
{
    internal override (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget)
        => BuildChildFragments(Text, Words, start, end, budget);
}

/// <summary>Full OCR result, a collection of lines</summary>
public record WcElementOcrResult(WcElement Element, BoundingRect BoundingRect, string Text, double? Angle, IReadOnlyList<WcElementOcrLine> Lines)
    : WcElementOcrText(Element, BoundingRect, Angle, Text)
{
    internal override (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget)
        => BuildChildFragments(Text, Lines, start, end, budget);
}

/// <summary>OCR word-slice for a match</summary>
/// <param name="OriginalWord">The word that got sliced.</param>
/// <param name="FromIndex">First index of the word text that is contained in this slice.</param>
/// <param name="ToIndex">Last index (exclusive) of the word text that is contained in this slice.</param>
public record WcElementOcrWordSlice(WcElement Element, BoundingRect BoundingRect, double? Angle, string Text, WcElementOcrWord OriginalWord, int FromIndex, int ToIndex)
    : WcElementOcrText(Element, BoundingRect, Angle, Text)
{
    internal override (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget)
    {
        if (start + (Text.Length - end) <= budget) return ([this], 0, Text.Length);
        return ([new WcElementOcrWordSlice(OriginalWord.Element, OriginalWord.BoundingRect, Angle,
            Text[start..end], OriginalWord, FromIndex + start, FromIndex + end)], start, end);
    }
}

/// <summary>OCR match resulting from a search.</summary>
/// <param name="Fragments">OCR fragments composing this match.</param>
/// <param name="Distance">Wagner-Fischer distance from the search text.</param>
public record WcElementOcrMatch(WcElement Element, BoundingRect BoundingRect, string Text, double? Angle, IReadOnlyList<WcElementOcrText> Fragments, int Distance)
    : WcElementOcrText(Element, BoundingRect, Angle, Text)
{
    internal override (IReadOnlyList<WcElementOcrText> Fragments, int Start, int End) BuildMatchFragments(
        int start, int end, int budget)
        => BuildChildFragments(Text, Fragments, start, end, budget);
}
