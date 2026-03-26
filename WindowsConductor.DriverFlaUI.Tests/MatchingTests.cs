using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class SelectorMatchingTests
{
    private static Func<string, string?> Props(
        string? automationId = null, string? name = null,
        string? className = null, string? controlType = null) =>
        key => key switch
        {
            "automationid" => automationId,
            "name" or "text" => name,
            "classname" or "class" => className,
            "type" or "controltype" => controlType,
            _ => null
        };

    // ── automationid ──────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_AutomationId_ExactMatch_ReturnsTrue() =>
        Assert.That(SelectorEngine.MatchesProperty("automationid", "okBtn",
            Props(automationId: "okBtn")), Is.True);

    [Test]
    public void MatchesProperty_AutomationId_CaseInsensitive() =>
        Assert.That(SelectorEngine.MatchesProperty("automationid", "OKBTN",
            Props(automationId: "okBtn")), Is.True);

    [Test]
    public void MatchesProperty_AutomationId_Mismatch_ReturnsFalse() =>
        Assert.That(SelectorEngine.MatchesProperty("automationid", "cancel",
            Props(automationId: "okBtn")), Is.False);

    [Test]
    public void MatchesProperty_AutomationId_Null_ReturnsFalse() =>
        Assert.That(SelectorEngine.MatchesProperty("automationid", "okBtn",
            Props()), Is.False);

    // ── name / text ───────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_Name_ExactMatch() =>
        Assert.That(SelectorEngine.MatchesProperty("name", "OK",
            Props(name: "OK")), Is.True);

    [Test]
    public void MatchesProperty_Name_CaseInsensitive() =>
        Assert.That(SelectorEngine.MatchesProperty("name", "ok",
            Props(name: "OK")), Is.True);

    [Test]
    public void MatchesProperty_Name_Mismatch() =>
        Assert.That(SelectorEngine.MatchesProperty("name", "Cancel",
            Props(name: "OK")), Is.False);

    [Test]
    public void MatchesProperty_Text_AliasForName() =>
        Assert.That(SelectorEngine.MatchesProperty("text", "Hello",
            Props(name: "Hello")), Is.True);

    // ── classname / class ─────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_ClassName_ExactMatch() =>
        Assert.That(SelectorEngine.MatchesProperty("classname", "Panel",
            Props(className: "Panel")), Is.True);

    [Test]
    public void MatchesProperty_Class_Alias() =>
        Assert.That(SelectorEngine.MatchesProperty("class", "Panel",
            Props(className: "Panel")), Is.True);

    [Test]
    public void MatchesProperty_ClassName_Mismatch() =>
        Assert.That(SelectorEngine.MatchesProperty("classname", "Grid",
            Props(className: "Panel")), Is.False);

    // ── type / controltype ────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_Type_ExactMatch() =>
        Assert.That(SelectorEngine.MatchesProperty("type", "Button",
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesProperty_ControlType_Alias() =>
        Assert.That(SelectorEngine.MatchesProperty("controltype", "Edit",
            Props(controlType: "Edit")), Is.True);

    [Test]
    public void MatchesProperty_Type_CaseInsensitive() =>
        Assert.That(SelectorEngine.MatchesProperty("type", "button",
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesProperty_Type_Mismatch() =>
        Assert.That(SelectorEngine.MatchesProperty("type", "Edit",
            Props(controlType: "Button")), Is.False);

    // ── unknown key ───────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_UnknownKey_ReturnsFalse() =>
        Assert.That(SelectorEngine.MatchesProperty("unknown", "value",
            Props(automationId: "id", name: "n", className: "c", controlType: "Button")), Is.False);

    // ── resolver only called for the needed key ─────────────────────────────

    [Test]
    public void MatchesProperty_OnlyAccessesRequestedKey()
    {
        var accessed = new List<string>();
        Func<string, string?> spy = key => { accessed.Add(key); return key == "name" ? "OK" : null; };

        SelectorEngine.MatchesProperty("name", "OK", spy);

        Assert.That(accessed, Is.EqualTo(new[] { "name" }));
    }
}

[TestFixture]
[Category("Unit")]
public class XPathMatchingTests
{
    private static Func<string, string?> Props(
        string? automationId = null, string? name = null,
        string? className = null, string? controlType = null) =>
        key => key switch
        {
            "automationid" => automationId,
            "name" => name,
            "classname" => className,
            "controltype" => controlType,
            _ => null
        };

    private static XPathStep MakeStep(string type, params (string attr, string[] values)[] preds)
    {
        var predicates = preds
            .Select(p => new XPathPredicate(p.attr, p.values))
            .ToList();
        return new XPathStep(XPathAxis.Descendant, type, predicates);
    }

    // ── Type matching ─────────────────────────────────────────────────────────

    [Test]
    public void MatchesStep_Wildcard_MatchesAnyType() =>
        Assert.That(XPathEngine.MatchesStep(MakeStep("*"),
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_SpecificType_Matches() =>
        Assert.That(XPathEngine.MatchesStep(MakeStep("Button"),
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_SpecificType_Mismatch() =>
        Assert.That(XPathEngine.MatchesStep(MakeStep("Button"),
            Props(controlType: "Edit")), Is.False);

    [Test]
    public void MatchesStep_InvalidControlType_ReturnsFalse() =>
        Assert.That(XPathEngine.MatchesStep(MakeStep("NotARealControlType"),
            Props(controlType: "Button")), Is.False);

    // ── Predicate matching ────────────────────────────────────────────────────

    [Test]
    public void MatchesStep_AutomationIdPredicate_Matches() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("AutomationId", new[] { "okBtn" })),
            Props(automationId: "okBtn", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_AutomationIdPredicate_CaseInsensitive() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("AutomationId", new[] { "OKBTN" })),
            Props(automationId: "okBtn", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_NamePredicate_Matches() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Button", ("Name", new[] { "OK" })),
            Props(name: "OK", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_NamePredicate_Mismatch() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Button", ("Name", new[] { "Cancel" })),
            Props(name: "OK", controlType: "Button")), Is.False);

    [Test]
    public void MatchesStep_ClassNamePredicate_Matches() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("ClassName", new[] { "Panel" })),
            Props(className: "Panel", controlType: "Custom")), Is.True);

    [Test]
    public void MatchesStep_ControlTypePredicate_Matches() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("ControlType", new[] { "Button" })),
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_UnknownAttribute_ReturnsFalse() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("Bogus", new[] { "val" })),
            Props(automationId: "id", name: "n", className: "c", controlType: "Button")), Is.False);

    // ── Multi-value predicates ────────────────────────────────────────────────

    [Test]
    public void MatchesStep_MultiValue_MatchesFirst() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("Name", new[] { "OK", "Apply" })),
            Props(name: "OK", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_MultiValue_MatchesSecond() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("Name", new[] { "OK", "Apply" })),
            Props(name: "Apply", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_MultiValue_NoneMatch() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("Name", new[] { "OK", "Apply" })),
            Props(name: "Cancel", controlType: "Button")), Is.False);

    // ── Multiple predicates (AND logic) ───────────────────────────────────────

    [Test]
    public void MatchesStep_TwoPredicates_BothMatch() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Button", ("AutomationId", new[] { "okBtn" }), ("Name", new[] { "OK" })),
            Props(automationId: "okBtn", name: "OK", controlType: "Button")), Is.True);

    [Test]
    public void MatchesStep_TwoPredicates_FirstFails() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Button", ("AutomationId", new[] { "cancelBtn" }), ("Name", new[] { "OK" })),
            Props(automationId: "okBtn", name: "OK", controlType: "Button")), Is.False);

    [Test]
    public void MatchesStep_TwoPredicates_SecondFails() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Button", ("AutomationId", new[] { "okBtn" }), ("Name", new[] { "Cancel" })),
            Props(automationId: "okBtn", name: "OK", controlType: "Button")), Is.False);

    // ── No predicates ─────────────────────────────────────────────────────────

    [Test]
    public void MatchesStep_NoPredicate_MatchesOnType() =>
        Assert.That(XPathEngine.MatchesStep(MakeStep("Button"),
            Props(controlType: "Button")), Is.True);

    // ── Type + predicate combined ─────────────────────────────────────────────

    [Test]
    public void MatchesStep_TypeMismatch_PredicateMatch_ReturnsFalse() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("Edit", ("Name", new[] { "OK" })),
            Props(name: "OK", controlType: "Button")), Is.False);

    // ── Null property values ──────────────────────────────────────────────────

    [Test]
    public void MatchesStep_NullName_PredicateOnName_ReturnsFalse() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("Name", new[] { "OK" })),
            Props(controlType: "Button")), Is.False);

    [Test]
    public void MatchesStep_NullAutomationId_PredicateOnId_ReturnsFalse() =>
        Assert.That(XPathEngine.MatchesStep(
            MakeStep("*", ("AutomationId", new[] { "btn1" })),
            Props(name: "OK", controlType: "Button")), Is.False);

    // ── resolver only called for needed keys ────────────────────────────────

    [Test]
    public void MatchesStep_TypeCheck_OnlyAccessesControlType()
    {
        var accessed = new List<string>();
        Func<string, string?> spy = key => { accessed.Add(key); return key == "controltype" ? "Edit" : null; };

        XPathEngine.MatchesStep(MakeStep("Button"), spy);

        Assert.That(accessed, Is.EqualTo(new[] { "controltype" }));
    }

    [Test]
    public void MatchesStep_Wildcard_WithPredicate_DoesNotAccessControlTypeForTypeCheck()
    {
        var accessed = new List<string>();
        Func<string, string?> spy = key => { accessed.Add(key); return key == "name" ? "OK" : "Button"; };

        XPathEngine.MatchesStep(MakeStep("*", ("Name", new[] { "OK" })), spy);

        Assert.That(accessed, Is.EqualTo(new[] { "name" }));
    }
}
