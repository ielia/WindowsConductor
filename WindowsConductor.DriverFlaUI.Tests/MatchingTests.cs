using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class SelectorMatchingTests
{
    private static Func<string, string?> Props(Dictionary<string, string?> values) =>
        key => values.TryGetValue(key, out var v) ? v : null;

    private static Func<string, string?> Props(
        string? automationId = null, string? name = null,
        string? className = null, string? controlType = null) =>
        Props(new Dictionary<string, string?>
        {
            ["automationid"] = automationId,
            ["name"] = name,
            ["classname"] = className,
            ["controltype"] = controlType,
        });

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

    // ── name ──────────────────────────────────────────────────────────────────

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

    // ── classname ─────────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_ClassName_ExactMatch() =>
        Assert.That(SelectorEngine.MatchesProperty("classname", "Panel",
            Props(className: "Panel")), Is.True);

    [Test]
    public void MatchesProperty_ClassName_Mismatch() =>
        Assert.That(SelectorEngine.MatchesProperty("classname", "Grid",
            Props(className: "Panel")), Is.False);

    // ── controltype ───────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_ControlType_ExactMatch() =>
        Assert.That(SelectorEngine.MatchesProperty("controltype", "Button",
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesProperty_ControlType_CaseInsensitive() =>
        Assert.That(SelectorEngine.MatchesProperty("controltype", "button",
            Props(controlType: "Button")), Is.True);

    [Test]
    public void MatchesProperty_ControlType_Mismatch() =>
        Assert.That(SelectorEngine.MatchesProperty("controltype", "Edit",
            Props(controlType: "Button")), Is.False);

    // ── new properties ────────────────────────────────────────────────────────

    [Test]
    public void MatchesProperty_IsEnabled_Matches() =>
        Assert.That(SelectorEngine.MatchesProperty("isenabled", "true",
            Props(new() { ["isenabled"] = "true" })), Is.True);

    [Test]
    public void MatchesProperty_FrameworkId_Matches() =>
        Assert.That(SelectorEngine.MatchesProperty("frameworkid", "Win32",
            Props(new() { ["frameworkid"] = "Win32" })), Is.True);

    [Test]
    public void MatchesProperty_ProcessId_Matches() =>
        Assert.That(SelectorEngine.MatchesProperty("processid", "1234",
            Props(new() { ["processid"] = "1234" })), Is.True);

    [Test]
    public void MatchesProperty_HelpText_Matches() =>
        Assert.That(SelectorEngine.MatchesProperty("helptext", "Click me",
            Props(new() { ["helptext"] = "Click me" })), Is.True);

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

[TestFixture]
[Category("Unit")]
public class ElementPropertiesTests
{
    // ── Normalize ─────────────────────────────────────────────────────────────

    [TestCase("text", "name")]
    [TestCase("Text", "name")]
    [TestCase("class", "classname")]
    [TestCase("CLASS", "classname")]
    [TestCase("type", "controltype")]
    [TestCase("Type", "controltype")]
    public void Normalize_Alias_ReturnsCanonical(string input, string expected) =>
        Assert.That(ElementProperties.Normalize(input), Is.EqualTo(expected));

    [TestCase("automationid")]
    [TestCase("name")]
    [TestCase("classname")]
    [TestCase("controltype")]
    [TestCase("isenabled")]
    [TestCase("processid")]
    public void Normalize_Canonical_ReturnsSelf(string key) =>
        Assert.That(ElementProperties.Normalize(key), Is.EqualTo(key));

    // ── IsSupported ───────────────────────────────────────────────────────────

    [TestCase("automationid")]
    [TestCase("name")]
    [TestCase("classname")]
    [TestCase("controltype")]
    [TestCase("isenabled")]
    [TestCase("isoffscreen")]
    [TestCase("frameworkid")]
    [TestCase("helptext")]
    [TestCase("processid")]
    [TestCase("itemtype")]
    [TestCase("acceleratorkey")]
    [TestCase("accesskey")]
    [TestCase("haskeyboardfocus")]
    [TestCase("iscontentelement")]
    [TestCase("iscontrolelement")]
    [TestCase("iskeyboardfocusable")]
    [TestCase("ispassword")]
    [TestCase("isrequiredforform")]
    [TestCase("itemstatus")]
    [TestCase("localizedcontroltype")]
    [TestCase("nativewindowhandle")]
    [TestCase("orientation")]
    [TestCase("boundingrectangle")]
    [TestCase("ariaproperties")]   // auto-discovered via reflection
    [TestCase("ariarole")]
    [TestCase("headinglevel")]
    [TestCase("culture")]
    [TestCase("isperipheral")]
    [TestCase("isdialog")]
    public void IsSupported_KnownKey_ReturnsTrue(string key) =>
        Assert.That(ElementProperties.IsSupported(key), Is.True);

    [TestCase("text")]   // alias for name
    [TestCase("class")]  // alias for classname
    [TestCase("type")]   // alias for controltype
    [TestCase("TEXT")]
    [TestCase("Class")]
    [TestCase("Type")]
    public void IsSupported_Alias_ReturnsTrue(string key) =>
        Assert.That(ElementProperties.IsSupported(key), Is.True);

    [TestCase("invalid")]
    [TestCase("href")]
    [TestCase("id")]
    [TestCase("")]
    public void IsSupported_Unknown_ReturnsFalse(string key) =>
        Assert.That(ElementProperties.IsSupported(key), Is.False);
}
