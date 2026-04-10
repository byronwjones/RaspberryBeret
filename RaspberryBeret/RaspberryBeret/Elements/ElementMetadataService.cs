using RaspberryBeret.Parsing;
using RaspberryBeret.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RaspberryBeret.Elements;
internal static class ElementMetadataService
{
    static ElementMetadataService()
    {
        allInfo = [
            new Pdfml(),
            new Head(),
            new Contents(),
            new Header(),
            new Footer(),
            new Grid(),
            new Row(),
            new ColOne(),
            new ColTwo(),
            new ColThree(),
            new ColFour(),
            new ColFive(),
            new ColSix(),
            new ColSeven(),
            new ColEight(),
            new ColNine(),
            new ColTen(),
            new ColEleven(),
            new ColTwelve(),
            new Table(),
            new TableHeader(),
            new TableFooter(),
            new TableBody(),
            new TableRow(),
            new TableHeaderCell(),
            new TableCell(),
            new OrderedList(),
            new UnorderedList(),
            new ListItem(),
            new Img(),
            new Paragraph(),
            new HeaderOne(),
            new HeaderTwo(),
            new HeaderThree(),
            new HeaderFour(),
            new HeaderFive(),
            new HeaderSix(),
            new Bold(),
            new Anchor(),
            new Underline(),
            new Italic(),
            new EmphasizedText(),
            new Strong(),
            new Label(),
            new Span(),
            new TextRun(),
            new PageNumber(),
            new PageTotal(),
            new LineBreak(),
            new DocumentElement(),
            new PageBreak(),
            new StyleElement(),
            new Include(),
            new ContextElement(),
            new ForEach(),
            new ShowElement(),
            new HideElement()
        ];
    }

