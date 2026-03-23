using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
public class SelectorValidatorTests
{
    // ── Empty / whitespace ───────────────────────────────────────────────────

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Validate_EmptyOrWhitespace_Throws(string? selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector!));
    }

    // ── XPath: missing type before predicate ─────────────────────────────────

    [TestCase("//[@AutomationId='foo']")]
    [TestCase("//[Name='bar']")]
    [TestCase("/[@Name='x']")]
    [TestCase("//[@attr='a']//Button[@Name='b']")]
    public void Validate_XPathMissingType_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("missing an element type"));
    }

    // ── XPath: empty predicate ───────────────────────────────────────────────

    [Test]
    public void Validate_XPathEmptyPredicate_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate("//Button[]"));
        Assert.That(ex!.Message, Does.Contain("Empty predicate"));
    }

    // ── XPath: unclosed bracket ──────────────────────────────────────────────

    [Test]
    public void Validate_XPathUnclosedBracket_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate("//Button[@Name='foo'"));
        Assert.That(ex!.Message, Does.Contain("Unclosed bracket"));
    }

    // ── XPath: predicate missing @ ───────────────────────────────────────────

    [TestCase("//Button[Name='foo']")]
    [TestCase("//Button[invalid]")]
    public void Validate_XPathPredicateMissingAt_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Predicates must start with '@'"));
    }

    // ── Simple: unclosed bracket ─────────────────────────────────────────────

    [TestCase("[automationid=foo")]
    [TestCase("[name=bar")]
    public void Validate_SimpleUnclosedBracket_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Unclosed bracket"));
    }

    // ── Simple: empty brackets ───────────────────────────────────────────────

    [Test]
    public void Validate_SimpleEmptyBrackets_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate("[]"));
        Assert.That(ex!.Message, Does.Contain("Empty bracket selector"));
    }

    // ── Simple: missing key in bracket ───────────────────────────────────────

    [TestCase("[=value]")]
    public void Validate_SimpleMissingKey_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("[key=value]"));
    }

    // ── Simple: unknown attribute ────────────────────────────────────────────

    [TestCase("[invalid=foo]")]
    [TestCase("[href=bar]")]
    [TestCase("invalid=foo")]
    [TestCase("[id=something]")]
    public void Validate_SimpleUnknownAttribute_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Unknown selector attribute"));
    }

    // ── Simple: unexpected closing bracket ───────────────────────────────────

    [TestCase("name=foo]")]
    public void Validate_SimpleUnexpectedClosingBracket_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Unexpected closing bracket"));
    }

    // ── Compound: one invalid part ───────────────────────────────────────────

    [TestCase("[automationid=foo]&&[invalid=bar]")]
    [TestCase("[automationid=foo]&&[=bar]")]
    public void Validate_CompoundWithInvalidPart_Throws(string selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector));
    }

    // ── Chained selectors (" >> ") ───────────────────────────────────────────

    [Test]
    public void Validate_ChainedWithInvalidSegment_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => SelectorValidator.Validate("[automationid=foo] >> //[@Name='bar']"));
    }

    [Test]
    public void Validate_ChainedWithEmptySegment_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => SelectorValidator.Validate("[automationid=foo] >>  >> [name=bar]"));
    }

    // ── Valid selectors should NOT throw ─────────────────────────────────────

    [TestCase("[automationid=num7Button]")]
    [TestCase("[name=Cancel]")]
    [TestCase("[classname=Foo]")]
    [TestCase("text=Cancel")]
    [TestCase("type=Button")]
    [TestCase("controltype=Edit")]
    [TestCase("[automationid=okBtn]&&type=Button")]
    [TestCase("[name=Text editor]&&type=Document")]
    [TestCase("Hello")]                                 // bare text → name=Hello
    [TestCase("//Button[@AutomationId='num7']")]        // valid XPath
    [TestCase("//*[@Name='Cancel']")]                   // valid XPath
    [TestCase("//Button")]                              // XPath without predicate
    [TestCase("//Window[@Name='Calc']//Button[@Name='7']")]
    public void Validate_ValidSelector_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorValidator.Validate(selector));
    }

    // ── Valid chained selectors ──────────────────────────────────────────────

    [TestCase("[automationid=parent] >> [name=child]")]
    [TestCase("type=Window >> type=Button")]
    [TestCase("[automationid=foo] >> //Button[@Name='OK']")]
    public void Validate_ValidChainedSelector_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorValidator.Validate(selector));
    }
}
