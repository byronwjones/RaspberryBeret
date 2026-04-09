using RaspberryBeret.Styling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryBeret.Elements;
internal abstract class ElementInfo
{
    public ElementInfo()
    {
        GroupMembership = Array.Empty<ElementGroup>();
        DefaultStyles = Array.Empty<Style>();
    }

    /// <summary>
    /// Gets if it is the case that this tag does not need a closing tag, e.g. [br /]
    /// </summary>
    public bool IsTagSelfClosing { get; protected set; }

    /// <summary>
    /// Gets the names of the tags this metadata applies to
    /// </summary>
    public string TagName { get; protected set; } = string.Empty;

    /// <summary>
    /// Gets a list of the logical groups that this element belongs to
    /// </summary>
    public ElementGroup[] GroupMembership { get; protected set; }

    /// <summary>
    /// Gets a list of styles applied to elements of this type by default
    /// </summary>
    public Style[] DefaultStyles { get; protected set; }

    /// <summary>
    /// A function that verifies that a given element
    /// is formatted and used in the correct manner
    /// </summary>
    protected virtual void Validator(Element e) { }

    /// <summary>
    /// A function that determines whether or not a given element should be removed
    /// from the DOM
    /// </summary>
    protected virtual bool Discarder(Element e) { return false; }

    /// <summary>
    /// Verifies that the given element is formatted and used in the correct manner
    /// </summary>
    /// <param name="e">Element to validate</param>
    public void ValidateElement(Element e)
    {
        if (TagName != e.Tag.Name)
        {
            throw new Exception("Metadata for '" + TagName +
                "' can not be used to validate elements of type '" +
                e.Tag.Name + "'.");
        }

        //perform validation
        Validator(e);
    }

    /// <summary>
    /// Determines whether or not the given element should be removed from the DOM
    /// </summary>
    /// <param name="e">Element to evaluate</param>
    /// <returns>True if the element should be discarded</returns>
    public bool ShouldDiscardElement(Element e)
    {
        if(e.Tag.Name is null) { return true; } // nameless tags never belong
        if (TagName.Contains(e.Tag.Name) == false)
        {
            throw new Exception("Metadata for '" + TagName +
                "' can not be used on elements of type '" +
                e.Tag.Name + "'.");
        }

        //determine discardability (is that a word?)
        return Discarder(e);
    }
}
