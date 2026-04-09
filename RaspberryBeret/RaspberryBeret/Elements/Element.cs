using BWJ.Core;
using RaspberryBeret.DataBinding;
using RaspberryBeret.Parsing;
using RaspberryBeret.Styling;

namespace RaspberryBeret.Elements;
internal class Element
{
    public Element(Tag tag)
    {
        Classes = new HashSet<string>();

        //tag cannot be null
        if (tag == null)
        {
            throw new ArgumentNullException("Tag cannot be null.");
        }

        Tag = tag;
        Metadata = ElementInfoCenter.GetInfoFor(tag.Name!) ?? throw new Exception($"Encountered unsupported element <{tag.Name}>");

        if (tag.Name != "textrun")
        {
            //all elements besides textruns get a unique Id...
            Id = ElementIdGenerator.CreateId();
        }
    }

    /// <summary>
    /// Gets the unique identifier for this element
    /// </summary>
    public string Id { get; private set; } = string.Empty;

    /// <summary>
    /// Gets the information in the opening tag of this element
    /// </summary>
    public Tag Tag { get; private set; }

    /// <summary>
    /// Validation information about the element represented by this instance
    /// </summary>
    public ElementInfo Metadata { get; private set; }

    public Element[] Children
    {
        get
        {
            return children.ToArray();
        }
    }

    public Element[] AllDescendants
    {
        get
        {
            return allDescendants.ToArray();
        }
    }

    public Element? Parent { get; private set; }

    public Element? RootParent { get; private set; }

    public string InnerText
    {
        get
        {
            return generateInnerPDFML();
        }
        set
        {
            //only elements of type textrun can have inner text
            if (Tag.Name != "textrun")
            {
                throw new Exception("InnerText property is read only for elements of type '" +
                    Tag.Name + "'.");
            }

            innerText = value;
        }
    }

    public BindingModel? DataModel { get; set; }

    public HashSet<string> Classes { get; private set; } = new HashSet<string>();

    public Style[] Styles
    {
        get
        {
            var styles = explicitStyles.Select(kvp => kvp.Value).ToList();
            styles.AddRange(inheritedStyles.Select(kvp => kvp.Value));

            return styles.ToArray();
        }
    }

    public string? RootPath { get; set; }

    /// <summary>
    /// Gets or sets whether all bindings and transformations have been completed for this
    /// object
    /// </summary>
    public bool _Compiled { get; set; }

    /// <summary>
    /// Gets or sets the width of this element
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// Gets or sets any supplemental information associated with this element
    /// </summary>
    public object? AdditionalData { get; set; }

    /// <summary>
    /// Gets or sets whether or not the width value was explicitly set for this element
    /// by a style definition
    /// </summary>
    public bool IsWidthExplicit { get; set; }

    public string? GetAttributeValue(string key)
    {
        if (Tag == null || Tag.Attributes == null) { return null; }
        if (!Tag.Attributes.ContainsKey(key)) { return null; }

        return Tag.Attributes[key] ?? string.Empty;
    }

    public void AddChild(Element child)
    {
        validateManipulateChildren();
        if (child == null || children.Contains(child)) { return; }

        children.AddLast(child);
        updateAddAncestry(child);
    }

    public void AddChildToTop(Element child)
    {
        validateManipulateChildren();
        if (child == null || children.Contains(child)) { return; }

        children.AddFirst(child);
        updateAddAncestry(child);
    }

    public void AddChildBefore(Element child, Element target)
    {
        validateManipulateChildren();
        if (child == null || target == null || children.Contains(child)) { return; }

        var node = children.Find(target);
        if (node != null)
        {
            children.AddBefore(node, child);
            updateAddAncestry(child);
        }
        else
        {
            throw new Exception("Add before element target is not a child of '" +
                Tag.Name + "'.");
        }
    }

    public void AddChildAfter(Element child, Element target)
    {
        validateManipulateChildren();
        if (child == null || target == null || children.Contains(child)) { return; }

        var node = children.Find(target);
        if (node != null)
        {
            children.AddAfter(node, child);
            updateAddAncestry(child);
        }
        else
        {
            throw new Exception("Add after element target is not a child of '" +
                Tag.Name + "'.");
        }
    }

