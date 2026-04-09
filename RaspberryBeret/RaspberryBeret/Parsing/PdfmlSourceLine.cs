using RaspberryBeret.Elements;

namespace RaspberryBeret.Parsing;
internal class PdfmlSourceLine
{
    /// <summary>
    /// Optional element associated with this line of code
    /// </summary>
    public Element? Element { get; set; }

    /// <summary>
    /// The number of tab characters to prepend this line of code with if 
    /// rendering the code with 'pretty print' formatting
    /// </summary>
    public int Indentation { get; set; }

    /// <summary>
    /// The text rendered on this line of code
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
