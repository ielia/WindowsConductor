using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;

namespace WindowsConductor.InspectorGUI;

internal enum HighlightRole { Name, Value }

internal static class TextHighlight
{
    public static readonly DependencyProperty SourceTextProperty =
        DependencyProperty.RegisterAttached("SourceText", typeof(string), typeof(TextHighlight),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty TermsProperty =
        DependencyProperty.RegisterAttached("Terms", typeof(object), typeof(TextHighlight),
            new PropertyMetadata(null, OnPropertyChanged));

    public static readonly DependencyProperty RoleProperty =
        DependencyProperty.RegisterAttached("Role", typeof(HighlightRole), typeof(TextHighlight),
            new PropertyMetadata(HighlightRole.Value));

    public static void SetSourceText(DependencyObject d, string? value) => d.SetValue(SourceTextProperty, value);
    public static string? GetSourceText(DependencyObject d) => (string?)d.GetValue(SourceTextProperty);
    public static void SetTerms(DependencyObject d, object? value) => d.SetValue(TermsProperty, value);
    public static object? GetTerms(DependencyObject d) => d.GetValue(TermsProperty);
    public static void SetRole(DependencyObject d, HighlightRole value) => d.SetValue(RoleProperty, value);
    public static HighlightRole GetRole(DependencyObject d) => (HighlightRole)d.GetValue(RoleProperty);

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        Rebuild(tb, GetSourceText(tb), GetTerms(tb) as string[], GetRole(tb));
    }

    private static IEnumerable<string> EffectiveTerms(string[] terms, HighlightRole role)
    {
        foreach (var term in terms)
        {
            var eq = term.IndexOf('=');
            if (eq > 0)
                yield return role == HighlightRole.Name ? term[..eq] : term[(eq + 1)..];
            else
                yield return term;
        }
    }

    private static void Rebuild(TextBlock tb, string? text, string[]? terms, HighlightRole role)
    {
        tb.Inlines.Clear();
        if (string.IsNullOrEmpty(text))
            return;

        if (terms is null or { Length: 0 })
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        // Find all match ranges
        var ranges = new List<(int Start, int End)>();
        var textLower = text.ToLowerInvariant();
        foreach (var term in EffectiveTerms(terms, role))
        {
            if (term.Length == 0) continue;
            int pos = 0;
            while ((pos = textLower.IndexOf(term, pos, StringComparison.Ordinal)) >= 0)
            {
                ranges.Add((pos, pos + term.Length));
                pos += term.Length;
            }
        }

        if (ranges.Count == 0)
        {
            tb.Inlines.Add(new Run(text));
            return;
        }

        // Merge overlapping ranges
        ranges.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.End.CompareTo(b.End));
        var merged = new List<(int Start, int End)> { ranges[0] };
        for (int i = 1; i < ranges.Count; i++)
        {
            var last = merged[^1];
            if (ranges[i].Start <= last.End)
                merged[^1] = (last.Start, Math.Max(last.End, ranges[i].End));
            else
                merged.Add(ranges[i]);
        }

        // Build runs
        int cursor = 0;
        foreach (var (start, end) in merged)
        {
            if (cursor < start)
                tb.Inlines.Add(new Run(text[cursor..start]));
            tb.Inlines.Add(new Run(text[start..end]) { Foreground = System.Windows.Media.Brushes.Red, FontWeight = FontWeights.Bold });
            cursor = end;
        }
        if (cursor < text.Length)
            tb.Inlines.Add(new Run(text[cursor..]));
    }
}
