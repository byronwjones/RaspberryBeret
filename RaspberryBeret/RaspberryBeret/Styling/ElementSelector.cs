using RaspberryBeret.Elements;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaspberryBeret.Styling;
internal class ElementSelector
{
    // There is one SelectionFilter per descendant level:
    // SelectionFilter > SelectionFilter (direct children), or SelectionFilter SelectionFilter (all descendants)
    public ElementSelector(IEnumerable<SelectionFilter> selectionFilters)
    {
        if (selectionFilters == null || !selectionFilters.Any())
        {
            throw new ArgumentNullException("selection filters null or empty");
        }

        filters = selectionFilters;
    }

    public SelectionFilter[] Filters
    {
        get { return filters.ToArray(); }
    }

    public int Specificity
    {
        get
        {
            return filters.Sum(f => f.Specificity);
        }
    }

    /// <summary>
    /// Uses this selector to filter elements in the given DOM
    /// </summary>
    /// <param name="dom">The DOM</param>
    /// <returns>Elements that meet this selection criteria</returns>
    public IEnumerable<Element> ApplySelector(Element dom)
    {
        if (dom == null)
        {
            throw new ArgumentNullException("DOM is null");
        }

        IEnumerable<Element> kids = [dom];
        //apply filters on DOM
        foreach (var f in filters)
        {
            kids = f.ApplyFilter(kids);
            if (!kids.Any()) { break; }
        }

        return kids;
    }

    private IEnumerable<SelectionFilter> filters = [];
}
