namespace RaspberryBeret.Typography;
public interface ITypefaceService
{
    string Name { get; }

    byte[] GetNormal();

    byte[] GetBold();

    byte[] GetItalic();

    byte[] GetBoldItalic();
}
