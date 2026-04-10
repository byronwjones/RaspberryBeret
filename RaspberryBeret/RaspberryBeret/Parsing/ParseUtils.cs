using BWJ.Core;
using RaspberryBeret.Elements;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Parsing;
internal static class ParseUtils
{

    /// <summary>
    /// Resolves encoded characters to their proper symbols
    /// </summary>
    /// <param name="text">Text with characters to resolved</param>
    /// <returns>Resolved text</returns>
    public static string? ResolveEncodedChars(string? text)
    {
        if (text == null) { return null; }

        return text.Replace("&lt;", "<")
            .Replace("&gt;", ">")
            .Replace("&quot;", "\"")
            .Replace("&apos;", "'")
            .Replace("&grave;", "`")
            .Replace("&nbsp;", " ");
    }

    /// <summary>
    /// Throws a PDFML parsing/binding exception with detailed error information
    /// </summary>
    /// <param name="e">Element that caused the exception</param>
    /// <param name="message">What is wrong with the given element</param>
    public static void ThrowParsingException(Element e, string message,
        Exception? innerException = null)
    {
        //get root element
        Element root = e.RootParent != null ? e.RootParent : e;

        //generate source from root element
        var source = root.GetPdfmlSourceLines();
        //get line number for this element
        int lineNumber = 0;
        string pdfml = PdfmlSourceFormatter.ToMultiLinePDFML(source);
        for (int i = 0; i < source.Count; i++)
        {
            if (source[i].Element == e)
            {
                lineNumber = i + 1;
                break;
            }
        }

        throw new ParsingException(message, pdfml, lineNumber, innerException);
    }

    /// <summary>
    /// Compresses all groups of consecutive whitespaces characters to a single blank
    /// character in a string of text
    /// </summary>
    /// <param name="text">Input string</param>
    /// <returns>Input string with compressed whitespaces</returns>
    public static string NormalizeWhitespace(string text)
    {
        MethodGuard.NoNull(new { text });
        return Regex.Replace(text, @"[\s]+", " ").Trim();
    }

    /// <summary>
    /// Converts PDFML template or snippet into a single line of text
    /// </summary>
    /// <param name="pdfml">Formatted PDFML text string</param>
    /// <returns>'Flattened' PDFML</returns>
    public static string FlattenTemplate(string? pdfml)
    {
        if (pdfml == null)
        {
            throw new ArgumentNullException("PDFML template value is null.");
        }

        return pdfml.Replace('\r', ' ').Replace('\n', ' ');
    }

    /// <summary>
    /// Removes comment tags from a PDFML document or snippet text string
    /// </summary>
    /// <param name="pdfml">PDFML text string</param>
    /// <returns>PDFML text string, without comments</returns>
    public static string RemoveComments(string pdfml)
    {
        var result = pdfml;
        var startTags = Regex.Matches(pdfml, @"<!\-\-").Cast<Match>().ToList();
        var endTags = Regex.Matches(pdfml, @"\-\->").Cast<Match>().ToList();

        int nextValidStartTagIsAfter = -1;
        int startDelta = 0;
        foreach (var startTag in startTags)
        {
            //ignore any comment start indicators found within a comment
            if (startTag.Index < nextValidStartTagIsAfter)
            {
                continue;
            }

            //find the matching end of comment indicator
            var endTag = endTags.FirstOrDefault(e => e.Index > startTag.Index);
            //no end of comment indicator is improperly formatted PDFML
            if (endTag == null)
            {
                throw new ParsingException("Improperly formatted comment incurred.",
                    pdfml,
                    sourceErrorIndex: startTag.Index);
            }

            //get the amount of text to remove
            int removeLength = (endTag.Index + endTag.Length) - startTag.Index;
            //remove the comment, taking any previously removed comments into account
            result = result.Remove((startTag.Index - startDelta), removeLength);

            startDelta += removeLength;
            nextValidStartTagIsAfter = endTag.Index;
        }

        return result;
    }

