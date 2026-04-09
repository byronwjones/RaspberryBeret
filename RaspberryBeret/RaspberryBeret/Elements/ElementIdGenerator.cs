using BWJ.Core.Chronology;

namespace RaspberryBeret.Elements;
internal static class ElementIdGenerator
{
    public static string CreateId()
    {
        Thread.Sleep(1); // ensure a unique value
        return DateTime.UtcNow.ToLong().ToString();
    }
}