    #region ElementInfo Definitions
    public class Pdfml : ElementMetadata
    {
        public Pdfml()
        {
            TagName = Elements.TagName.pdfml;
            IsTagSelfClosing = false;
            GroupMembership = [ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //There must be one child 'contents' element
            if (e.Children.Count() == 0 || (e.Children.Count() == 1 &&
                e.Children[0].Tag.Name != Elements.TagName.contents))
            {
                ParseUtils.ThrowParsingException(e,
                    $"Element '{Elements.TagName.pdfml}' must have one child of type '{Elements.TagName.contents}'.");
            }
            //only children of types contents and head are allowed
            var badKid = e.Children.
                FirstOrDefault(c => (new string[] { Elements.TagName.contents, Elements.TagName.head }).Contains(c.Tag.Name) == false);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    $"Element '{Elements.TagName.pdfml}' may only have children of types '{Elements.TagName.head}' and '{Elements.TagName.contents}'.");
            }
            //there can only be one contents element
            var kids = e.Children.Where(c => c.Tag.Name == Elements.TagName.contents).ToList();
            if (kids.Count > 1)
            {
                ParseUtils.ThrowParsingException(kids[1],
                    $"Element '{Elements.TagName.pdfml}' may only have child element of type '{Elements.TagName.contents}'.");
            }
            //there can only be one head element
            kids = e.Children.Where(c => c.Tag.Name == Elements.TagName.head).ToList();
            if (kids.Count > 1)
            {
                ParseUtils.ThrowParsingException(kids[1],
                    $"Element '{Elements.TagName.pdfml}' may only have child element of type '{Elements.TagName.head}'.");
            }
        }
    }

    public class Head : ElementMetadata
    {
        public Head()
        {
            TagName = "head";
            IsTagSelfClosing = false;
            GroupMembership = [ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only style tags are allowed as children
            var badKid = e.Children.FirstOrDefault(c => c.Tag.Name != "style");
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element 'head' may only have children of type 'style'.");
            }
        }

        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class Contents : ElementMetadata
    {
        public Contents()
        {
            TagName = Elements.TagName.contents;
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.HasChildren,
                ElementGroup.Styleable];
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-size: 10pt; font-family: Arial;", 0)
                .ToArray();
        }

        protected override void Validator(Element e)
        {
            //at the very least, there must be one child 'document' element
            if (e.Children.Count() == 0 ||
                e.Children.Count(c => c.Tag.Name == Elements.TagName.document) == 0)
            {
                ParseUtils.ThrowParsingException(e,
                    $"Element '{Elements.TagName.contents}' must have at least one child of type '{Elements.TagName.document}'.");
            }
            //only 'document' tags are allowed as children
            var badKid = e.Children
                .FirstOrDefault(c => c.Tag.Name != Elements.TagName.document);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    $"Element '{Elements.TagName.contents}' may only have children of type '{Elements.TagName.document}'; type '" +
                    badKid.Tag.Name + "' is not allowed.");
            }
        }
    }

    public class Header : HeaderOrFooter
    {
        public Header()
        {
            TagName = "header";
        }
    }
    public class Footer : HeaderOrFooter
    {
        public Footer()
        {
            TagName = "footer";
        }
    }
    public abstract class HeaderOrFooter : ElementMetadata
    {
        public HeaderOrFooter()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only 'grid' tags are allowed as children
            var badKid = e.Children.FirstOrDefault(c => c.Tag.Name != "grid" &&
                c.Tag.Name != "table");
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + e.Tag.Name +
                    "' may only have children of type 'grid' or 'table'.");
            }
            //valid values for this attribute are
            //'all', 'first', 'odd', or 'even'
            var okTypeAttr = new string[] { "all", "first", "odd", "even" };
            string typeVal = e.GetAttributeValue("targetpages");
            if (!string.IsNullOrWhiteSpace(typeVal) &&
                okTypeAttr.Contains(typeVal.ToLower()) == false)
            {
                ParseUtils.ThrowParsingException(e,
                    "Valid values for attribute 'targetpages' are 'all', " +
                    "'first', or 'odd', or 'even'");
            }
        }

        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class Grid : ElementMetadata
    {
        public Grid()
        {
            TagName = "grid";
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.MarginableBlock,
                ElementGroup.Sizeable,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only 'row' tags are allowed as children
            var badKid = e.Children.FirstOrDefault(c => c.Tag.Name != "row");
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element 'grid' may only have children of type 'row'.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class Row : ElementMetadata
    {
        public Row()
        {
            TagName = "row";
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only 'col-[n]' tags are allowed as children
            var badKid = e.Children
                .FirstOrDefault(c => Regex.IsMatch(c.Tag.Name!, @"^col\-[1-9][0-2]?$", RegexOptions.IgnoreCase) == false);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element 'row' may only have children of types 'col-[1 - 12]' - '" +
                    badKid.Tag.Name + "' is not allowed.");
            }

            //no more than 12 columns allowed
            int colCount = e.Children.Select(c =>
            {
                return Convert.ToInt32(c.Tag.Name!.Split('-')[1]);
            }).Sum();
            if (colCount > 12)
            {
                ParseUtils.ThrowParsingException(e, "Total sum of col-n rows must not exceed 12.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class ColOne : Col
    {
        public ColOne()
        {
            TagName = "col-1";
        }
    }
    public class ColTwo : Col
    {
        public ColTwo()
        {
            TagName = "col-2";
        }
    }
    public class ColThree : Col
    {
        public ColThree()
        {
            TagName = "col-3";
        }
    }
    public class ColFour : Col
    {
        public ColFour()
        {
            TagName = "col-4";
        }
    }
    public class ColFive : Col
    {
        public ColFive()
        {
            TagName = "col-5";
        }
    }
    public class ColSix : Col
    {
        public ColSix()
        {
            TagName = "col-6";
        }
    }
    public class ColSeven : Col
    {
        public ColSeven()
        {
            TagName = "col-7";
        }
    }
    public class ColEight : Col
    {
        public ColEight()
        {
            TagName = "col-8";
        }
    }
    public class ColNine : Col
    {
        public ColNine()
        {
            TagName = "col-9";
        }
    }
    public class ColTen : Col
    {
        public ColTen()
        {
            TagName = "col-10";
        }
    }
    public class ColEleven : Col
    {
        public ColEleven()
        {
            TagName = "col-11";
        }
    }
    public class ColTwelve : Col
    {
        public ColTwelve()
        {
            TagName = "col-12";
        }
    }
    public abstract class Col : ElementMetadata
    {
        public Col()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.PaddableBlock,
                ElementGroup.TableOrGridCell,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only elements classified as cell content can be children
            var badKid = e.Children
                .FirstOrDefault(c => c.Metadata == null ||
                    !c.Metadata.GroupMembership.Contains(ElementGroup.CellContent));
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + badKid.Tag.Name +
                    "' may not be a direct child of a '" + e.Tag.Name +
                    "' element.");
            }
        }
    }

    public class Table : ElementMetadata
    {
        public Table()
        {
            TagName = "table";
            IsTagSelfClosing = false;
            GroupMembership = new ElementGroup[] {
                        ElementGroup.Styleable,
                        ElementGroup.Block,
                        ElementGroup.MarginableBlock,
                        ElementGroup.Sizeable,
                        ElementGroup.HasChildren
                    };
        }

        protected override void Validator(Element e)
        {
            //only 'tbody', 'thead', and 'tfoot' tags are allowed as children
            var okKids = new string[] { "tbody", "thead", "tfoot" };
            var badKid = e.Children
                .FirstOrDefault(c => okKids.Contains(c.Tag.Name) == false);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element 'table' may only have children of types " +
                    "'thead', 'tbody', or 'tfoot'.");
            }
            //there must be a tbody child
            if (e.Children.Count(c => c.Tag.Name == "tbody") == 0)
            {
                ParseUtils.ThrowParsingException(e,
                    "Element 'table' must have one child of type 'tbody'.");
            }
            //only one thead element allowed
            var kids = e.Children.Where(c => c.Tag.Name == "thead").ToList();
            if (kids.Count > 1)
            {
                ParseUtils.ThrowParsingException(kids[1],
                    "Element 'table' may only have child element of type 'thead'.");
            }
            //only one tbody element allowed
            kids = e.Children.Where(c => c.Tag.Name == "tbody").ToList();
            if (kids.Count > 1)
            {
                ParseUtils.ThrowParsingException(kids[1],
                    "Element 'table' may only have child element of type 'tbody'.");
            }
            //only one tfoot element allowed
            kids = e.Children.Where(c => c.Tag.Name == "tfoot").ToList();
            if (kids.Count > 1)
            {
                ParseUtils.ThrowParsingException(kids[1],
                    "Element 'table' may only have child element of type 'tfoot'.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if tbody element has no children
            var tbody = e.Children.FirstOrDefault(c => c.Tag.Name == "tbody");

            return tbody == null || tbody.Children.Count() == 0;
        }
    }

    public class TableFooter : TableHeaderOrFooter
    {
        public TableFooter()
        {
            TagName = "tfoot";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 8pt;", 0).ToArray();
        }
    }
    public class TableHeader : TableHeaderOrFooter
    {
        public TableHeader()
        {
            TagName = "thead";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 12pt;", 0).ToArray();
        }
    }
    public class TableBody : TableSection
    {
        public TableBody()
        {
            TagName = "tbody";
        }
    }
    public abstract class TableHeaderOrFooter : TableSection
    {
        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }
    public abstract class TableSection : ElementMetadata
    {
        public TableSection()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only 'tr' tags are allowed as children
            var badKid = e.Children.FirstOrDefault(c => c.Tag.Name != "tr");
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + e.Tag.Name +
                    "' may only have children of type 'tr'.");
            }
        }
    }

    public class TableRow : ElementMetadata
    {
        public TableRow()
        {
            TagName = "tr";
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only 'td' or 'th' tags are allowed as children
            var okKids = new string[] { "th", "td" };
            var badKid = e.Children
                .FirstOrDefault(c => okKids.Contains(c.Tag.Name) == false);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element 'tr' may only have children of types 'th' or 'td' - '" +
                    badKid.Tag.Name + "' is not allowed.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class TableHeaderCell : TableRowContent
    {
        public TableHeaderCell()
        {
            TagName = "th";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold;", 0).ToArray();
        }
    }
    public class TableCell : TableRowContent
    {
        public TableCell()
        {
            TagName = "td";
        }
    }
    public abstract class TableRowContent : ElementMetadata
    {
        public TableRowContent()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.Sizeable,
                ElementGroup.PaddableBlock,
                ElementGroup.TableOrGridCell,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only elements classified as cell content can be children
            var badKid = e.Children
                .FirstOrDefault(c => c.Metadata == null ||
                    !c.Metadata.GroupMembership.Contains(ElementGroup.CellContent));
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + badKid.Tag.Name +
                    "' may not be a direct child of a '" + e.Tag.Name +
                    "' element.");
            }
        }
    }

    public class OrderedList : List
    {
        public OrderedList()
        {
            TagName = "ol";
        }
    }
    public class UnorderedList : List
    {
        public UnorderedList()
        {
            TagName = "ul";
        }
    }
    public abstract class List : ElementMetadata
    {
        public List()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.ListComponent,
                ElementGroup.MarginableBlock,
                ElementGroup.Sizeable,
                ElementGroup.HasChildren,
                ElementGroup.CellContent];
        }

        protected override void Validator(Element e)
        {
            //only 'li' elements are allowed as children
            var badKid = e.Children.FirstOrDefault(c => c.Tag.Name != "li");
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + e.Tag.Name +
                    "' may only have children of type 'li' - '" + badKid.Tag.Name +
                    "' elements are not allowed.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if element has no children
            return e.Children.Count() == 0;
        }
    }

    public class ListItem : ElementMetadata
    {
        public ListItem()
        {
            TagName = "li";
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.MarginableBlock,
                ElementGroup.PaddableBlock,
                ElementGroup.BlockText,
                ElementGroup.Text,
                ElementGroup.HasChildren];
        }

        protected override void Validator(Element e)
        {
            //only elements classified as cell content can be children
            var badKid = e.Children
                .FirstOrDefault(c => c.Metadata == null ||
                    !c.Metadata.GroupMembership.Contains(ElementGroup.CellContent));
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + badKid.Tag.Name +
                    "' may not be a direct child of an 'li' element.");
            }
        }
    }

    public class Img : ElementMetadata
    {
        public Img()
        {
            TagName = "img";
            IsTagSelfClosing = true;
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.MarginableBlock,
                ElementGroup.Sizeable,
                ElementGroup.CellContent];
        }

        protected override bool Discarder(Element e)
        {
            //remove if no src attribute
            return string.IsNullOrWhiteSpace(e.GetAttributeValue("src"));
        }
    }

    public class Paragraph : BlockText
    {
        public Paragraph()
        {
            TagName = "p";
        }
    }
    public class HeaderOne : BlockText
    {
        public HeaderOne()
        {
            TagName = "h1";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 20pt; margin-bottom: 6.7pt;", 0).ToArray();
        }
    }
    public class HeaderTwo : BlockText
    {
        public HeaderTwo()
        {
            TagName = "h2";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 15pt; margin-bottom: 8.3pt;", 0).ToArray();
        }
    }
    public class HeaderThree : BlockText
    {
        public HeaderThree()
        {
            TagName = "h3";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 12pt; margin-bottom: 10pt;", 0).ToArray();
        }
    }
    public class HeaderFour : BlockText
    {
        public HeaderFour()
        {
            TagName = "h4";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 10pt; margin-bottom: 13.3pt;", 0).ToArray();
        }
    }
    public class HeaderFive : BlockText
    {
        public HeaderFive()
        {
            TagName = "h5";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 8.3pt; margin-bottom: 16.7pt;", 0).ToArray();
        }
    }
    public class HeaderSix : BlockText
    {
        public HeaderSix()
        {
            TagName = "h6";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold; font-size: 7pt; margin-bottom: 23.3pt;", 0).ToArray();
        }
    }
    public abstract class BlockText : TextBase
    {
        public BlockText()
        {
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.Block,
                ElementGroup.MarginableBlock,
                ElementGroup.PaddableBlock,
                ElementGroup.BlockText,
                ElementGroup.Text,
                ElementGroup.Sizeable,
                ElementGroup.CellContent,
                ElementGroup.HasChildren];
        }
    }

    public class Bold : InlineText
    {
        public Bold()
        {
            TagName = "b";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold;", 0).ToArray();
        }
    }
    public class Anchor : InlineText
    {
        public Anchor()
        {
            TagName = "a";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("color: #0000EE; text-decoration: underline;", 0).ToArray();
        }
    }
    public class Underline : InlineText
    {
        public Underline()
        {
            TagName = "u";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("text-decoration: underline;", 0).ToArray();
        }
    }
    public class Italic : InlineText
    {
        public Italic()
        {
            TagName = "i";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-style: italic;", 0).ToArray();
        }
    }
    public class EmphasizedText : InlineText
    {
        public EmphasizedText()
        {
            TagName = "em";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-style: italic;", 0).ToArray();
        }
    }
    public class Strong : InlineText
    {
        public Strong()
        {
            TagName = "strong";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold;", 0).ToArray();
        }
    }
    public class Label : InlineText
    {
        public Label()
        {
            TagName = "label";
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("font-weight: bold;", 0).ToArray();
        }
    }
    public class Span : InlineText
    {
        public Span()
        {
            TagName = "span";
        }
    }
    public abstract class InlineText : TextBase
    {
        public InlineText()
        {
            GroupMembership = [
                ElementGroup.Styleable,
                ElementGroup.InlineText,
                ElementGroup.Text,
                ElementGroup.CellContent,
                ElementGroup.HasChildren];
        }
    }
    public abstract class TextBase : ElementMetadata
    {
        public TextBase()
        {
            IsTagSelfClosing = false;
        }

        protected override void Validator(Element e)
        {
            //only element classified as inline text are allowed as children
            var badKid = e.Children
                .FirstOrDefault(c => c.Metadata == null ||
                    !c.Metadata.GroupMembership.Contains(ElementGroup.InlineText));
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    "Element '" + badKid.Tag.Name +
                    "' is not allowed to be a child of '" + e.Tag.Name +
                    "', which may only contain inline text.");
            }
        }
        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }

    public class TextRun : UnstyleableInlineText
    {
        public TextRun()
        {
            TagName = "textrun";
            IsTagSelfClosing = false;
        }
    }
    public class PageNumber : UnstyleableInlineText
    {
        public PageNumber()
        {
            TagName = "pagenumber";
            IsTagSelfClosing = true;
        }
    }
    public class PageTotal : UnstyleableInlineText
    {
        public PageTotal()
        {
            TagName = "pagetotal";
            IsTagSelfClosing = true;
        }
    }
    public class LineBreak : UnstyleableInlineText
    {
        public LineBreak()
        {
            TagName = "br";
            IsTagSelfClosing = true;
        }
    }
    public abstract class UnstyleableInlineText : ElementMetadata
    {
        public UnstyleableInlineText()
        {
            GroupMembership = [
                ElementGroup.InlineText,
                ElementGroup.Text,
                ElementGroup.CellContent];
        }
    }

    public class PageBreak : ElementMetadata
    {
        public PageBreak()
        {
            TagName = "pagebreak";
            IsTagSelfClosing = true;
        }
    }

    public class DocumentElement : ElementMetadata
    {
        public DocumentElement()
        {
            TagName = Elements.TagName.document;
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.HasChildren,
                ElementGroup.MarginableBlock,
                ElementGroup.Styleable];
            DefaultStyles =
                StyleUtils.ParseStyleDeclaration("margin: 1in;", 0)
                .ToArray();
        }

        protected override void Validator(Element e)
        {
            //at the very least, there must be one child 'grid' or 'table' element
            if (e.Children.Count() == 0 ||
                e.Children.Count(c => c.Tag.Name == "grid" || c.Tag.Name == "table") == 0)
            {
                ParseUtils.ThrowParsingException(e,
                    $"Element '{Elements.TagName.document}' must have at least one child of type 'grid' or 'table'.");
            }
            //only 'grid', 'table', 'pagebreak', 'header', and 'footer' tags are allowed as children
            var badKid = e.Children
                .FirstOrDefault(c => (new string[] { "grid", "table", "pagebreak", "header", "footer" })
                .Contains(c.Tag.Name) == false);
            if (badKid != null)
            {
                ParseUtils.ThrowParsingException(badKid,
                    $"Element '{Elements.TagName.document}' may only have children of types " +
                    "'grid', 'table', 'pagebreak', 'header', or 'footer'; type '" +
                    badKid.Tag.Name + "' is not allowed.");
            }
        }
    }

    public class StyleElement : ElementMetadata
    {
        public StyleElement()
        {
            TagName = "style";
            IsTagSelfClosing = false;
            GroupMembership = [ElementGroup.Virtual];
        }
    }

    public class Include : ElementMetadata
    {
        public Include()
        {
            TagName = "include";
            IsTagSelfClosing = true;
            GroupMembership = [ElementGroup.Virtual];
        }

        protected override bool Discarder(Element e)
        {
            //remove if no src attribute
            return string.IsNullOrWhiteSpace(e.GetAttributeValue("src"));
        }
    }

    public class ContextElement : ChildContextHost
    {
        public ContextElement()
        {
            TagName = "context";
        }
    }
    public class ForEach : ChildContextHost
    {
        public ForEach()
        {
            TagName = "foreach";
        }
    }
    public abstract class ChildContextHost : BindingElementBase
    {
        protected override void Validator(Element e)
        {
            //context attribute must be present
            if (string.IsNullOrWhiteSpace(e.GetAttributeValue("context")))
            {
                ParseUtils.ThrowParsingException(e,
                    "Element '" + e.Tag.Name +
                    "' is missing attribute 'context', which is required.");
            }
        }
    }

    public class ShowElement : VisibilityElement
    {
        public ShowElement()
        {
            TagName = "show";
        }
    }
    public class HideElement : VisibilityElement
    {
        public HideElement()
        {
            TagName = "hide";
        }
    }
    public abstract class VisibilityElement : BindingElementBase
    {
        protected override void Validator(Element e)
        {
            //either 'if' or 'ifnot' attribute must be present
            if (!e.Tag.Attributes.Keys.Contains("if") &&
                !e.Tag.Attributes.Keys.Contains("ifnot"))
            {
                ParseUtils.ThrowParsingException(e,
                    "Element '" + e.Tag.Name +
                    "' must contain either an 'if' or an 'ifnot' attribute.");
            }
            //both 'if' and 'ifnot' is not allowed
            if (e.Tag.Attributes.Keys.Contains("if") &&
                e.Tag.Attributes.Keys.Contains("ifnot"))
            {
                ParseUtils.ThrowParsingException(e,
                    "Element '" + e.Tag.Name +
                    "' may contain either an 'if' or an 'ifnot' attribute - but not both.");
            }
        }
    }
    public abstract class BindingElementBase : ElementMetadata
    {
        public BindingElementBase()
        {
            IsTagSelfClosing = false;
            GroupMembership = [
                ElementGroup.Virtual,
                ElementGroup.HasChildren];
        }

        protected override bool Discarder(Element e)
        {
            //remove if no children
            return e.Children.Count() == 0;
        }
    }
    #endregion

    /// <summary>
    /// Gets element information for elements with the given tag name
    /// </summary>
    /// <param name="tagName">The element's tag name</param>
    /// <returns>ElementInfo for the given element, or null if no info exists for it</returns>
    public static ElementMetadata? GetInfoFor(string tagName)
    {
        return allInfo.FirstOrDefault(e => e.TagName == tagName.ToLower());
    }

    private static ElementMetadata[] allInfo;
}