    /// <summary>
    /// Gets a fully-qualified file path for an existing file, relative to a
    /// folder containing a PDFML file
    /// </summary>
    /// <param name="origin">Fully-qualified file path of a parent PDFML file</param>
    /// <param name="sourcePath">Fully-qualified file path, or file path
    /// relative location of the PDFML file</param>
    /// <returns>Fully-qualified file path, or null or path can not be resolved,
    /// or file does not exist</returns>
    public static string? ResolveSourcePath(string? origin, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath)) { return null; }

        //normalize the source path to only use backslash chars
        sourcePath = sourcePath.Replace("/", "\\");
        //if the source path resolves on its own, it is assumed to be a fully qualified
        //path, independent of the location of the template
        try
        {
            if (File.Exists(sourcePath))
            {
                return sourcePath;
            }
        }
        catch { }//not a problem

        //now there really must be a PDFML template path
        if (string.IsNullOrWhiteSpace(origin)) { return null; }

        int lastSlash = origin.LastIndexOf('\\');
        string root = origin.Remove(lastSlash);
        DirectoryInfo rootDir = new DirectoryInfo(root);
        try
        {
            //get any 'go up a level' indicators
            sourcePath = sourcePath.Trim();
            var match = Regex.Match(sourcePath, @"^(\\?\.\.\\)+");
            if (match.Success)
            {
                //remove up level indicators from source path
                sourcePath = sourcePath.Remove(0, match.Length);
                //remove first and last slashes, if present
                var mtext = match.Value;
                if (mtext.IndexOf('\\') == 0)
                {
                    mtext = mtext.Remove(0, 1);
                }
                var lastCharIndex = mtext.Length - 1;
                if (mtext.LastIndexOf('\\') == lastCharIndex)
                {
                    mtext = mtext.Remove(lastCharIndex);
                }

                //walk root directory back up the correct amount of levels
                string[] upLevels = mtext.Split('\\');
                for (int i = 0; i < upLevels.Length; i++)
                {
                    if (rootDir.Parent == null)
                    {
                        return null;
                    }
                    else
                    {
                        rootDir = rootDir.Parent;
                    }
                }

                root = rootDir.FullName;
            }

            //combine root and source paths
            string result = Path.Combine(root, sourcePath);
            if (File.Exists(result))
            {
                return result;
            }
            else
            {
                return null;
            }
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Gets a list of element tag objects extracted from a PDFML document
    /// </summary>
    /// <param name="pdfml">PDFML source code</param>
    /// <returns>A list of all tags encountered in the PDFML source</returns>
    public static List<Tag> ExtractTagsFromPDFML(string pdfml)
    {
        //get all tags in the source code
        var matches =
            Regex.Matches(pdfml, "(<\\s*[a-z0-9_\\-:]+((\\s+[^\\s<>=\\\"\\'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*[^\\s<>=\"'\\/]+)|(\\s+[^\\s<>=\"'\\/]+\\s*=\\s*(\"[^\"]*\"|'[^']*')))*\\s*\\/?\\s*>|<\\s*\\/\\s*[a-z0-9_\\-:]+\\s*>)", RegexOptions.IgnoreCase);

        //convert the tag matches into tag objects and return them
        return matches.Cast<Match>()
            .Select(m => new Tag(m)) //convert matches to tag objects
            .Where(t => t.Name != null).ToList(); //filter out invalid tags
    }

    /// <summary>
    /// Constructs a Document Object Model from a PDFML document or code snippet
    /// </summary>
    /// <param name="pdfml">PDFML source code</param>
    /// <returns>A list of all element structures composed from the given PDFML</returns>
    public static List<Element> CreateDOM(string pdfml)
    {
        List<Element> DOM = new List<Element>();
        //parse the tags extracted from the source to build element structures
        var tags = ExtractTagsFromPDFML(pdfml);

        var openElements = new Stack<Element?>();
        //this makes null indicate nothing on the stack when performing a Peek(),
        //otherwise Peek would throw an exception
        openElements.Push(null);
        string? seekingTag = null;
        int seekingDepth = 0;
        int textrunStartIndex = 0;
        Element? topStack = null;
        //stack pop subroutine
        Action stackPop = () =>
        {
            //do nothing if nothing to pop
            if (topStack == null) { return; }

            openElements.Pop();
            topStack = openElements.Peek();
        };
        //stack push subroutine
        Action<Element> stackPush = (e) =>
        {
            //this element is the child of whatever's on top of the stack
            if (topStack != null)
            {
                topStack.AddChild(e);
            }
            //if there is nothing on the stack, this element is at the root of the
            //DOM
            else
            {
                DOM.Add(e);
            }

            //if this element's tag is self closing, or if its the type
            //of element that never has children, it doesn't get added to the stack
            if (e.Tag.TagType == TagType.SelfClosing ||
                (e.Metadata != null && e.Metadata.IsTagSelfClosing))
            {
                return;
            }

            openElements.Push(e);
            topStack = e;
        };
        //this subroutine appends to an existing textrun if there's one on the stack,
        //otherwise creates a new one
        Action<string> addTextrun = (txt) =>
        {
            if (txt == null) { return; }

            //resolve supported character encodings
            txt = ResolveEncodedChars(txt)!;

            //append to an existing textrun if there's one on the stack,
            //otherwise create a new one
            if (topStack != null && topStack.Tag.Name == "textrun")
            {
                topStack.InnerText += txt;
            }
            else
            {
                Element trun =
                    new Element(new Tag("textrun", TagType.Opening));
                trun.InnerText = txt;

                stackPush(trun);
            }
        };

        //construct the DOM...
        for (int inc = 0; inc < tags.Count; inc++)
        {
            var tag = tags[inc];

            //normal processing (not looking for a specific end tag)
            if (seekingTag == null)
            {
                //handle any run of text preceding this tag
                int runLength = tag.SourceTextStartIndex - textrunStartIndex;
                if (runLength > 0)
                {
                    var txt = pdfml.Substring(textrunStartIndex, runLength);
                    if (txt.Length > 1 ||
                        !string.IsNullOrWhiteSpace(txt))
                    {
                        addTextrun(txt);
                    }
                }

                //pop topmost stack element if its a text run
                if (topStack != null && topStack.Tag.Name == "textrun")
                {
                    stackPop();
                }

                //process tag based on type
                switch (tag.TagType)
                {
                    case TagType.Opening:
                        Element elem = new Element(tag);
                        stackPush(elem);
                        //if the element belongs to the 'virtual' classification,
                        //we need to treat all its contents as a textrun that we'll parse
                        //later.  So we seek this tag's end tag starting index position,
                        //and everything between the two is text...
                        if (elem.Metadata != null &&
                            elem.Metadata.GroupMembership.Contains(ElementGroup.Virtual))
                        {
                            seekingTag = elem.Tag.Name;
                            seekingDepth = 1;
                        }
                        break;
                    case TagType.SelfClosing:
                        Element scElem = new Element(tag);
                        stackPush(scElem);
                        break;
                    case TagType.Closing:
                        //closing tag only relevant if it matches some element
                        //on the stack
                        if (openElements.Count(e => e != null && e.Tag.Name == tag.Name) > 0)
                        {
                            //pop elements from stack until target element is reached
                            while (true)
                            {
                                bool stop = topStack is null || tag.Name == topStack.Tag.Name;
                                stackPop();
                                if (stop) { break; }
                            }
                        }
                        break;
                }

                //update the textrun start index
                textrunStartIndex = tag.SourceTextStartIndex + tag.SourceText.Length;
            }
            //looking for a certain end tag
            else
            {
                switch (tag.TagType)
                {
                    case TagType.Opening:
                        //if the tag is the same type as the one we're looking to
                        //find the closer for, increase number of closer tags we need
                        //to encounter
                        if (tag.Name == seekingTag)
                        {
                            seekingDepth++;
                        }
                        break;
                    case TagType.Closing:
                        //closing tag only relevant if it is the one we're looking for
                        if (tag.Name == seekingTag)
                        {
                            seekingDepth--;
                            //if this tag is the closer we want, process this tag again
                            //normally
                            if (seekingDepth == 0)
                            {
                                seekingTag = null;
                                inc--;
                            }
                        }
                        break;
                }
            }
        }

        //complete the DOM by handling any remaining textrun
        int rl = pdfml.Length - textrunStartIndex;
        if (rl > 0)
        {
            var txt = pdfml.Substring(textrunStartIndex, rl);
            if (txt != " ")
            {
                addTextrun(txt);
            }
        }

        return DOM;
    }
}
