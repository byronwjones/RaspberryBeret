namespace RaspberryBeret.ReferenceData;
public interface ICloudResourceService
{
    Task<byte[]> Fetch(CloudResource cloudResource);
}
