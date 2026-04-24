using FlaUI.Core.WindowsAPI;
using WindowsConductor.DriverFlaUI;

namespace WindowsConductor.DriverFlaUI.Tests;

[TestFixture]
[Category("Unit")]
public class KeyTranslatorTests
{
    [TestCase("ESCAPE", VirtualKeyShort.ESCAPE)]
    [TestCase("escape", VirtualKeyShort.ESCAPE)]
    [TestCase("Escape", VirtualKeyShort.ESCAPE)]
    [TestCase("CONTROL", VirtualKeyShort.CONTROL)]
    [TestCase("ENTER", VirtualKeyShort.ENTER)]
    [TestCase("TAB", VirtualKeyShort.TAB)]
    public void Get_String_TranslatesKeyName(string keyName, VirtualKeyShort expected)
    {
        Assert.That(KeyTranslator.Get(keyName), Is.EqualTo(expected));
    }

    [Test]
    public void Get_InvalidKeyName_ThrowsKeyNotFound()
    {
        Assert.Throws<KeyNotFoundException>(() => KeyTranslator.Get("BOGUSKEY"));
    }

    [Test]
    public void Get_ClientKey_TranslatesCorrectly()
    {
        Assert.That(KeyTranslator.Get(Client.Key.ESCAPE), Is.EqualTo(VirtualKeyShort.ESCAPE));
    }

    [Test]
    public void GetAll_Strings_TranslatesAll()
    {
        var result = KeyTranslator.GetAll(["CONTROL", "KEY_A"]);
        Assert.That(result, Is.EqualTo(new[] { VirtualKeyShort.CONTROL, VirtualKeyShort.KEY_A }));
    }

    [Test]
    public void GetAll_ClientKeys_TranslatesAll()
    {
        var result = KeyTranslator.GetAll([Client.Key.SHIFT, Client.Key.TAB]);
        Assert.That(result, Is.EqualTo(new[] { VirtualKeyShort.SHIFT, VirtualKeyShort.TAB }));
    }

    [Test]
    public void GetAll_EmptyArray_ReturnsEmpty()
    {
        Assert.That(KeyTranslator.GetAll(Array.Empty<string>()), Is.Empty);
    }

    [Test]
    public void KeyNames_ContainsExpectedKeys()
    {
        var names = KeyTranslator.KeyNames;
        Assert.That(names, Does.Contain("escape"));
        Assert.That(names, Does.Contain("control"));
        Assert.That(names, Does.Contain("enter"));
    }
}