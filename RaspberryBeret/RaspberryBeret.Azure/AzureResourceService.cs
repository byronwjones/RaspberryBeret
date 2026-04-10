using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using RaspberryBeret.ReferenceData;
using System;
using System.IO;
using System.Threading.Tasks;

namespace RaspberryBeret.Azure;

internal class AzureResourceService : ICloudResourceService
{
    private readonly CloudBlobClient blobClient;
     
    public AzureResourceService(string connectionString)
    {
        var storageAccount = CloudStorageAccount.Parse(connectionString);
        blobClient = storageAccount.CreateCloudBlobClient();
    }

    public async Task<byte[]> Fetch(CloudResource cloudResource)
    {
        var b = await GetBlob(cloudResource);
        var exists = b is not null && await b.ExistsAsync();
        if (exists == false) { return Array.Empty<byte>(); }

        byte[] data = [];
        using (var memoryStream = new MemoryStream())
        {
            await b!.DownloadToStreamAsync(memoryStream);
            data = memoryStream.ToArray();
        }

        return data;
    }

    public async Task SaveBlob(byte[] data, CloudResource cloudResource)
    {
        var blob = await GetBlob(cloudResource);
        if(blob is null)
        {
            throw new Exception("Write failure: Storage container does not exist, and could not be created");
        }

        await blob.UploadFromByteArrayAsync(data, 0, data.Length);
    }

    private async Task<CloudBlobContainer> GetContainer(CloudResource cloudResource, bool createIfNotExists)
    {
        var container = blobClient.GetContainerReference(cloudResource.Container.ToLower());
        if (createIfNotExists)
        {
            await container.CreateIfNotExistsAsync();
        }
        return container;
    }

    /// <summary>
    /// Gets a reference to the given blob
    /// </summary>
    /// <param name="reference">Information about desired blob</param>
    /// <returns>Blob reference, or null if its container does not exist</returns>
    private async Task<CloudBlockBlob?> GetBlob(CloudResource reference)
    {
        var container = await GetContainer(reference, createIfNotExists: false);
        var containerExists = await container.ExistsAsync();
        if (containerExists == false) { return null; }
        return container.GetBlockBlobReference(reference.ResourceName);
    }
}
