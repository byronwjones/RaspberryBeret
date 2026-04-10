namespace RaspberryBeret.ReferenceData;
public class CloudResource
{
    public CloudResource(string container, string resourceName)
    {
        Container = container;
        ResourceName = resourceName;
    }

    public string Container { get; }
    public string ResourceName { get; }
}
