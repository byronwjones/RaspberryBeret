using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal class NumericStyleValue : StyleValue<double>
{
    public StyleUnit Units { get; set; }

    public static NumericStyleValue? FromString(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        value = value.Trim();
        //drop semicolon if necessary
        if (value[value.Length - 1] == ';')
        {
            value = value.Remove(value.Length - 1).Trim();
        }

        var mth = Regex.Match(value, @"^([0-9]+\.[0-9]+)|([0-9]+\.?)|(\.[0-9]+)");
        //there must be a number here
        if (!mth.Success) { return null; }

        var valuePart = mth.Value;
        var unitPart = string.Empty;
        int unitPos = mth.Index + mth.Length;
        if (unitPos < value.Length)
        {
            unitPart = value.Substring(unitPos).ToLower();
        }

        var result = new NumericStyleValue{ Value = Convert.ToDouble(valuePart) };

        switch (unitPart)
        {
            case "pt":
                result.Units = StyleUnit.Point;
                break;
            case "mm":
                result.Units = StyleUnit.Millimeter;
                break;
            case "cm":
                result.Units = StyleUnit.Centimeter;
                break;
            case "in":
                result.Units = StyleUnit.Inch;
                break;
            case "%":
                result.Units = StyleUnit.Percent;
                break;
            default:
                result.Units = StyleUnit.Point;
                break;
        }

        return result;
    }

    public double GetValueInInches(double availableWidthInInches = 0.0)
    {
        if (this.Value == 0 || this.Units == StyleUnit.Inch) { return this.Value; }

        double result = 0;
        switch (this.Units)
        {
            case StyleUnit.Millimeter:
                result = this.Value * 0.0393700787401575d;
                break;
            case StyleUnit.Centimeter:
                result = this.Value * 0.3937007874015748d;
                break;
            case StyleUnit.Percent:
                result = (this.Value * 0.01d) * availableWidthInInches;
                break;
            case StyleUnit.Point:
            default:
                result = this.Value * 0.0138888888888889d;
                break;
        }

        return result;
    }
}