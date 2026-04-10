namespace RaspberryBeret;
public static class RB
{
    private static readonly PdfService pdfService = new PdfService();

    public static PdfService Pdf { get { return pdfService; } }
}
