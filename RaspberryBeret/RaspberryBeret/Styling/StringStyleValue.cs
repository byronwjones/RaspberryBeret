namespace RaspberryBeret.Styling;
internal class StringStyleValue : StyleValue<string>
{
    public static StringStyleValue? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        value = value.Trim();
        //drop semicolon if necessary
        if (value[value.Length - 1] == ';')
        {
            value = value.Remove(value.Length - 1).Trim();
        }

        //handle apostrophe/double quote wrapping
        if (value.Length > 1 &&
            ((value[0] == '\'' && value[value.Length - 1] == '\'') ||
            (value[0] == '"' && value[value.Length - 1] == '"')))
        {
            value = value.Remove(0, 1)
                .Remove(value.Length - 2);
        }

        return new StringStyleValue
        {
            Value = value
        };
    }
}
