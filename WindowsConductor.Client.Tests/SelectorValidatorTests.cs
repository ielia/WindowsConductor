using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

[TestFixture]
[Category("Unit")]
public class SelectorValidatorTests
{
    // -- Empty / whitespace ---------------------------------------------------

    [TestCase("")]
    [TestCase("   ")]
    [TestCase(null)]
    public void Validate_EmptyOrWhitespace_Throws(string? selector)
    {
        Assert.Throws<ArgumentException>(() => SelectorValidator.Validate(selector!));
    }

    // -- Valid selectors should NOT throw -------------------------------------

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
    [TestCase("//Button")]
    [TestCase("//Window[@Name='Calc']//Button[@Name='7']")]
    [TestCase("//Button[3]")]
    [TestCase("//Button[@Name='OK'][2]")]
    [TestCase("//Button[@Name='OK']/..")]
    [TestCase("//Button/..")]
    [TestCase("./Button")]
    [TestCase(".//Button")]
    [TestCase(".//Button[@Name='OK']")]
    [TestCase("./Panel/Button")]
    [TestCase("../Button")]
    [TestCase("../../Button")]
    [TestCase("//Button[position()=5]")]
    [TestCase("//Button[@Name='OK'][position()=2]")]
    [TestCase("//Button[3 < position()]")]
    [TestCase("//Button[position()-1 = 3]")]
    [TestCase("//Button[position() = last() - 1]")]
    [TestCase("//Button[position() != last()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Button[position() <= last() - 2]")]
    [TestCase("//Button[last() / 2 = position()]")]
    [TestCase("//Button[position() >= 2]")]
    [TestCase("//Button[starts-with(@Name, 'Start')]")]
    [TestCase("//Button[contains(@Name, 'thing')]")]
    [TestCase("//Button[ends-with(@Name, 'End')]")]
    [TestCase("//Button[@Name='foo' and @AutomationId='bar']")]
    [TestCase("//Button[@Name='a' or @Name='b']")]
    [TestCase("//Button[@Name=concat('foo', 'bar')]")]
    [TestCase("//Button[string-length(@Name) > 5]")]
    [TestCase("//Button[position() mod 2 = 1]")]
    [TestCase("//Button[position() div 3 > 1.5]")]
    [TestCase("//Button[position() > 2 and position() < last()]")]
    [TestCase("//Button[position() = 1 or position() = last()]")]
    [TestCase("//Window[text()='Calculator']")]
    [TestCase("//Window[ends-with(text(), '- Microsoft Edge')]")]
    [TestCase("//Window[starts-with(text(), 'Calc')]")]
    [TestCase("//Window[contains(text(), 'Edge')]")]
    [TestCase("//Button[contains-point(bounds(), point(10, 50))]")]
    [TestCase("//Button[contains(@Name, 'foo')]")]
    [TestCase("//Button[contains(text(), 'bar')]")]
    [TestCase("//*[contains-point(bounds(), point(0, 0)) and @Name='OK']")]
    [TestCase("//frontmost::Button[contains-point(bounds(), point(10, 50))]")]
    [TestCase("//Window//frontmost::Button[@Name='OK']")]
    [TestCase("/frontmost::Button")]
    [TestCase("//Button[at(10, 50)]")]
    [TestCase("//frontmost::Button[at(10, 50)]")]
    [TestCase("[custom=anything]")]
    [TestCase("custom=anything")]
    public void Validate_ValidSelector_DoesNotThrow(string selector)
    {
        Assert.DoesNotThrow(() => SelectorValidator.Validate(selector));
    }
}
