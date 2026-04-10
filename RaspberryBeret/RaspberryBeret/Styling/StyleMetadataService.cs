using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal static class StyleMetadataService
{
    static StyleMetadataService()
    {
        allInfo = [
            new PdfmlBorder(),
            new BorderTop(),
            new BorderRight(),
            new BorderBottom(),
            new BorderLeft(),
            new BorderWidth(),
            new BorderTopWidth(),
            new BorderRightWidth(),
            new BorderBottomWidth(),
            new BorderLeftWidth(),
            new PdfmlBorderStyle(),
            new BorderTopStyle(),
            new BorderRightStyle(),
            new BorderBottomStyle(),
            new BorderLeftStyle(),
            new BorderColor(),
            new BorderTopColor(),
            new BorderRightColor(),
            new BorderBottomColor(),
            new BorderLeftColor(),
            new BackgroundColor(),
            new ColorStyle(),
            new FontFamily(),
            new FontSize(),
            new FontWeight(),
            new FontStyle(),
            new TextDecoration(),
            new LineHeight(),
            new ListStyleType(),
            new Margin(),
            new MarginTop(),
            new MarginRight(),
            new MarginBottom(),
            new MarginLeft(),
            new Padding(),
            new PaddingTop(),
            new PaddingRight(),
            new PaddingBottom(),
            new PaddingLeft(),
            new TextIndent(),
            new TextAlign(),
            new VerticalAlign(),
            new Width(),
            new MaxWidth(),
            new Height(),
            new MaxHeight()
            ];
    }

    public static StyleMetadata? GetStyleMetadata(string? styleName)
    {
        if (styleName == null) { return null; }
        styleName = styleName.ToLower();

        return allInfo.FirstOrDefault(m => m.Name == styleName);
    }

    private static readonly StyleMetadata[] allInfo;
}

internal class PdfmlBorder : StyleMetadata
{
    public PdfmlBorder()
    {
        Name = "border";
        Inheritable = false;
    }

    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);
        //ensure no multiple space gaps
        value = Regex.Replace(value, @"\s+", " ", RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) { return styles; }

        string[] vals = splitStyleValue(value);
        if (vals.Length > 3) { return styles; }//not legal
                                               //one value (width)
        if (vals.Length == 1)
        {
            appendStylesToStyleList("border-width", value, specificity, important, styles);

            if (styles.Count != 4) { return styles; }
        }
        //two values (width, style)
        else if (vals.Length == 2)
        {
            appendStylesToStyleList("border-width", vals[0], specificity, important, styles);
            appendStylesToStyleList("border-style", vals[1], specificity, important, styles);

            if (styles.Count != 8) { return new List<Style>(); }
        }
        //three values (width, style, color)
        else if (vals.Length == 3)
        {
            appendStylesToStyleList("border-width", vals[0], specificity, important, styles);
            appendStylesToStyleList("border-style", vals[1], specificity, important, styles);
            appendStylesToStyleList("border-color", vals[2], specificity, important, styles);

            if (styles.Count != 12) { return new List<Style>(); }
        }

        return styles;
    }
}
internal class BorderTop : BorderX
{
    public BorderTop()
    {
        Name = "border-top";
        template = "border-top-{0}";
    }
}
internal class BorderRight : BorderX
{
    public BorderRight()
    {
        Name = "border-right";
        template = "border-right-{0}";
    }
}
internal class BorderBottom : BorderX
{
    public BorderBottom()
    {
        Name = "border-bottom";
        template = "border-bottom-{0}";
    }
}
internal class BorderLeft : BorderX
{
    public BorderLeft()
    {
        Name = "border-left";
        template = "border-left-{0}";
    }
}
internal abstract class BorderX : StyleMetadata
{
    public BorderX()
    {
        Inheritable = false;
    }

    protected string template = string.Empty;

    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);
        //ensure no multiple space gaps
        value = Regex.Replace(value, @"\s+", " ", RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) { return styles; }

        string[] vals = splitStyleValue(value);
        if (vals.Length > 3) { return styles; }//not legal
                                               //one value (width)
        if (vals.Length == 1)
        {
            appendStylesToStyleList(string.Format(template, "width"),
                value, specificity, important, styles);

            if (styles.Count == 0) { return styles; }
        }
        //two values (width, style)
        else if (vals.Length == 2)
        {
            appendStylesToStyleList(string.Format(template, "width"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "style"),
                vals[1], specificity, important, styles);

            if (styles.Count != 2) { return new List<Style>(); }
        }
        //three values (width, style, color)
        else if (vals.Length == 3)
        {
            appendStylesToStyleList(string.Format(template, "width"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "style"),
                vals[1], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "color"),
                vals[2], specificity, important, styles);

            if (styles.Count != 3) { return new List<Style>(); }
        }

        return styles;
    }
}

