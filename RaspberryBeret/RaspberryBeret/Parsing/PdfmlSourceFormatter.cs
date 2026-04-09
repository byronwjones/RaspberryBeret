using System.Text;

namespace RaspberryBeret.Parsing;
internal static class PdfmlSourceFormatter
{
    /// <summary>
    /// Converts a collection of PDFML source code lines into a single line
    /// string of PDFML code
    /// </summary>
    /// <param name="code">PDFML source code collection</param>
    /// <returns>PDFML string</returns>
    public static string ToSingleLinePDFML(IEnumerable<PdfmlSourceLine> code)
    {
        StringBuilder sb = new StringBuilder();
        foreach (var line in code)
        {
            sb.Append(line.Text);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Converts a collection of PDFML source code lines into a 'pretty printed'
    /// multiple line string of PDFML code
    /// </summary>
    /// <param name="code">PDFML source code collection</param>
    /// <param name="indentCharacter">The character to use for code indentation</param>
    /// <returns>PDFML string</returns>
    public static string ToMultiLinePDFML(IEnumerable<PdfmlSourceLine> code,
        string indent = "\t")
    {
        StringBuilder sb = new StringBuilder();
        foreach (var line in code)
        {
            //get indentation string
            string indentSequence = string.Empty;
            for (int i = 0; i < line.Indentation; i++)
            {
                indentSequence += indent;
            }

            sb.AppendLine(indentSequence + line.Text);
        }

        return sb.ToString();
    }
}
