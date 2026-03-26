using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Resolves normalised property keys to string values on an <see cref="AutomationElement"/>.
/// Single source of truth for attribute resolution — used by <see cref="SelectorEngine"/>,
/// <see cref="XPathEngine"/>, and <see cref="AppManager.GetAttribute"/>.
/// </summary>
internal static class ElementProperties
{
    // Aliases map common shorthand keys to their canonical names.
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["text"] = "name",
        ["class"] = "classname",
        ["type"] = "controltype",
    };

    // All supported canonical property keys (lower-case).
    private static readonly HashSet<string> SupportedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "acceleratorkey",
        "accesskey",
        "automationid",
        "boundingrectangle",
        "classname",
        "controltype",
        "frameworkid",
        "haskeyboardfocus",
        "helptext",
        "iscontentelement",
        "iscontrolelement",
        "isenabled",
        "iskeyboardfocusable",
        "isoffscreen",
        "ispassword",
        "isrequiredforform",
        "itemstatus",
        "itemtype",
        "localizedcontroltype",
        "name",
        "nativewindowhandle",
        "orientation",
        "processid",
    };

    internal static bool IsSupported(string key) =>
        SupportedKeys.Contains(Normalize(key));

    internal static string Normalize(string key)
    {
        key = key.ToLowerInvariant();
        return Aliases.TryGetValue(key, out var canonical) ? canonical : key;
    }

    internal static string? Resolve(AutomationElement el, string key)
    {
        try
        {
            return Normalize(key) switch
            {
                "acceleratorkey" => el.Properties.AcceleratorKey.ValueOrDefault,
                "accesskey" => el.Properties.AccessKey.ValueOrDefault,
                "automationid" => el.AutomationId,
                "boundingrectangle" => el.BoundingRectangle.ToString(),
                "classname" => el.ClassName,
                "controltype" => el.ControlType.ToString(),
                "frameworkid" => el.Properties.FrameworkId.ValueOrDefault,
                "haskeyboardfocus" => el.Properties.HasKeyboardFocus.ValueOrDefault.ToString().ToLowerInvariant(),
                "helptext" => el.Properties.HelpText.ValueOrDefault,
                "iscontentelement" => el.Properties.IsContentElement.ValueOrDefault.ToString().ToLowerInvariant(),
                "iscontrolelement" => el.Properties.IsControlElement.ValueOrDefault.ToString().ToLowerInvariant(),
                "isenabled" => el.IsEnabled.ToString().ToLowerInvariant(),
                "iskeyboardfocusable" => el.Properties.IsKeyboardFocusable.ValueOrDefault.ToString().ToLowerInvariant(),
                "isoffscreen" => el.IsOffscreen.ToString().ToLowerInvariant(),
                "ispassword" => el.Properties.IsPassword.ValueOrDefault.ToString().ToLowerInvariant(),
                "isrequiredforform" => el.Properties.IsRequiredForForm.ValueOrDefault.ToString().ToLowerInvariant(),
                "itemstatus" => el.Properties.ItemStatus.ValueOrDefault,
                "itemtype" => el.Properties.ItemType.ValueOrDefault,
                "localizedcontroltype" => el.Properties.LocalizedControlType.ValueOrDefault,
                "name" => el.Name,
                "nativewindowhandle" => el.Properties.NativeWindowHandle.ValueOrDefault.ToString(),
                "orientation" => el.Properties.Orientation.ValueOrDefault.ToString(),
                "processid" => el.Properties.ProcessId.ValueOrDefault.ToString(),
                _ => null
            };
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
    }
}
