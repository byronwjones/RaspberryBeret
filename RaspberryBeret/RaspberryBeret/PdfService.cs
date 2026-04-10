using BWJ.Core;
using RaspberryBeret.ReferenceData;
using RaspberryBeret.Typography;
using System.IO;

namespace RaspberryBeret;
public sealed class PdfService
{
    internal PdfService() { }

    /// <summary>
    /// Creates data representing a PDF document using the given data and PDFML template
    /// on the given stream
    /// </summary>
    /// <param name="model">Data model used for rendering PDF</param>
    /// <param name="pdfmlTemplate">Full path on disk, URL or cloud resource reference, to PDFML
    /// template used to render PDF</param>
    /// <param name="stream">Stream to create PDF document on</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    public void Create(object? model, string pdfmlTemplate, Stream stream,
        ICloudResourceService? cloudService = null)
    {
        MethodGuard.NoNull(new { stream });
        MethodGuard.NoEmptyString(new { pdfmlTemplate });

        if (TypographyService.InUse())
        {
            ProvidedFontResolver.Apply();
        }

        var dom = PdfRenderer.BuildDOM(model, pdfmlTemplate, cloudService);
        PdfRenderer.RenderPDF(dom).Save(stream);
    }

    /// <summary>
    /// Creates a PDF document using the given data and PDFML template, saving the file
    /// to the specified location on disk
    /// </summary>
    /// <param name="model">Data model used for rendering PDF</param>
    /// <param name="pdfmlTemplate">Full path on disk, URL, or cloud resource reference, to PDFML
    /// template used to render PDF</param>
    /// <param name="pdfFileName">Full path of PDF file to create.  An existing file
    /// of the same name will be overwritten.</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    public void Create(object? model, string pdfmlTemplate, string pdfFileName,
        ICloudResourceService? cloudService = null)
    {
        MethodGuard.NoEmptyString(new { pdfmlTemplate, pdfFileName });

        if (TypographyService.InUse())
        {
            ProvidedFontResolver.Apply();
        }

        var dom = PdfRenderer.BuildDOM(model, pdfmlTemplate, cloudService);
        PdfRenderer.RenderPDF(dom).Save(pdfFileName);
    }

    /// <summary>
    /// Creates a PDF document using the given data and PDFML template as a byte array
    /// </summary>
    /// <param name="model">Data model used for rendering PDF</param>
    /// <param name="pdfmlTemplate">Full path to PDFML template used to render PDF</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    public byte[] Create(object? model, string pdfmlTemplate,
        ICloudResourceService? cloudService = null)
    {
        byte[] data = [];
        using (var memstr = new MemoryStream())
        {
            Create(model, pdfmlTemplate, memstr, cloudService);
            data = memstr.ToArray();
        }

        return data;
    }

    public void RegisterTypeface(ITypefaceService typefaceService)
    {
        TypographyService.RegisterTypeface(typefaceService);
    }
}
