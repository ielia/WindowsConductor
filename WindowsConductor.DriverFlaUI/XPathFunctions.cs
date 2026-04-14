using FlaUI.Core.AutomationElements;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Function registry and expression evaluator for XPath predicates.
/// Functions are registered by name and invoked at evaluation time with type coercion.
/// </summary>
internal static class XPathFunctions
{
    private sealed record FunctionDef(string Name, int MinArgs, int MaxArgs, Func<XPathValue[], EvalContext, XPathValue> Invoke);

    private static readonly Dictionary<string, FunctionDef> Registry = BuildRegistry();

    private static Dictionary<string, FunctionDef> BuildRegistry()
    {
        var r = new Dictionary<string, FunctionDef>(StringComparer.OrdinalIgnoreCase);

        void Add(string name, int min, int max, Func<XPathValue[], EvalContext, XPathValue> fn) =>
            r[name] = new FunctionDef(name, min, max, fn);

        // Boolean constants
        Add("true", 0, 0, (_, _) => new XPathBool(true));
        Add("false", 0, 0, (_, _) => new XPathBool(false));

        // Context functions
        Add("position", 0, 0, (_, ctx) => new XPathNumber(ctx.Position));
        Add("last", 0, 0, (_, ctx) => new XPathNumber(ctx.Last));

        // String functions
        Add("concat", 2, -1, (args, _) =>
            new XPathString(string.Concat(args.Select(a => a.AsString()))));

        Add("string-length", 0, 1, (args, ctx) =>
        {
            var s = args.Length == 0 ? ctx.GetProperty("text") ?? "" : args[0].AsString();
            return new XPathNumber(s.Length);
        });

        Add("contains", 2, 2, (args, _) =>
            new XPathBool(args[0].AsString().Contains(args[1].AsString(), StringComparison.InvariantCultureIgnoreCase)));

        Add("starts-with", 2, 2, (args, _) =>
            new XPathBool(args[0].AsString().StartsWith(args[1].AsString(), StringComparison.InvariantCultureIgnoreCase)));

        Add("ends-with", 2, 2, (args, _) =>
            new XPathBool(args[0].AsString().EndsWith(args[1].AsString(), StringComparison.InvariantCultureIgnoreCase)));

        // text() — returns the Text property
        Add("text", 0, 0, (_, ctx) =>
            new XPathString(ctx.GetProperty("text") ?? ""));

        // Spatial functions
        Add("bounds", 0, 0, (_, ctx) =>
        {
            if (ctx.Element is not AutomationElement el) return new XPathRect(0, 0, 0, 0);
            var rect = GetTopContainerRelativeBounds(el);
            return new XPathRect(rect.X, rect.Y, rect.Width, rect.Height);
        });

        Add("point", 2, 2, (args, _) =>
            new XPathPoint(args[0].AsNumber(), args[1].AsNumber()));

        Add("contains-point", 2, 2, (args, _) =>
        {
            if (args[0] is XPathRect r && args[1] is XPathPoint p)
                return new XPathBool(r.ContainsPoint((int)p.X, (int)p.Y));
            return new XPathBool(false);
        });

        // at(x, y) — shorthand for contains-point(bounds(), point(x, y))
        Add("at", 2, 2, (args, ctx) =>
        {
            var bounds = r["bounds"].Invoke([], ctx);
            var point = new XPathPoint(args[0].AsNumber(), args[1].AsNumber());
            return r["contains-point"].Invoke([bounds, point], ctx);
        });

        Add("not", 1, 1, (args, _) =>
            new XPathBool(!args[0].AsBool()));

        return r;
    }

    // ── Expression evaluation ───────────────────────────────────────────────

    internal static XPathValue Evaluate(XPathExpr expr, EvalContext ctx) => expr switch
    {
        LiteralStringExpr s => new XPathString(s.Value),
        LiteralNumberExpr n => new XPathNumber(n.Value),
        AttrRefExpr a => new XPathString(ctx.GetProperty(a.Name.ToLowerInvariant()) ?? ""),
        FunctionCallExpr f => InvokeFunction(f, ctx),
        UnaryMinusExpr u => new XPathNumber(-Evaluate(u.Operand, ctx).AsNumber()),
        SubPathExpr sp => ctx.SubPathEvaluator is not null
            ? new XPathBool(ctx.SubPathEvaluator(sp))
            : throw new InvalidOperationException("Sub-path expressions require an element context for evaluation."),
        SequenceExpr => throw new ArgumentException("Sequence expressions can only appear in comparisons."),
        BinaryExpr b => EvaluateBinary(b, ctx),
        _ => throw new ArgumentException($"Unknown expression type: {expr.GetType().Name}")
    };

    private static XPathValue InvokeFunction(FunctionCallExpr call, EvalContext ctx)
    {
        if (!Registry.TryGetValue(call.Name, out var def))
            throw new ArgumentException($"Unknown function: '{call.Name}()'");

        if (call.Args.Count < def.MinArgs || (def.MaxArgs >= 0 && call.Args.Count > def.MaxArgs))
        {
            string expected = def.MaxArgs < 0 ? $"at least {def.MinArgs}"
                : def.MinArgs == def.MaxArgs ? $"{def.MinArgs}"
                : $"{def.MinArgs}-{def.MaxArgs}";
            throw new ArgumentException(
                $"{call.Name}() expects {expected} argument(s), got {call.Args.Count}.");
        }

        var evaluatedArgs = call.Args.Select(a => Evaluate(a, ctx)).ToArray();
        return def.Invoke(evaluatedArgs, ctx);
    }

