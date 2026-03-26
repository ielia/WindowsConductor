using FlaUI.Core.AutomationElements;
using FlaUI.Core.Exceptions;

namespace WindowsConductor.DriverFlaUI;

/// <summary>
/// Resolves normalised property keys to values on an <see cref="AutomationElement"/>.
/// Shared by <see cref="SelectorEngine"/> and <see cref="XPathEngine"/>.
/// </summary>
internal static class ElementProperties
{
    internal static string? Resolve(AutomationElement el, string key)
    {
        try
        {
            return key switch
            {
                "automationid" => el.AutomationId,
                "name" or "text" => el.Name,
                "classname" or "class" => el.ClassName,
                "type" or "controltype" => el.ControlType.ToString(),
                _ => null
            };
        }
        catch (PropertyNotSupportedException)
        {
            return null;
        }
    }
}
