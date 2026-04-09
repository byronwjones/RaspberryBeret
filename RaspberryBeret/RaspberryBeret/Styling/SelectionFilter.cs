using RaspberryBeret.Elements;
using System.Text.RegularExpressions;

namespace RaspberryBeret.Styling;
internal class SelectionFilter
{
    public SelectionFilter()
    {
        ClassesRequired = new List<string>();
        PrimaryScope = SecondaryScope = SelectionScope.AllChildren;
    }

    public SelectionScope PrimaryScope { get; set; }
    public SelectionScope SecondaryScope { get; set; }
    /// <summary>
    /// CSS classes an element must have for the related style to apply
    /// </summary>
    public List<string> ClassesRequired { get; private set; }
    /// <summary>
    /// Element type the related style applies to
    /// </summary>
    public string? ElementRequired { get; set; }
    /// <summary>
    /// ID element must have for the related style to apply
    /// </summary>
    public string? IdRequired { get; set; }

    /// <summary>
    /// Whether any filtering needs to be done on the primary scope elements to determine
    /// which of them to apply the related style to.  If no filtering needed, the related style
    /// applies to all primary scope elements
    /// </summary>
    public bool FilterPrimaryScope
    {
        get
        {
            return ClassesRequired.Any() ||
                ElementRequired != null ||
                IdRequired != null;
        }
    }

    public int Specificity
    {
        get
        {
            int s = ClassesRequired.Count * 10;
            if (ElementRequired != null) { s += 1; }
            if (IdRequired != null) { s += 100; }
            if (SecondaryScope != SelectionScope.AllChildren) { s += 10; }

            return s;
        }
    }

    /// <summary>
    /// Applies this filter to the children of the given element
    /// </summary>
    /// <param name="e">Element whose kids get filtered</param>
    /// <returns>Child elements that met the filtering criteria</returns>
    public IEnumerable<Element> ApplyFilter(Element e)
    {
        if (e == null)
        {
            throw new ArgumentNullException("element");
        }

        //determine which children to filter: all including grandchildren, or just
        //immediate children
        IEnumerable<Element> kids = PrimaryScope == SelectionScope.AllChildren ?
            e.AllDescendants : e.Children;
        if (!kids.Any()) { return kids; } //no need to work

        //only elements of a certain type
        if (!string.IsNullOrWhiteSpace(ElementRequired))
        {
            kids = kids.Where(k => k.Tag.Name == ElementRequired);
        }
        //only elements with the given ID
        if (!string.IsNullOrWhiteSpace(IdRequired))
        {
            kids = kids.Where(k => k.GetAttributeValue("id") == IdRequired);
        }
        //only elements with the given classes
        if (ClassesRequired.Any())
        {
            kids = kids.Where(k => k.Classes.Intersect(ClassesRequired)
                .Count() == ClassesRequired.Count);
        }
        //first children only
        if (SecondaryScope == SelectionScope.FirstChild)
        {
            kids = kids.Where(k => k.Parent == null ||
                k.Parent.Children.First() == k);
        }
        //last children only
        if (SecondaryScope == SelectionScope.LastChild)
        {
            kids = kids.Where(k => k.Parent == null ||
                k.Parent.Children.Last() == k);
        }

        return kids;
    }
    public IEnumerable<Element> ApplyFilter(IEnumerable<Element> elements)
    {
        if (elements == null)
        {
            throw new ArgumentNullException("elements");
        }

        var kids = new List<Element>();
        foreach (var e in elements)
        {
            kids.AddRange(ApplyFilter(e));
        }

        return kids;
    }

    public static SelectionFilter? FromString(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter)) { return null; }
        filter = filter.ToLower().Trim();
        var result = new SelectionFilter();

        //get secondary scope (nth element attribute)
        var mth = Regex.Match(filter, @":(first|last)-child$", RegexOptions.IgnoreCase);
        if (mth.Success)
        {
            var secondaryScope = mth.Value;
            result.SecondaryScope = secondaryScope.Contains("first-child") ?
                SelectionScope.FirstChild : SelectionScope.LastChild;
            //remove secondary scope from filter
            filter = filter.Remove(mth.Index);
        }

        //split selector into filtering parameters
        filter = filter.Replace(".", "`.").Replace("#", "`#");
        var filterParams = filter.Split('`');
        //build filter with parameters
        foreach (var fp in filterParams)
        {
            //ignore *, empty parameters
            if (string.IsNullOrWhiteSpace(fp) || fp == "*") { continue; }
            //id
            if (fp[0] == '#')
            {
                //only one ID in a legal selector
                if (!string.IsNullOrWhiteSpace(result.IdRequired)) { return null; }
                result.IdRequired = fp.Substring(1);
            }
            //class
            else if (fp[0] == '.')
            {
                var cls = fp.Substring(1);
                if (!result.ClassesRequired.Contains(cls)) //only unique ones
                {
                    result.ClassesRequired.Add(cls);
                }
            }
            //element type
            else
            {
                result.ElementRequired = fp;
            }
        }

        return result;
    }
}
