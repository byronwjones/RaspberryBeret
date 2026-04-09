using BWJ.Core;
using RaspberryBeret.Parsing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RaspberryBeret.Elements;
internal class Tag
{
    public Tag(string pdfmlTag, int startIndex)
    {
        initTagFromSource(pdfmlTag, startIndex);
    }

    public Tag(Match m)
    {
        initTagFromSource(m.Value, m.Index);
    }

    public Tag(string name, TagType tagType)
    {
        MethodGuard.NoNull(new { name });

        Name = name.ToLower();
        TagType = tagType;
    }

    /// <summary>
    /// Gets the name of the tag represented by this instance
    /// </summary>
    public string? Name { get; private set; }

    /// <summary>
    /// Gets the type of the tag represented by this instance
    /// </summary>
    public TagType TagType { get; private set; }

    /// <summary>
    /// The snippet of PDFML source text from which this tag was derived
    /// </summary>
    public string SourceText { get; private set; } = string.Empty;

    /// <summary>
    /// The start index of the PDFML source data snippet, in the context of the
    /// original PDFML source text
    /// </summary>
    public int SourceTextStartIndex { get; private set; }

    /// <summary>
    /// Gets the attributes associated with this tag
    /// </summary>
    public Dictionary<string, string> Attributes { get; } = new Dictionary<string, string>();

    /// <summary>
    /// Gets or sets whether all bindings have been completed for this
    /// this tag's attributes
    /// </summary>
    public bool _Compiled { get; set; }

    /// <summary>
    /// Populates tag object properties using a PDFML source snippet
    /// </summary>
    /// <param name="pdfmlTag">PDFML snippet</param>
    /// <param name="startIndex">Starting index of PDFML snippet in the context
    /// of the PDFML source code</param>
    private void initTagFromSource(string pdfmlTag, int startIndex)
    {
        this.SourceText = pdfmlTag;
        this.SourceTextStartIndex = startIndex;

        //identify tag type
        if (Regex.IsMatch(pdfmlTag, "<\\s*[a-z0-9_\\-:]+((\\s+[^\\s<>=\\\"\\'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*[^\\s<>=\"'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*(\"[^\"]*\"|'[^']*')))*\\s*>", RegexOptions.IgnoreCase))
        {
            TagType = TagType.Opening;
        }
        else if (Regex.IsMatch(pdfmlTag, "<\\s*[a-z0-9_\\-:]+((\\s+[^\\s<>=\\\"\\'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*[^\\s<>=\"'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*(\"[^\"]*\"|'[^']*')))*\\s*\\/?\\s*>", RegexOptions.IgnoreCase))
        {
            TagType = TagType.SelfClosing;
        }
        else if (Regex.IsMatch(pdfmlTag, @"<\s*\/\s*[a-z0-9_\-:]+\s*>", RegexOptions.IgnoreCase))
        {
            TagType = TagType.Closing;
        }
        else //source input string doesn't represent a tag
        {
            Name = null;
            return;
        }

        //get tag name
        var tagOpening = Regex.Match(pdfmlTag, @"^<\s*\/?[a-z0-9_\-:]+");
        //the Remove(0, 1) is removing the '<' character; all tag names are lower case
        this.Name = tagOpening.Value.Remove(0, 1).Trim().ToLower();
        //also remove '/' from an end tag
        if (this.TagType == TagType.Closing)
        {
            this.Name = this.Name.Remove(0, 1).Trim();
        }
        //no tag configured this way can have the name 'textrun'
        if (this.Name == "textrun")
        {
            this.Name = "$textrun";
        }

        //nothing more to do if this is a closing tag
        if (this.TagType == Elements.TagType.Closing)
        {
            return;
        }

        //parse out attributes encountered
        string attrSegment = pdfmlTag.Remove(0, tagOpening.Length);
        var attrs = Regex.Matches(attrSegment, "(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*[^\\s<>=\"'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*(\"[^\"]*\"|'[^']*'))|(\\s+[^\\s<>=\\\"\\'\\/]+)")
            .Cast<Match>()
            //converts matches to list of key/value pair strings
            .Select(m => m.Value.Trim());
        foreach (var keyValue in attrs)
        {
            //handle key only attribute
            if (!keyValue.Contains('='))
            {
                var kv = ParseUtils.ResolveEncodedChars(keyValue)!;
                Attributes[kv.ToLower()] = kv;
                continue;
            }

            //normal attribute
            var splitCharIndex = keyValue.IndexOf('=');
            var key = keyValue.Substring(0, splitCharIndex).Trim().ToLower();
            var value = keyValue.Substring(splitCharIndex + 1).Trim();
            key = ParseUtils.ResolveEncodedChars(key)!;
            value = ParseUtils.ResolveEncodedChars(value)!;

            //if any attribute key has no alpha characters, the tag match encountered
            //is not really a tag
            if (!Regex.IsMatch(key, "[a-z]", RegexOptions.IgnoreCase))
            {
                Name = null;
                return;
            }

            //get quote character used to wrap value, if any
            var quoteChar = (value[0] == '\'') ? "'" :
                (value[0] == '"') ? "\"" : null;
            //unwrap value if necessary
            if (quoteChar != null)
            {
                value = value.Substring(1, (value.Length - 2)).Trim();
            }

            //add attribute if it has a value
            if (!string.IsNullOrWhiteSpace(value))
            {
                Attributes[key] = value;
            }
        }
    }
}