internal class BorderColor : FourSizesStyle
{
    public BorderColor()
    {
        Name = "border-color";
        Inheritable = false;

        template = "border-{0}-color";
    }
}
internal class BorderTopColor : BorderXColor
{
    public BorderTopColor()
    {
        Name = "border-top-color";
    }
}
internal class BorderRightColor : BorderXColor
{
    public BorderRightColor()
    {
        Name = "border-right-color";
    }
}
internal class BorderBottomColor : BorderXColor
{
    public BorderBottomColor()
    {
        Name = "border-bottom-color";
    }
}
internal class BorderLeftColor : BorderXColor
{
    public BorderLeftColor()
    {
        Name = "border-left-color";
    }
}
internal abstract class BorderXColor : ColourStyle
{
    public BorderXColor()
    {
        Inheritable = false;
    }
}

internal class BorderWidth : FourSizesStyle
{
    public BorderWidth()
    {
        Name = "border-width";
        Inheritable = false;

        template = "border-{0}-width";
    }
}
internal class BorderTopWidth : BorderXWidth
{
    public BorderTopWidth()
    {
        Name = "border-top-width";
    }
}
internal class BorderRightWidth : BorderXWidth
{
    public BorderRightWidth()
    {
        Name = "border-right-width";
    }
}
internal class BorderBottomWidth : BorderXWidth
{
    public BorderBottomWidth()
    {
        Name = "border-bottom-width";
    }
}
internal class BorderLeftWidth : BorderXWidth
{
    public BorderLeftWidth()
    {
        Name = "border-left-width";
    }
}
internal abstract class BorderXWidth : NumericStyle
{
    public BorderXWidth()
    {
        Inheritable = false;
    }
}

internal class PdfmlBorderStyle : FourSizesStyle
{
    public PdfmlBorderStyle()
    {
        Name = "border-style";
        Inheritable = false;

        template = "border-{0}-style";
    }
}
internal class BorderTopStyle : BorderXStyle
{
    public BorderTopStyle()
    {
        Name = "border-top-style";
    }
}
internal class BorderRightStyle : BorderXStyle
{
    public BorderRightStyle()
    {
        Name = "border-right-style";
    }
}
internal class BorderBottomStyle : BorderXStyle
{
    public BorderBottomStyle()
    {
        Name = "border-bottom-style";
    }
}
internal class BorderLeftStyle : BorderXStyle
{
    public BorderLeftStyle()
    {
        Name = "border-left-style";
    }
}
internal abstract class BorderXStyle : SetValueOptionsStyle
{
    public BorderXStyle()
    {
        Inheritable = false;

        acceptedValues = new string[] { "dashed", "dotted", "solid" };
    }
}

internal class BackgroundColor : ColourStyle
{
    public BackgroundColor()
    {
        Name = "background-color";
        Inheritable = false;
    }
}

internal class ColorStyle : ColourStyle
{
    public ColorStyle()
    {
        Name = "color";
        Inheritable = true;
    }
}

internal class FontFamily : StyleMetadata
{
    public FontFamily()
    {
        Name = "font-family";
        Inheritable = true;
    }

    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);

        var sv = StringStyleValue.FromString(value);
        if (sv != null && !string.IsNullOrWhiteSpace(sv.Value))
        {
            styles.Add(new Style(this, specificity, important, sv));
        }

        return styles;
    }
}
internal class FontSize : NumericStyle
{
    public FontSize()
    {
        Name = "font-size";
        Inheritable = true;
    }
}
internal class FontWeight : SetValueOptionsStyle
{
    public FontWeight()
    {
        Name = "font-weight";
        Inheritable = true;

        acceptedValues = ["normal", "bold"];
    }
}
internal class FontStyle : SetValueOptionsStyle
{
    public FontStyle()
    {
        Name = "font-style";
        Inheritable = true;

        acceptedValues = ["normal", "italic"];
    }
}
internal class TextDecoration : SetValueOptionsStyle
{
    public TextDecoration()
    {
        Name = "text-decoration";
        Inheritable = true;

        acceptedValues = ["none", "underline"];
    }
}

internal class LineHeight : NumericStyle
{
    public LineHeight()
    {
        Name = "line-height";
        Inheritable = true;
    }
}

internal class ListStyleType : SetValueOptionsStyle
{
    public ListStyleType()
    {
        Name = "list-style-type";
        Inheritable = true;

        acceptedValues = new string[] { "disc", "square", "none" };
    }
}

internal class Margin : FourSizesStyle
{
    public Margin()
    {
        Name = "margin";
        Inheritable = false;

        template = "margin-{0}";
    }
}
internal class MarginTop : MarginX
{
    public MarginTop()
    {
        Name = "margin-top";
    }
}
internal class MarginRight : MarginX
{
    public MarginRight()
    {
        Name = "margin-right";
    }
}
internal class MarginBottom : MarginX
{
    public MarginBottom()
    {
        Name = "margin-bottom";
    }
}
internal class MarginLeft : MarginX
{
    public MarginLeft()
    {
        Name = "margin-left";
    }
}
internal abstract class MarginX : PercentableStyle
{
    public MarginX()
    {
        Inheritable = false;
    }
}

