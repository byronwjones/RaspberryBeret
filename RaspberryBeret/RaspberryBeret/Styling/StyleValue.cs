namespace RaspberryBeret.Styling;
internal abstract class StyleValue<T> : StyleValue
{
    public required T Value { get; set; }
}

internal abstract class StyleValue { }