    public void RemoveChild(Element child)
    {
        validateManipulateChildren();
        if (child == null) { return; }

        var node = children.Find(child);
        if (node == null) { return; }

        children.Remove(node);
        updateRemoveAncestry(child);
    }

    public IEnumerable<Element> GetAncestors()
    {
        var p = this.Parent;
        while (p != null)
        {
            var currentParent = p;
            p = currentParent.Parent;
            yield return currentParent;
        }
    }

    /// <summary>
    /// Gets the most relevant root path value in this element's ancestry,
    /// which is the nearest non-null value on itself or its closest ancestor
    /// </summary>
    /// <returns></returns>
    public string? GetRootPath()
    {
        var rootPath = RootPath;
        if (string.IsNullOrWhiteSpace(rootPath))
        {
            foreach (var p in GetAncestors())
            {
                if (!string.IsNullOrWhiteSpace(p.RootPath))
                {
                    rootPath = p.RootPath;
                    break;
                }
            }
        }

        return rootPath;
    }

    /// <summary>
    /// Adds a style that was explicitly meant for this element, i.e. not inherited
    /// from a parent
    /// </summary>
    /// <param name="s">Style to add</param>
    public void AddExplicitStyle(Style s)
    {
        if (s == null)
        {
            throw new ArgumentNullException("Style is null.");
        }

        //remove any inherited style of the given type, because explicit styles always
        //have greater priority than inherited ones
        inheritedStyles.Remove(s.Name);

        if (shouldAddStyle(s, explicitStyles))
        {
            explicitStyles[s.Name] = s;

            //propagate this style to all this element's children/grandchildren if
            //its inheritable
            if (s.Metadata.Inheritable)
            {
                foreach (var child in AllDescendants)
                {
                    child.AddInheritedStyle(s);
                }
            }
        }
    }

    /// <summary>
    /// Adds a style inherited from a parent to this element
    /// </summary>
    /// <param name="s">Style to add</param>
    public void AddInheritedStyle(Style s)
    {
        MethodGuard.NoNull(new { s });

        //never add an inherited style if an explicit one of the same type is already
        //present
        if (explicitStyles.ContainsKey(s.Name) &&
            explicitStyles[s.Name] != null) { return; }

        if (shouldAddStyle(s, inheritedStyles))
        {
            inheritedStyles[s.Name] = s;
        }
    }

    /// <summary>
    /// Composes the list of classes that apply to this element from
    /// its class attribute, if present
    /// </summary>
    public void ExtractClassesFromAttributes()
    {
        //nothing to do if there are no classes
        var clss = GetAttributeValue("class");
        if (string.IsNullOrWhiteSpace(clss)) { return; }

        //remove excess whitespaces
        clss = ParseUtils.NormalizeWhitespace(clss);

        string[] arrClasses = clss!.Split(' ');
        foreach (var c in arrClasses)
        {
            Classes.Add(c);
        }
    }

    //TEST
    /// <summary>
    /// Generates objects representing lines of code for rendering this element
    /// back to PDFML
    /// </summary>
    /// <param name="indentAmount"></param>
    /// <returns></returns>
    public List<PdfmlSourceLine> GetPdfmlSourceLines(int indentAmount = 0)
    {
        List<PdfmlSourceLine> lines = new List<PdfmlSourceLine>();

        //handle textruns
        if (Tag.Name == "textrun")
        {
            lines.Add(new PdfmlSourceLine
            {
                Text = generateInnerPDFML(),
                Indentation = indentAmount
            });
        }
        //handle elements that never have children
        else if (Metadata != null && Metadata.IsTagSelfClosing)
        {
            string template = "<{0}{1} />";
            lines.Add(new PdfmlSourceLine
            {
                Element = this,
                Text = string.Format(template, Tag.Name, generateAttributeString()),
                Indentation = indentAmount
            });
        }
        //all other elements
        else
        {
            //opening tag
            string template = "<{0}{1}>";
            lines.Add(new PdfmlSourceLine
            {
                Element = this,
                Text = string.Format(template, Tag.Name, generateAttributeString()),
                Indentation = indentAmount
            });

            //inner PDFML
            foreach (var child in Children)
            {
                lines.AddRange(child.GetPdfmlSourceLines(indentAmount + 1));
            }

            //closing tag
            template = "</{0}>";
            lines.Add(new PdfmlSourceLine
            {
                Text = string.Format(template, Tag.Name, generateAttributeString()),
                Indentation = indentAmount
            });
        }

        return lines;
    }

