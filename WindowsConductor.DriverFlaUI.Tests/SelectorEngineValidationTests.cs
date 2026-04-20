using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class SelectorEngineValidationTests
{
    // ── Empty / whitespace ───────────────────────────────────────────────────

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Validate_EmptyOrWhitespace_Throws(string? selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector!));
    }

    // ── Unclosed bracket ─────────────────────────────────────────────────────

    [TestCase("[automationid=foo")]
    [TestCase("[name=bar")]
    public void Validate_UnclosedBracket_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Unclosed bracket"));
    }

    // ── Empty bracket selector ───────────────────────────────────────────────

    [Test]
    public void Validate_EmptyBrackets_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorEngine.Validate("[]"));
        Assert.That(ex!.Message, Does.Contain("Empty bracket selector"));
    }

    // ── Missing key in bracket ───────────────────────────────────────────────

    [TestCase("[=value]")]
    public void Validate_MissingKeyInBracket_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("[key=value]"));
    }

    // ── Custom attributes are now accepted ──────────────────────────────────

    [TestCase("[custom=foo]")]
    [TestCase("[href=bar]")]
    [TestCase("custom=foo")]
    [TestCase("[id=something]")]
    public void Validate_CustomAttribute_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorEngine.Validate(selector));
    }

    // ── Unexpected closing bracket ───────────────────────────────────────────

    [TestCase("name=foo]")]
    public void Validate_UnexpectedClosingBracket_Throws(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("Unexpected closing bracket"));
    }

    // ── XPath delegation — invalid XPath ─────────────────────────────────────

    [TestCase("//[@attr='value']")]
    [TestCase("//[Name='bar']")]
    public void Validate_InvalidXPath_DelegatesToXPathEngineAndThrows(string selector)
    {
        var ex = Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
        Assert.That(ex!.Message, Does.Contain("missing an element type"));
    }

    // ── Compound selector with syntax error ──────────────────────────────────

    [TestCase("[automationid=foo]&&[=bar]")]
    public void Validate_CompoundWithSyntaxError_Throws(string selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorEngine.Validate(selector));
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
    [TestCase("Hello")]
    [TestCase("//Button[@AutomationId='num7']")]
    [TestCase("//*[@Name='Cancel']")]
    [TestCase("[isenabled=true]")]
    [TestCase("[isoffscreen=false]")]
    [TestCase("[frameworkid=Win32]")]
    [TestCase("[processid=1234]")]
    [TestCase("[helptext=Click me]")]
    [TestCase("[isenabled=true]&&type=Button")]
    [TestCase("[ariarole=button]")]
    [TestCase("[headinglevel=1]")]
    [TestCase("./Button")]
    [TestCase(".//Button[@Name='OK']")]
    [TestCase("../Button")]
    [TestCase("[automationid=foo]&&[custom=bar]")]
    [TestCase("concat('a', 'b')")]
    [TestCase("string-join(('a', 'b'), ',')")]
    [TestCase("true()")]
    [TestCase("math:pi()")]
    [TestCase("//Button/child::Edit")]
    [TestCase("//Button/descendant::Edit")]
    [TestCase("//Button/descendant-or-self::*")]
    [TestCase("//*[self::Button]")]
    [TestCase("//Button/sibling::Edit")]
    [TestCase("//Button/preceding-sibling::Edit")]
    [TestCase("//Button/following-sibling::Edit")]
    [TestCase("//Button/attribute::Name")]
    [TestCase("self::Button")]
    [TestCase("child::Button")]
    [TestCase("descendant::Button")]
    [TestCase("sibling::Edit")]
    [TestCase("preceding-sibling::Edit")]
    [TestCase("following-sibling::Edit")]
    [TestCase("ancestor::Window")]
    [TestCase("frontmost::Button")]
    [TestCase("parent::Window")]
    [TestCase("parent::*")]
    public void Validate_ValidSelector_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorEngine.Validate(selector));
    }

    // ── ParsePart unit tests ─────────────────────────────────────────────────

    [Test]
    public void ParsePart_BracketSelector_ReturnsKeyValue()
    {
        var (key, value) = SelectorEngine.ParsePart("[automationid=foo]");
        Assert.That(key, Is.EqualTo("automationid"));
        Assert.That(value, Is.EqualTo("foo"));
    }

    [Test]
    public void ParsePart_ShorthandSelector_ReturnsKeyValue()
    {
        var (key, value) = SelectorEngine.ParsePart("type=Button");
        Assert.That(key, Is.EqualTo("controltype"));
        Assert.That(value, Is.EqualTo("Button"));
    }

    [Test]
    public void ParsePart_BareText_ReturnNameKey()
    {
        var (key, value) = SelectorEngine.ParsePart("Cancel");
        Assert.That(key, Is.EqualTo("name"));
        Assert.That(value, Is.EqualTo("Cancel"));
    }

    [Test]
    public void ParsePart_KeyIsCaseInsensitive()
    {
        var (key, _) = SelectorEngine.ParsePart("[AutomationId=foo]");
        Assert.That(key, Is.EqualTo("automationid"));
    }
}
