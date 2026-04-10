using System.Threading.Tasks;

namespace RaspberryBeret.ReferenceData;
public interface ICloudResourceService
{
    Task<byte[]> Fetch(CloudResource cloudResource);
}
