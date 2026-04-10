using BWJ.Core;
using RaspberryBeret.ReferenceData;

namespace RaspberryBeret.Azure;
public static class PdfServiceExtensions
{
    /// <summary>
    /// Creates a PDF document using the given data, storing in the storage account indicating
    /// </summary>
    /// <param name="model">Data model used for rendering PDF</param>
    /// <param name="pdfmlTemplate">Full path, URL, or cloud resource reference to PDFML template used to render PDF</param>
    /// <param name="connectionString">Azure storage account connection string</param>
    /// <param name="containerName">Container where PDF will be stored</param>
    /// <param name="pdfBlobName">PDF file name</param>
    public static void CreateOnAzure(this PdfService pdfService, object? model, string pdfmlTemplate,
        string connectionString, string containerName, string pdfBlobName)
    {
        MethodGuard.NoEmptyString(new { pdfmlTemplate, connectionString, containerName, pdfBlobName });
        var ars = new AzureResourceService(connectionString);

        var data = pdfService.Create(model, pdfmlTemplate, ars);
        ars.SaveBlob(data, new CloudResource(containerName, pdfBlobName)).Wait();
    }
}