internal class Padding : FourSizesStyle
{
    public Padding()
    {
        Name = "padding";
        Inheritable = false;

        template = "padding-{0}";
    }
}
internal class PaddingTop : PaddingX
{
    public PaddingTop()
    {
        Name = "padding-top";
    }
}
internal class PaddingRight : PaddingX
{
    public PaddingRight()
    {
        Name = "padding-right";
    }
}
internal class PaddingBottom : PaddingX
{
    public PaddingBottom()
    {
        Name = "padding-bottom";
    }
}
internal class PaddingLeft : PaddingX
{
    public PaddingLeft()
    {
        Name = "padding-left";
    }
}
internal abstract class PaddingX : PercentableStyle
{
    public PaddingX()
    {
        Inheritable = false;
    }
}

internal class TextIndent : NumericStyle
{
    public TextIndent()
    {
        Name = "text-indent";
        Inheritable = true;
    }
}

internal class TextAlign : SetValueOptionsStyle
{
    public TextAlign()
    {
        Name = "text-align";
        Inheritable = true;

        acceptedValues = ["left", "center", "right", "justify"];
    }
}
internal class VerticalAlign : SetValueOptionsStyle
{
    public VerticalAlign()
    {
        Name = "vertical-align";
        Inheritable = true;

        acceptedValues = ["top", "middle", "bottom"];
    }
}

internal class Width : PercentableStyle
{
    public Width()
    {
        Name = "width";
        Inheritable = false;
    }
}
internal class MaxWidth : PercentableStyle
{
    public MaxWidth()
    {
        Name = "max-width";
        Inheritable = false;
    }
}

internal class Height : NumericStyle
{
    public Height()
    {
        Name = "height";
        Inheritable = false;
    }
}
internal class MaxHeight : NumericStyle
{
    public MaxHeight()
    {
        Name = "max-height";
        Inheritable = false;
    }
}

internal abstract class ColourStyle : StyleMetadata
{
    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);

        var sv = ColorStyleValue.FromString(value);
        if (sv != null)
        {
            styles.Add(new Style(this, specificity, important, sv));
        }

        return styles;
    }
}
internal abstract class FourSizesStyle : StyleMetadata
{
    protected string template = string.Empty;

    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);
        //ensure no multiple space gaps
        value = Regex.Replace(value, @"\s+", " ", RegexOptions.IgnoreCase);
        if (string.IsNullOrWhiteSpace(value)) { return styles; }

        string[] vals = splitStyleValue(value);
        if (vals.Length > 4) { return styles; }//not legal
                                               //one value (top/right/bottom/left)
        if (vals.Length == 1)
        {
            appendStylesToStyleList(string.Format(template, "top"),
                value, specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "right"),
                value, specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "bottom"),
                value, specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "left"),
                 value, specificity, important, styles);
        }
        //two values (top/bottom, left/right)
        else if (vals.Length == 2)
        {
            appendStylesToStyleList(string.Format(template, "top"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "bottom"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "right"),
                vals[1], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "left"),
                vals[1], specificity, important, styles);
        }
        //three values (top, left/right, bottom)
        else if (vals.Length == 3)
        {
            appendStylesToStyleList(string.Format(template, "top"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "left"),
                vals[1], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "right"),
                vals[1], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "bottom"),
                vals[2], specificity, important, styles);
        }
        //four values (top, right, bottom, left)
        else if (vals.Length == 4)
        {
            appendStylesToStyleList(string.Format(template, "top"),
                vals[0], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "right"),
                vals[1], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "bottom"),
                vals[2], specificity, important, styles);
            appendStylesToStyleList(string.Format(template, "left"),
                vals[3], specificity, important, styles);
        }

        //this must result in 4 styles
        if (styles.Count != 4)
        {
            return new List<Style>();
        }

        return styles;
    }
}
internal abstract class SetValueOptionsStyle : StyleMetadata
{
    protected string[] acceptedValues = [];

    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);

        //this value only valid if exists and is one of the accepted values
        var sv = StringStyleValue.FromString(value);
        if (sv != null && !string.IsNullOrWhiteSpace(sv.Value))
        {
            sv.Value = value.ToLower();
            if (acceptedValues.Contains(sv.Value))
            {
                styles.Add(new Style(this, specificity, important, sv));
            }
        }

        return styles;
    }
}
internal abstract class NumericStyle : StyleMetadata
{
    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);

        //percentage values not allowed
        var sv = NumericStyleValue.FromString(value);
        if (sv != null && sv.Units != StyleUnit.Percent)
        {
            styles.Add(new Style(this, specificity, important, sv));
        }

        return styles;
    }
}
internal abstract class PercentableStyle : StyleMetadata
{
    public override List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        List<Style> styles = new List<Style>();
        bool important;
        value = removeValueEnding(value, out important);

        var sv = NumericStyleValue.FromString(value);
        if (sv != null)
        {
            styles.Add(new Style(this, specificity, important, sv));
        }

        return styles;
    }
}
