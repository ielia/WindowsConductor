using NUnit.Framework;

namespace WindowsConductor.Client.Tests;

/// <summary>
/// Integration tests for the Windows 11 Calculator application.
/// Runs against the FlaUI driver on port 8765.
/// If the driver is not running its fixture is automatically skipped.
///
/// ── Starting the driver ──────────────────────────────────────────────────────
///   dotnet run --project WindowsConductor.DriverFlaUI   # ws://localhost:8765/
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
public sealed class AttachTests
{
    // ── Driver endpoints ──────────────────────────────────────────────────────

    public static IEnumerable<TestFixtureData> DriverUris()
    {
        yield return new TestFixtureData("ws://localhost:8765/").SetArgDisplayNames("DriverFlaUI");
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private readonly string _driverUri;
    private WcSession _connection = null!;
    private WcApp _calc = null!;

    public AttachTests(string driverUri) => _driverUri = driverUri;

    // ── Fixture setup ─────────────────────────────────────────────────────────

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        try
        {
            _connection = await WcSession.ConnectAsync(_driverUri);
        }
        catch (Exception ex)
        {
            Assert.Ignore($"Driver at {_driverUri} is not available — skipping fixture. ({ex.Message})");
        }
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        if (_calc is not null) await _calc.DisposeAsync();
        if (_connection is not null) await _connection.DisposeAsync();
    }

    // ── Tests using AutomationId ──────────────────────────────────────────────

    [Test]
    public async Task Addition_ByAutomationId()
    {
        _calc = await _connection.AttachAsync("^Calculator$", 1000);

        await _calc.GetByXPath("//Button[@AutomationId=('clearButton','clearEntryButton')]").ClickAsync();
        await Task.Delay(150);
        await _calc.GetByAutomationId("num2Button").ClickAsync();
        await _calc.GetByAutomationId("plusButton").ClickAsync();
        await _calc.GetByAutomationId("num3Button").ClickAsync();
        await _calc.GetByAutomationId("equalButton").ClickAsync();

        var result = await _calc.GetByAutomationId("CalculatorResults").GetTextAsync();
        Assert.That(result, Does.Contain("5"),
            $"2 + 3 should equal 5.  Display shows: '{result}'");
    }
}