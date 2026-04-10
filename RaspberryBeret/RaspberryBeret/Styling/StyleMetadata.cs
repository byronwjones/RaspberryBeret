using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal abstract class StyleMetadata
{
    public string Name { get; protected set; } = string.Empty;
    public bool Inheritable { get; protected set; }

    /// <summary>
    /// Expands 'shorthand' styles. This method would, for example, expand the padding style to padding-left,
    /// padding-right, padding-top, and padding-bottom. For styles that are not shorthand, simply returns the input style
    /// </summary>
    /// <param name="value">Style value, e.g. '1in 2in 3in 4in'</param>
    /// <param name="specificity">Value signifying the importance to give the resulting styles, to resolve conflicting styles</param>
    /// <returns>Expanded list of styles</returns>
    /// <exception cref="NotImplementedException"></exception>
    public virtual List<Style> ExtractStylesFromValue(string value, int specificity)
    {
        throw new NotImplementedException();
    }

    protected string removeValueEnding(string value, out bool valueIsImportant)
    {
        valueIsImportant = Regex.IsMatch(value, @"\s\!important\s*;$", RegexOptions.IgnoreCase);
        return Regex.Replace(value, @"(\s\!important)?\s*;$", string.Empty, RegexOptions.IgnoreCase).Trim();
    }

    protected List<Style> appendStylesToStyleList(string styleName, string value,
        int specificity, bool important, List<Style> styles)
    {
        var styleMeta = StyleMetadataService.GetStyleMetadata(styleName);
        if(styleMeta is not null)
        {
            styles.AddRange(
                styleMeta.ExtractStylesFromValue(value, specificity)
                .Select(s =>
                {
                    s.Important = important;
                    return s;
                })
            );
        }
        return styles;
    }

    protected string[] splitStyleValue(string value)
    {
        List<string> values = new List<string>();
        if (string.IsNullOrWhiteSpace(value)) { return values.ToArray(); }

        while (!string.IsNullOrWhiteSpace(value))
        {
            var mth = Regex.Match(value, "^((#?[0-9a-z\\-_\\.]+%?)|(\\'[^\\']+\\')|(\\\"[^\\\"]+\\\"))\\s*", RegexOptions.IgnoreCase);
            if (!mth.Success) { break; }

            values.Add(mth.Value.Trim());
            //remove individual style value from value string
            if (mth.Length < value.Length)
            {
                value = value.Substring(mth.Length);
            }
            else //value string completely consumed
            {
                value = string.Empty;
            }
        }

        return values.ToArray();
    }
}
