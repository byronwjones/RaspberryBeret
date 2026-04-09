using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal class ColorStyleValue : StyleValue<string>
{
    public static ColorStyleValue? FromString(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        value = value.Trim();
        //drop semicolon if necessary
        if (value[value.Length - 1] == ';')
        {
            value = value.Remove(value.Length - 1).Trim();
        }
        //drop hash if there
        if (value[0] == '#')
        {
            value = value.Remove(0, 1);
        }

        //handle color code
        if (Regex.IsMatch(value, @"^([0-9a-f]{6}|[0-9a-f]{3})$", RegexOptions.IgnoreCase))
        {
            //expand 3 character code
            if (value.Length == 3)
            {
                value = value[0].ToString() + value[0] +
                    value[1] + value[1] +
                    value[2] + value[2];
            }

            return new ColorStyleValue
            {
                Value = value
            };
        }

        //handle color name
        value = WebColor.GetRGB(value);
        if (value == null) { return null; }//color input was not valid

        return new ColorStyleValue
        {
            Value = value
        };
    }
}
