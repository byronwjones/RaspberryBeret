using BWJ.Core;
using RaspberryBeret.Elements;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal static class StyleUtils
{
    /// <summary>
    /// Parses a style declaration string into a list of Style objects
    /// </summary>
    /// <param name="declaration">Style declaration string</param>
    /// <param name="specificity">A weight given to the style objects created denoting
    /// their significance in comparison with other styles of the same type</param>
    /// <returns>A list of Style objects</returns>
    public static List<Style> ParseStyleDeclaration(string declaration, int specificity)
    {
        if (string.IsNullOrWhiteSpace(declaration))
        {
            throw new ArgumentNullException("Style declaration string null or empty");
        }

        declaration = declaration.Trim();
        //strip curly braces from declaration if present
        declaration = Regex.Replace(declaration, @"(^\{\s*)|(\s*\}$)", string.Empty, RegexOptions.IgnoreCase);

        //extract style strings
        var styleStrings = new List<string>();
        while (!string.IsNullOrWhiteSpace(declaration))
        {
            var mth = Regex.Match(declaration,
                "^([a-z]+\\-)*[a-z]+\\s*:\\s*((#?[0-9a-z\\-_\\.]+%?)|(\\'[^\\']+\\')|(\\\"[^\\\"]+\\\"))(\\s+((#?[0-9a-z\\-_\\.]+%?)|(\\'[^\\']+\\')|(\\\"[^\\\"]+\\\")))*(\\s*\\!important)?\\s*;", RegexOptions.IgnoreCase);
            if (!mth.Success) { break; }

            styleStrings.Add(mth.Value.Trim());
            //remove individual style/value pair from declaration
            if (mth.Length < declaration.Length)
            {
                declaration = declaration.Substring(mth.Length).Trim();
            }
            else //declaration string completely consumed
            {
                declaration = string.Empty;
            }
        }

        //parse style strings into objects.  We use a dictionary to store the styles to
        //ensure no duplicates
        Dictionary<string, Style> styles = new Dictionary<string, Style>();
        foreach (var str in styleStrings)
        {
            parseStyle(str, specificity)
                .ForEach(sty =>
                {
                    styles[sty.Name] = sty;
                });
        }

        //convert dictionary into list and return
        return styles.Select(sty =>
        {
            return sty.Value;
        }).ToList();
    }

    /// <summary>
    /// Identifies and extracts CSS-style rule sets from a given string,
    /// parsing them into one or more ruleset objects
    /// </summary>
    /// <param name="input">String containing rule sets</param>
    /// <returns>A list of rule set objects</returns>
    public static List<RuleSet> ParseRuleSets(string input)
    {
        var result = new List<RuleSet>();
        if (string.IsNullOrWhiteSpace(input)) { return result; }

        //extract rule set(s) from input
        var rSets = Regex.Matches(input,
            "((((((\\*?[\\.#])?[a-z0-9_\\-]+)+)|\\*)(:(first|last)-child)?)(\\s*\\>?\\s*(((((\\*?[\\.#])?[a-z0-9_\\-]+)+)|\\*)(:(first|last)-child)?))*)(\\s*\\,\\s*((((((\\*?[\\.#])?[a-z0-9_\\-]+)+)|\\*)(:(first|last)-child)?)(\\s*\\>?\\s*(((((\\*?[\\.#])?[a-z0-9_\\-]+)+)|\\*)(:(first|last)-child)?))*))*\\s*\\{\\s*(([a-z]+\\-)*[a-z]+\\s*:\\s*((#?[0-9a-z\\-_\\.]+%?)|(\\'[^\\']+\\')|(\\\"[^\\\"]+\\\"))(\\s+((#?[0-9a-z\\-_\\.]+%?)|(\\'[^\\']+\\')|(\\\"[^\\\"]+\\\")))*(\\s*\\!important)?\\s*;\\s*)+\\}", RegexOptions.IgnoreCase)
            .Cast<Match>();
        if (!rSets.Any()) { return result; }

        //convert rule sets extracted to objects
        foreach (var rs in rSets)
        {
            result.AddRange(parseRuleSet(rs.Value));
        }

        return result;
    }

    /// <summary>
    /// Extracts style rule sets from any Style elements in the given DOM
    /// </summary>
    /// <param name="dom">DOM from which to extract styles</param>
    /// <returns>A list of rule set objects</returns>
    public static List<RuleSet> ExtractStyleRulesFromDOM(Element dom)
    {
        //get style text content
        var styleText = dom.AllDescendants.Where(c => c.Tag.Name == "textrun" &&
            c.Parent != null && c.Parent.Tag.Name == "style")
            .Select(s => s.InnerText);

        //convert text into rule sets
        List<RuleSet> ruleSets = new List<RuleSet>();
        foreach (var txt in styleText)
        {
            ruleSets.AddRange(ParseRuleSets(txt));
        }

        return ruleSets;
    }

    /// <summary>
    /// Resolves all style references found in the given DOM and applies them to
    /// the appropriate elements therein
    /// </summary>
    /// <param name="dom">DOM on which to apply styles</param>
    public static void ApplyStylesToDOM(Element dom)
    {
        foreach (var elem in dom.AllDescendants)
        {
            //only work on elements with metadata
            if (elem.Metadata == null) { continue; }

            //generate the list of classes associated with this element
            elem.ExtractClassesFromAttributes();

            //apply this element's default styles 
            //e.g. a <strong> element has style font-weight: bold by default
            foreach (var ds in elem.Metadata.DefaultStyles)
            {
                elem.AddExplicitStyle(ds);
            }

            //extract and apply this element's inline styles
            var strStyle = elem.GetAttributeValue("style");
            if (!string.IsNullOrWhiteSpace(strStyle))
            {
                var inlineStyles = ParseStyleDeclaration(strStyle, 1000);
                foreach (var iStyle in inlineStyles)
                {
                    elem.AddExplicitStyle(iStyle);
                }
            }
        }

        //apply styles from style rules to the appropriate elements
        var allStyleRules = ExtractStyleRulesFromDOM(dom);
        foreach (var sr in allStyleRules)
        {
            var applicableElements = sr.Selector.ApplySelector(dom);
            foreach (var ae in applicableElements)
            {
                foreach (var ruleStyle in sr.Styles)
                {
                    ae.AddExplicitStyle(ruleStyle);
                }
            }
        }
    }

    /// <summary>
    /// Parses a style string into one or more Style objects
    /// </summary>
    /// <param name="s">Style string</param>
    /// <param name="specificity">A weight given to the style objects created denoting
    /// their significance in comparison with other styles of the same type</param>
    /// <returns>Style object list</returns>
    private static List<Style> parseStyle(string s, int specificity)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            throw new ArgumentNullException("Style string null or empty");
        }

        s = s.Trim();
        //divide style into name/value
        int separatorIndex = s.IndexOf(':');
        if (separatorIndex < 0 || separatorIndex > (s.Length - 2)) //invalid style
        {
            return new List<Style>();
        }
        string name = s.Remove(separatorIndex).Trim();
        string value = s.Substring(separatorIndex + 1).Trim();

        //get metadata, which can validate and properly format the string value
        var metadata = StyleInfoCenter.GetStyleMetadata(name);
        if (metadata == null) { return new List<Style>(); }

        return metadata.ExtractStylesFromValue(value, specificity);
    }

    /// <summary>
    /// Parses a selector string into an object used for element filtering
    /// </summary>
    /// <param name="selector">Selector string</param>
    /// <returns>Element selection object, or null if selector is invalid</returns>
    private static ElementSelector? parseSelector(string selector)
    {
        MethodGuard.NoNull(new { selector });
        selector = selector.Trim();

        //extract individual element selector strings
        var eSelectors = new List<string>();
        while (!string.IsNullOrWhiteSpace(selector))
        {
            var mth = Regex.Match(selector,
                @"^\>?\s*((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?\s*", RegexOptions.IgnoreCase);
            if (!mth.Success) { return null; }

            eSelectors.Add(mth.Value.Trim());
            //remove individual element selector
            if (mth.Length < selector.Length)
            {
                selector = selector.Substring(mth.Length).Trim();
            }
            else //selector string completely consumed
            {
                selector = string.Empty;
            }
        }

        //get element selectors
        var filters = new List<SelectionFilter>();
        foreach (var es in eSelectors)
        {
            string strFilter = es;
            SelectionScope pScope = SelectionScope.AllChildren;
            //handle direct child indicator
            if (es[0] == '>')
            {
                pScope = SelectionScope.ImmediateChildren;
                strFilter = es.Substring(1).Trim();
            }

            var sf = SelectionFilter.FromString(strFilter);
            if (sf == null) { return null; }//invalid selector

            sf.PrimaryScope = pScope;
            filters.Add(sf);
        }

        return new ElementSelector(filters);
    }

    /// <summary>
    /// Parses a string of possibly multiple selectors into a list of element selection 
    /// objects
    /// </summary>
    /// <param name="selectors">Selector string</param>
    /// <returns>A list of element selection objects, or null if input was invalid</returns>
    private static List<ElementSelector>? parseSelectors(string selectors)
    {
        MethodGuard.NoNull(new {selectors});
        selectors = selectors.Trim();

        //extract individual element selector strings
        var eSelectors = new List<string>();
        while (!string.IsNullOrWhiteSpace(selectors))
        {
            var mth = Regex.Match(selectors,
                @"^\,?\s*((((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?)(\s*\>?\s*(((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?))*)\s*", RegexOptions.IgnoreCase);
            if (!mth.Success) { return null; }

            eSelectors.Add(mth.Value.Trim());
            //remove individual element selector
            if (mth.Length < selectors.Length)
            {
                selectors = selectors.Substring(mth.Length).Trim();
            }
            else //selector string completely consumed
            {
                break;
            }
        }

        //get element selectors
        var result = new List<ElementSelector>();
        foreach (var es in eSelectors)
        {
            string slctor = es;
            //remove leading comma
            if (es[0] == ',')
            {
                slctor = es.Substring(1).Trim();
            }

            var elemSelector = parseSelector(slctor);
            if (elemSelector == null) { return null; }//invalid selector

            result.Add(elemSelector);
        }

        return result;
    }

    /// <summary>
    /// Parses a CSS-style rule set string into one or more ruleset objects
    /// </summary>
    /// <param name="ruleset">Rule set string</param>
    /// <returns>A list of rule set objects</returns>
    private static List<RuleSet> parseRuleSet(string ruleset)
    {
        if (string.IsNullOrWhiteSpace(ruleset))
        {
            throw new ArgumentNullException("ruleset string null or empty");
        }

        ruleset = ruleset.Trim();

        //separate selector(s) from style declaration
        var mSelectors = Regex.Match(ruleset,
            @"^((((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?)(\s*\>?\s*(((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?))*)(\s*\,\s*((((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?)(\s*\>?\s*(((((\*?[\.#])?[a-z0-9_\-]+)+)|\*)(:(first|last)-child)?))*))*", RegexOptions.IgnoreCase);
        if (!mSelectors.Success || mSelectors.Length == ruleset.Length)//invalid input
        {
            return new List<RuleSet>();
        }
        string selectors = mSelectors.Value;
        string styleDeclaration = ruleset.Substring(selectors.Length).Trim();

        //build rule sets
        var slctors = parseSelectors(selectors);
        if (slctors == null) { return new List<RuleSet>(); }//invalid input
        var rsets = slctors.Select(s => new RuleSet { Selector = s }).ToList();
        //add styles to rule sets
        foreach (var rs in rsets)
        {
            rs.Styles = ParseStyleDeclaration(styleDeclaration, rs.Selector.Specificity);
            if (rs.Styles == null || !rs.Styles.Any()) { return new List<RuleSet>(); }
        }

        return rsets;
    }
}
