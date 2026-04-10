using System;

namespace RaspberryBeret.Parsing;
public class ParsingException : Exception
{
    public ParsingException(string message, string sourcePDFML, int sourceErrorIndex) :
        base(message)
    {
        setSourcePDFML(sourcePDFML, sourceErrorIndex);
    }
    public ParsingException(string message, string sourcePDFML, int sourceErrorIndex,
        Exception? innerException) :
        base(message, innerException)
    {
        setSourcePDFML(sourcePDFML, sourceErrorIndex);
    }

    public string SourcePDFML
    {
        get { return Data["Source"]?.ToString() ?? string.Empty; }
    }

    public string SourceErrorSnippet
    {
        get { return Data["ErrorAt"]?.ToString() ?? string.Empty; }
    }

    private void setSourcePDFML(string code, int errorIndex)
    {
        Data.Add("Source", code);
        Data.Add("ErrorAt", getErrorSnippet(code, errorIndex));
    }

    private string getErrorSnippet(string pdfml, int startIndex)
    {
        int SNIPPET_LENGTH = 50;

        if (pdfml == null)
        {
            throw new ArgumentNullException("PDFML input must not be null");
        }

        //startIndex represents a line number in multiline PDFML source
        if (pdfml.Contains("\r\n"))
        {
            return "Line number " + startIndex;
        }

        if (startIndex < 0 || startIndex >= pdfml.Length)
        {
            throw new IndexOutOfRangeException
                ("startIndex value is outside of PDFML string length.");
        }

        if ((pdfml.Length - startIndex) <= SNIPPET_LENGTH)
        {
            return pdfml.Substring(startIndex);
        }
        else
        {
            return pdfml.Substring(startIndex, SNIPPET_LENGTH).TrimEnd() + "...";
        }
    }
}