    public override string ToString()
    {
        if (Tag == null || string.IsNullOrWhiteSpace(Tag.Name))
        {
            return base.ToString() ?? string.Empty;
        }

        return Tag.Name;
    }

    private Dictionary<string, Style> inheritedStyles = new Dictionary<string, Style>();
    private Dictionary<string, Style> explicitStyles = new Dictionary<string, Style>();

    private string? innerText = null;

    private LinkedList<Element> children = new LinkedList<Element>();
    private List<Element> allDescendants = new List<Element>();

    private bool shouldAddStyle(Style s, Dictionary<string, Style> d)
    {
        //add style if not already on the target list
        Style? existingStyle = null;
        if (d.ContainsKey(s.Name)) { existingStyle = d[s.Name]; }
        if (existingStyle == null) { return true; }

        //determine based on !important flag
        if (existingStyle.Important && !s.Important) { return false; }
        if (s.Important && !existingStyle.Important) { return true; }

        //determine based on specificity:
        //if new style greater or equal to existing, it wins
        if (s.Specificity >= existingStyle.Specificity) { return true; }

        return false;
    }

    private string generateInnerPDFML()
    {
        //if this element is a textrun, its InnerPDFML is its inner text
        if (Tag.Name == "textrun")
        {
            return innerText == null ? string.Empty : innerText;
        }

        //for all others, its the OuterPDFML of all its children
        List<PdfmlSourceLine> childLines = new List<PdfmlSourceLine>();
        foreach (var kid in Children)
        {
            childLines.AddRange(kid.GetPdfmlSourceLines());
        }

        //return as single line of text
        return PdfmlSourceFormatter.ToSingleLinePDFML(childLines);
    }

    private string generateAttributeString()
    {
        string attrs = string.Empty;
        foreach (var kvp in Tag.Attributes)
        {
            string template = " {0}=\"{1}\"";
            string value = kvp.Value;
            //handle (impossible?) case where value contains " and '
            if (value.Contains('\'') && value.Contains('"'))
            {
                value = value.Replace("\"", string.Empty);
            }
            //value contains double quote (")
            else if (value.Contains('"'))
            {
                template = "{0}='{1}'";
            }

            attrs += string.Format(template, kvp.Key, kvp.Value);
        }

        return attrs;
    }

    private void validateManipulateChildren()
    {
        if (Tag.Name == "textrun" || (Metadata != null && Metadata.IsTagSelfClosing))
        {
            throw new Exception("Element '" + Tag.Name + "' may not have children.");
        }
    }

    private void updateAddAncestry(Element child)
    {
        child.Parent = this;
        child.RootParent = RootParent == null ? this : RootParent;
        allDescendants.Add(child);
        //add descendants to the list, and update root parent of all descendants
        foreach (var g in child.AllDescendants)
        {
            allDescendants.Add(g);
            g.RootParent = child.RootParent;
        }

        //add new child/descendants to ancestors
        foreach (var p in GetAncestors())
        {
            p.allDescendants.Add(child);
            foreach (var g in child.AllDescendants)
            {
                p.allDescendants.Add(g);
            }
        }
    }

    private void updateRemoveAncestry(Element child)
    {
        child.Parent = null;
        child.RootParent = null;

        //remove all grandchildren, then remove child
        foreach (var g in child.AllDescendants)
        {
            allDescendants.Remove(g);
        }
        allDescendants.Remove(child);

        //do the same for all grandparents
        foreach (var p in GetAncestors())
        {
            foreach (var g in child.AllDescendants)
            {
                p.allDescendants.Remove(g);
            }
            p.allDescendants.Remove(child);
        }
    }
}
