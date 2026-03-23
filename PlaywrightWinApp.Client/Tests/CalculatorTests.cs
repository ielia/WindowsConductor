using NUnit.Framework;

namespace PlaywrightWinApp.Client.Tests;

/// <summary>
/// Integration tests for the Windows 11 Calculator application.
/// Runs against the FlaUI driver on port 8765.
/// If the driver is not running its fixture is automatically skipped.
///
/// ── Starting the driver ──────────────────────────────────────────────────────
///   dotnet run --project PlaywrightWinApp.DriverFlaUI   # ws://localhost:8765/
///
/// ── AutomationId quick-reference (Win 11 Calculator) ─────────────────────────
///   num0Button … num9Button   Digit buttons
///   plusButton / minusButton / multiplyButton / divideButton
///   equalButton               Equals / compute result  (=)
///   clearButton               Clear all  (AC)
///   clearEntryButton          Clear current entry  (CE)
///   CalculatorResults         Result display
/// ─────────────────────────────────────────────────────────────────────────────
/// </summary>
[TestFixtureSource(nameof(DriverUris))]
[Category("Integration")]
public sealed class CalculatorTests : WinAppTest
{
    // ── Driver endpoints ──────────────────────────────────────────────────────

    public static IEnumerable<TestFixtureData> DriverUris()
    {
        yield return new TestFixtureData("ws://localhost:8765/").SetArgDisplayNames("DriverFlaUI");
    }

    // ── Configuration ─────────────────────────────────────────────────────────

    private readonly string _driverUri;

    public CalculatorTests(string driverUri) => _driverUri = driverUri;

    protected override string DriverUri => _driverUri;
    protected override string AppPath => "explorer.exe";
    protected override string[]? AppArgs => ["shell:appsfolder\\Microsoft.WindowsCalculator_8wekyb3d8bbwe!App"];
    protected override string? DetachedTitleRegex => "^Calculator$";
    protected override uint? MainWindowTimeout => 1000;

    [SetUp]
    public async Task ClearState()
    {
        await App.GetByXPath("//Button[@AutomationId=('clearButton','clearEntryButton')]").ClickAsync();
        await Task.Delay(150);
    }

    // ── Tests using AutomationId ──────────────────────────────────────────────

    [Test]
    public async Task Addition_ByAutomationId()
    {
        await App.GetByAutomationId("num2Button").ClickAsync();
        await App.GetByAutomationId("plusButton").ClickAsync();
        await App.GetByAutomationId("num3Button").ClickAsync();
        await App.GetByAutomationId("equalButton").ClickAsync();

        var result = await App.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("5"),
            $"2 + 3 should equal 5.  Display shows: '{result}'");
    }

    [Test]
    public async Task Subtraction_ByAutomationId()
    {
        await App.GetByAutomationId("num9Button").ClickAsync();
        await App.GetByAutomationId("minusButton").ClickAsync();
        await App.GetByAutomationId("num3Button").ClickAsync();
        await App.GetByAutomationId("equalButton").ClickAsync();

        var result = await App.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("6"),
            $"9 − 3 should equal 6.  Display shows: '{result}'");
    }

    [Test]
    public async Task Multiplication_ByAutomationId()
    {
        await App.GetByAutomationId("num4Button").ClickAsync();
        await App.GetByAutomationId("multiplyButton").ClickAsync();
        await App.GetByAutomationId("num7Button").ClickAsync();
        await App.GetByAutomationId("equalButton").ClickAsync();

        var result = await App.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("28"),
            $"4 × 7 should equal 28.  Display shows: '{result}'");
    }

    [Test]
    public async Task Division_ByAutomationId()
    {
        await App.GetByAutomationId("num8Button").ClickAsync();
        await App.GetByAutomationId("divideButton").ClickAsync();
        await App.GetByAutomationId("num2Button").ClickAsync();
        await App.GetByAutomationId("equalButton").ClickAsync();

        var result = await App.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("4"),
            $"8 ÷ 2 should equal 4.  Display shows: '{result}'");
    }

    [Test]
    public async Task Buttons_AreEnabled_ByAutomationId()
    {
        foreach (var id in new[] { "num0Button", "num5Button", "num9Button",
                                    "plusButton", "equalButton", "clearButton" })
        {
            bool enabled = await App.GetByAutomationId(id).IsEnabledAsync();
            Assert.That(enabled, Is.True, $"Button '{id}' should be enabled.");
        }
    }

    // ── Tests using Name / Text ───────────────────────────────────────────────

    [Test]
    public async Task Addition_ByText()
    {
        await App.GetByText("Five").ClickAsync();
        await App.GetByText("Plus").ClickAsync();
        await App.GetByText("Six").ClickAsync();
        await App.GetByText("Equals").ClickAsync();

        var result = await App.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("11"),
            $"5 + 6 should equal 11.  Display shows: '{result}'");
    }

    [Test]
    public async Task ButtonIsVisible_ByText()
    {
        bool visible = await App.GetByText("One").IsVisibleAsync();
        Assert.That(visible, Is.True, "Button 'One' should be visible.");
    }

    // ── Tests using XPath ─────────────────────────────────────────────────────

    [Test]
    public async Task Addition_ByXPath()
    {
        await App.GetByXPath("//Button[@AutomationId='num1Button']").ClickAsync();
        await App.GetByXPath("//Button[@AutomationId='plusButton']").ClickAsync();
        await App.GetByXPath("//Button[@AutomationId='num2Button']").ClickAsync();
        await App.GetByXPath("//Button[@AutomationId='equalButton']").ClickAsync();

        var result = await App.GetByXPath("//*[@AutomationId='CalculatorResults']").GetTextAsync();
        Assert.That(result, Does.Contain("3"),
            $"1 + 2 should equal 3.  Display shows: '{result}'");
    }

    [Test]
    public async Task FindAllButtons_ByXPath()
    {
        var buttons = await App.GetByXPath("//Button").GetAllElementsAsync();
        Assert.That(buttons.Count, Is.GreaterThan(10),
            "Calculator should expose more than 10 Button elements.");
    }

    [Test]
    public async Task FindButtonByName_ByXPath()
    {
        bool enabled = await App.GetByXPath("//Button[@Name='Seven']").IsEnabledAsync();
        Assert.That(enabled, Is.True, "Button with Name='Seven' should be enabled.");
    }

    [Test]
    public async Task FindDisplay_ByXPath_AnyType()
    {
        bool visible = await App
            .GetByXPath("//*[@AutomationId='CalculatorResults']")
            .IsVisibleAsync();
        Assert.That(visible, Is.True, "Result display should be visible.");
    }

    [Test]
    public async Task GetBoundingRect_ResultDisplay()
    {
        var rect = await App.GetByAutomationId("CalculatorResults").GetBoundingRectAsync();
        Assert.That(rect.Width,  Is.GreaterThan(50), "Result display width > 50 px.");
        Assert.That(rect.Height, Is.GreaterThan(10), "Result display height > 10 px.");
    }

    // ── Compound selector ─────────────────────────────────────────────────────

    [Test]
    public async Task CompoundSelector_AutomationIdAndType()
    {
        bool enabled = await App
            .Locator("[automationid=num5Button]&&type=Button")
            .IsEnabledAsync();
        Assert.That(enabled, Is.True,
            "Compound selector [automationid=num5Button]&&type=Button should resolve.");
    }
}
