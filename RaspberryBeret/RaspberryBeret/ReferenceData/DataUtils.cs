using BWJ.Net.Http;
using BWJ.Net.Http.RequestBuilder;
using RaspberryBeret.Elements;
using RaspberryBeret.Parsing;
using System.Text;
using System.Text.RegularExpressions;

namespace RaspberryBeret.ReferenceData;
internal static class DataUtils
{
    private static FluentHttpClient httpClient = new();

    /// <summary>
    /// Gets whether or not the given string refers to a resource hosted in cloud storage
    /// </summary>
    /// <param name="src">string to analyze</param>
    /// <returns>True if the string is a cloud resource reference</returns>
    public static bool IsSourceCloudStorage(string src)
    {
        if (string.IsNullOrWhiteSpace(src)) { return false; }
        return Regex.IsMatch(src, @"^cloud\:[a-z][a-z\-0-9]{2,62}[\\/].{1,1024}$");
    }

    /// <summary>
    /// Gets whether or not the given string refers to Base-64 encoded data
    /// </summary>
    /// <param name="src">string to analyze</param>
    /// <returns>True if the string is Base-64 encoded data</returns>
    public static bool IsSourceBase64(string src)
    {
        if (string.IsNullOrWhiteSpace(src)) { return false; }
        return Regex.IsMatch(src, @"^base64\:[A-Za-z0-9+/=]+$");
    }

    /// <summary>
    /// Gets whether or not the given string refers to a web resource
    /// </summary>
    /// <param name="src">string to analyze</param>
    /// <returns>True if the string is a a web resource</returns>
    public static bool IsSourceWebBased(string src)
    {
        if (string.IsNullOrWhiteSpace(src)) { return false; }
        return Regex.IsMatch(src, @"^http(?:s)?\:\/\/");
    }

    /// <summary>
    /// Get container/data name from source string, ensuring that all of the components
    /// required for obtaining the data are valid
    /// </summary>
    /// <param name="caller">The name of the method invoking this method</param>
    /// <param name="src">Cloud resource string</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <param name="element">(Optional) PDFML element from which source string was
    /// obtained, used for error reporting</param>
    /// <returns>A CloudResource object</returns>
    public static CloudResource GetCloudReference(string caller, string src, ICloudResourceService? cloudService, Element? element = null)
    {
        Action<string> throwBadArgException = (msg) => {
            if (element != null)
            {
                ParseUtils.ThrowParsingException(element, msg);
            }
            else
            {
                throw new ArgumentException(msg);
            }
        };

        if (cloudService is null)
        {
            throwBadArgException("Use of cloud data references require provision of a service implementing ICloudResourceService");
        }

        //source string must refer to a cloud resource
        if (IsSourceCloudStorage(src) == false)
        {
            throwBadArgException($"File source passed to {caller} must refer to file in blob storage.");
        }

        return GetCloudReference(src);
    }

    /// <summary>
    /// Get data contents of a cloud resource referenced by the given source string
    /// </summary>
    /// <param name="src">Cloud resource string</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <param name="element">(Optional) PDFML element from which source string was
    /// obtained, used for error reporting</param>
    /// <returns>Byte array contents of resource</returns>
    public static byte[] GetDataFromBlob(string src, ICloudResourceService? cloudService, Element? element = null)
    {
        var cloudRef = GetCloudReference(nameof(GetDataFromBlob), src, cloudService, element);
        var data = cloudService!.Fetch(cloudRef).Result;
        return data;
    }

    public static byte[] GetDataFromWeb(string src)
    {
        try
        {
            var stream = httpClient.Get(src)
                .SendForStreamAsync().Result;
            byte[] data = [];
            using (var memoryStream = new MemoryStream())
            {
                stream.CopyTo(memoryStream);
                data = memoryStream.ToArray();
            }

            return data;
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    /// <summary>
    /// Get text contents of a cloud resource referenced by the given source string
    /// </summary>
    /// <param name="src">Cloud resource string</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <param name="element">(Optional) PDFML element from which source string was
    /// obtained, used for error reporting</param>
    /// <returns>Text contents of blob</returns>
    public static string GetTextFromBlob(string src, ICloudResourceService? cloudService, Element? element = null)
    {
        var data = GetDataFromBlob(src, cloudService, element);
        return GetStringFromData(data);
    }

    public static string GetTextFromWeb(string src)
    {
        try
        {
            var txt = httpClient.Get(src)
                .SendForTextAsync().Result;
            return txt;
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Get data contents from the cloud resource referenced by the given source string as a
    /// Base-64 encoded string 
    /// </summary>
    /// <param name="src">Cloud resource string</param>
    /// <param name="cloudService">Service to retrieve cloud-hosted resources</param>
    /// <param name="element">(Optional) PDFML element from which source string was
    /// obtained, used for error reporting</param>
    /// <returns>Base-64 encoded cloud resource data, prefixed by 'base64:',
    /// or null if file does not exist/content is empty</returns>
    public static string? GetBase64StringFromBlob(string src, ICloudResourceService? cloudService, Element element = null)
    {
        var data = GetDataFromBlob(src, cloudService, element);
        return (data.Length > 0) ?
            "base64:" + Convert.ToBase64String(data) : null;
    }

    public static string? GetBase64StringFromWeb(string src)
    {
        var data = GetDataFromWeb(src);
        return (data.Length > 0) ?
            "base64:" + Convert.ToBase64String(data) : null;
    }

    /// <summary>
    /// Gets container/data name from string
    /// </summary>
    /// <param name="src">Cloud resource string</param>
    /// <returns>A CloudResource object</returns>
    private static CloudResource GetCloudReference(string src)
    {
        if (!IsSourceCloudStorage(src))
        {
            throw new ArgumentException(nameof(src), "Argument is not a cloud resource reference");
        }

        src = src.Substring(6);//remove 'cloud:'
        //get the position of the container / file name divider
        int dividerIndex = src.IndexOfAny(['\\', '/']);

        var r = new CloudResource(src.Remove(dividerIndex), src.Substring(dividerIndex + 1));
        return r;
    }

    private static string GetStringFromData(byte[] data)
    {
        var correctedData = data;
        //remove UTF-8 byte order marker if found
        if (data.Length >= 3 &&
            data[0] == 239 &&
            data[1] == 187 &&
            data[2] == 191)
        {
            //empty string -- the only thing present was the BOM
            if (data.Length == 3) { return string.Empty; }

            correctedData = new byte[data.Length - 3];
            Array.Copy(data, 3, correctedData, 0, data.Length - 3);
        }

        return Encoding.UTF8.GetString(correctedData);
    }
}