    private static XPathValue EvaluateBinary(BinaryExpr expr, EvalContext ctx)
    {
        // Logical operators (short-circuit)
        if (expr.Op == XPathBinaryOp.And)
            return new XPathBool(Evaluate(expr.Left, ctx).AsBool() && Evaluate(expr.Right, ctx).AsBool());

        if (expr.Op == XPathBinaryOp.Or)
            return new XPathBool(Evaluate(expr.Left, ctx).AsBool() || Evaluate(expr.Right, ctx).AsBool());

        // Comparison operators — support general comparison (sequence on either side)
        if (expr.Op is >= XPathBinaryOp.Eq and <= XPathBinaryOp.GtEq)
        {
            var leftValues = Flatten(expr.Left, ctx);
            var rightValues = Flatten(expr.Right, ctx);

            foreach (var l in leftValues)
                foreach (var r in rightValues)
                    if (CompareValues(l, r, expr.Op))
                        return new XPathBool(true);
            return new XPathBool(false);
        }

        // Arithmetic operators
        double left = Evaluate(expr.Left, ctx).AsNumber();
        double right = Evaluate(expr.Right, ctx).AsNumber();
        return new XPathNumber(expr.Op switch
        {
            XPathBinaryOp.Add => left + right,
            XPathBinaryOp.Sub => left - right,
            XPathBinaryOp.Mul => left * right,
            XPathBinaryOp.Div => left / right,
            XPathBinaryOp.Mod => (int)left % (int)right,
            _ => throw new ArgumentException($"Unknown binary operator: {expr.Op}")
        });
    }

    private static List<XPathValue> Flatten(XPathExpr expr, EvalContext ctx)
    {
        if (expr is SequenceExpr seq)
            return seq.Items.Select(item => Evaluate(item, ctx)).ToList();
        return [Evaluate(expr, ctx)];
    }

    private static bool CompareValues(XPathValue left, XPathValue right, XPathBinaryOp op)
    {
        // Both booleans → boolean comparison
        if (left is XPathBool lb && right is XPathBool rb)
            return CompareBools(lb.Value, rb.Value, op);

        // One is boolean → coerce the other to boolean (with smart parsing for strings)
        if (left is XPathBool lb2)
            return CompareBools(lb2.Value, CoerceToBool(right), op);
        if (right is XPathBool rb2)
            return CompareBools(CoerceToBool(left), rb2.Value, op);

        // Both numbers → numeric comparison
        if (left is XPathNumber ln && right is XPathNumber rn)
            return CompareDoubles(ln.Value, rn.Value, op);

        // One is number → coerce the other
        if (left is XPathNumber ln2)
        {
            double rv = right.AsNumber();
            if (!double.IsNaN(rv))
                return CompareDoubles(ln2.Value, rv, op);
        }
        if (right is XPathNumber rn2)
        {
            double lv = left.AsNumber();
            if (!double.IsNaN(lv))
                return CompareDoubles(lv, rn2.Value, op);
        }

        // Default: string comparison (case-insensitive)
        return CompareStrings(left.AsString(), right.AsString(), op);
    }

    private static bool CoerceToBool(XPathValue value)
    {
        // Smart coercion: try parsing "True"/"False" before falling back to XPath rules
        if (value is XPathString s && bool.TryParse(s.Value, out var parsed))
            return parsed;
        return value.AsBool();
    }

    private static bool CompareBools(bool left, bool right, XPathBinaryOp op) => op switch
    {
        XPathBinaryOp.Eq => left == right,
        XPathBinaryOp.NotEq => left != right,
        _ => CompareDoubles(left ? 1 : 0, right ? 1 : 0, op)
    };

    private static bool CompareDoubles(double left, double right, XPathBinaryOp op) => op switch
    {
        XPathBinaryOp.Eq => left == right,
        XPathBinaryOp.NotEq => left != right,
        XPathBinaryOp.Lt => left < right,
        XPathBinaryOp.Gt => left > right,
        XPathBinaryOp.LtEq => left <= right,
        XPathBinaryOp.GtEq => left >= right,
        _ => false
    };

    private static bool CompareStrings(string left, string right, XPathBinaryOp op)
    {
        int cmp = string.Compare(left, right, StringComparison.InvariantCultureIgnoreCase);
        return op switch
        {
            XPathBinaryOp.Eq => cmp == 0,
            XPathBinaryOp.NotEq => cmp != 0,
            XPathBinaryOp.Lt => cmp < 0,
            XPathBinaryOp.Gt => cmp > 0,
            XPathBinaryOp.LtEq => cmp <= 0,
            XPathBinaryOp.GtEq => cmp >= 0,
            _ => false
        };
    }

    // ── Spatial helpers (from XPathEngine) ───────────────────────────────────

    internal static System.Drawing.Rectangle GetTopContainerRelativeBounds(AutomationElement element)
    {
        var rect = element.BoundingRectangle;
        var origin = GetTopContainerOrigin(element);
        return new System.Drawing.Rectangle(
            rect.X - origin.X, rect.Y - origin.Y,
            rect.Width, rect.Height);
    }

    private static System.Drawing.Point GetTopContainerOrigin(AutomationElement element)
    {
        var current = element;
        var parent = SafeGetParent(current);
        while (parent is not null)
        {
            var grandparent = SafeGetParent(parent);
            if (grandparent is null) break;
            current = parent;
            parent = grandparent;
        }

        if (parent is null)
            return System.Drawing.Point.Empty;

        var r = current.BoundingRectangle;
        return new System.Drawing.Point(r.X, r.Y);
    }

    private static AutomationElement? SafeGetParent(AutomationElement el)
    {
        try { return el.Parent; }
        catch { return null; }
    }
}
