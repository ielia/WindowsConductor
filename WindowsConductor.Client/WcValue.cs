using System.Drawing;
using System.Globalization;
using static WindowsConductor.Client.WcAttrType;

namespace WindowsConductor.Client;

/// <summary>Represents a typed value for a WcElement attribute.</summary>
public record WcValue(WcAttrType Type, object? Value)
{
    private static readonly Dictionary<WcAttrType, Type> ExpectedValueTypes = new()
    {
        [BoolValue] = typeof(bool),
        [IntValue] = typeof(int),
        [DoubleValue] = typeof(double),
        [LongValue] = typeof(long),
        [DateOnlyValue] = typeof(DateOnly),
        [DateTimeValue] = typeof(DateTime),
        [TimeOnlyValue] = typeof(TimeOnly),
        [TimeSpanValue] = typeof(TimeSpan),
        [StringValue] = typeof(string),
        [PointValue] = typeof(Point),
        [RectangleValue] = typeof(Rectangle),
    };

    public object? Value { get; init; } = ValidateValue(Type, Value);

    private static object? ValidateValue(WcAttrType type, object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (type == NullValue)
        {
            throw new ArgumentException(
                $"WcAttrType.NullValue requires a null Value, but got '{value}' ({value.GetType().Name}).",
                nameof(value));
        }

        // StringValue accepts any CLR type — GetAs* methods use ToString() fallbacks.
        if (type == StringValue)
        {
            return value;
        }

        if (type == ListValue)
        {
            if (value is not IReadOnlyList<WcValue>)
                throw new ArgumentException(
                    $"WcAttrType.ListValue expects an IReadOnlyList<WcValue> Value, but got '{value}' ({value.GetType().Name}).",
                    nameof(value));
            return value;
        }

        if (type == MapValue)
        {
            if (value is not IReadOnlyDictionary<WcValue, WcValue>)
                throw new ArgumentException(
                    $"WcAttrType.MapValue expects an IReadOnlyDictionary<WcValue, WcValue> Value, but got '{value}' ({value.GetType().Name}).",
                    nameof(value));
            return value;
        }

        if (type == ElementValue)
        {
            if (value is not WcElement)
                throw new ArgumentException(
                    $"WcAttrType.ElementValue expects a WcElement Value, but got '{value}' ({value.GetType().Name}).",
                    nameof(value));
            return value;
        }

        if (ExpectedValueTypes.TryGetValue(type, out var expected) && !expected.IsInstanceOfType(value))
        {
            throw new ArgumentException(
                $"WcAttrType.{type} expects a {expected.Name} Value, but got '{value}' ({value.GetType().Name}).",
                nameof(value));
        }

        return value;
    }

    private static readonly HashSet<WcAttrType> NumericTypes = [DoubleValue, IntValue, LongValue, NullValue];

    private T? ConvertNumericValue<T>(WcAttrType dest, Func<string, T?> converter)
    {
        try
        {
            return NumericTypes.Contains(Type) || Value == null ? (T?)Value : converter(Value?.ToString() ?? "");
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, dest, e);
        }
    }

    public bool? GetAsBool()
    {
        try
        {
            return Type == BoolValue || Value == null
                ? (bool?)Value
                : NumericTypes.Contains(Type)
                    ? (double?)Value != 0
                    : bool.Parse(Value?.ToString() ?? "");
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, BoolValue, e);
        }
    }

    public DateOnly? GetAsDateOnly()
    {
        try
        {
            return Type == DateOnlyValue || Type == NullValue || Value == null
                ? (DateOnly?)Value
                : Type == DateTimeValue
                    ? DateOnly.FromDateTime((DateTime)Value)
                    : DateOnly.Parse(Value?.ToString() ?? "", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, DateOnlyValue, e);
        }
    }

    public DateTime? GetAsDateTime()
    {
        try
        {
            return Type == DateTimeValue || Type == NullValue || Value == null
                ? (DateTime?)Value
                : Type == DateOnlyValue
                    ? ((DateOnly)Value).ToDateTime(TimeOnly.MinValue)
                    : DateTime.Parse(Value?.ToString() ?? "", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, DateTimeValue, e);
        }
    }

    public double? GetAsDouble() => ConvertNumericValue(DoubleValue, s => double.Parse(s, CultureInfo.InvariantCulture));

    public int? GetAsInt() => ConvertNumericValue(IntValue, s => int.Parse(s, CultureInfo.InvariantCulture));

    public long? GetAsLong() => ConvertNumericValue(LongValue, s => long.Parse(s, CultureInfo.InvariantCulture));

    public WcElement? GetAsElement() => Value as WcElement;

    public IReadOnlyList<WcValue> GetAsList() =>
        Value as IReadOnlyList<WcValue>
        ?? throw new InvalidCastException($"Cannot convert WcValue({Type}) to IReadOnlyList<WcValue>.");

    public IReadOnlyList<T> GetAsList<T>() =>
        GetAsList().Select(Unwrap<T>).ToList();

    public IReadOnlyDictionary<WcValue, WcValue> GetAsMap() =>
        Value as IReadOnlyDictionary<WcValue, WcValue>
        ?? throw new InvalidCastException($"Cannot convert WcValue({Type}) to IReadOnlyDictionary<WcValue, WcValue>.");

    public IReadOnlyDictionary<TKey, TValue> GetAsMap<TKey, TValue>() where TKey : notnull =>
        GetAsMap().ToDictionary(kv => Unwrap<TKey>(kv.Key), kv => Unwrap<TValue>(kv.Value));

    private static T Unwrap<T>(WcValue v) =>
        v.Value is T t ? t : throw new InvalidCastException(
            $"Cannot unwrap WcValue({v.Type}, {v.Value}) as {typeof(T).Name}.");

    public Point? GetAsPoint() => Value as Point?;

    public Rectangle? GetAsRectangle() => Value as Rectangle?;

    public string? GetAsString() => Value?.ToString();

    public TimeOnly? GetAsTimeOnly()
    {
        try
        {
            return Type == TimeOnlyValue || Type == NullValue || Value == null
                ? (TimeOnly?)Value
                : Type == DateTimeValue
                    ? TimeOnly.FromDateTime((DateTime)Value)
                    : Type == DateOnlyValue
                        ? TimeOnly.MinValue
                        : TimeOnly.Parse(Value?.ToString() ?? "", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, TimeOnlyValue, e);
        }
    }

    public TimeSpan? GetAsTimeSpan()
    {
        try
        {
            return Type == TimeSpanValue || Type == NullValue || Value == null
                ? (TimeSpan?)Value
                : TimeSpan.Parse(Value?.ToString() ?? "", CultureInfo.InvariantCulture);
        }
        catch (FormatException e)
        {
            throw new UnconvertibleValueTypeException(Type, TimeSpanValue, e);
        }
    }
}

/// <summary>Thrown when a value is requested to have a different unconvertible type.</summary>
public sealed class UnconvertibleValueTypeException(WcAttrType from, WcAttrType to, Exception innerException)
    : WcException($"Value with type {from} cannot be converted to {to}", innerException);
