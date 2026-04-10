using System;

namespace RaspberryBeret.Styling;
internal class Style
{
    public Style(StyleMetadata metadata, int specificity, bool important,
        StyleValue value)
    {
        init(metadata, specificity, important);
        Value = value;
    }

    public string Name { get; private set; } = string.Empty;
    public StyleMetadata Metadata { get; private set; } = new PdfmlBorder(); // arbitrary -- value set simply to appease the compiler
    public int Specificity { get; private set; }
    public bool Important { get; set; }
    public StyleValue Value { get; set; }

    private void init(StyleMetadata metadata, int specificity, bool important)
    {
        if (metadata == null)
        {
            throw new ArgumentNullException("style metadata is required for instanciation of Style");
        }

        Name = metadata.Name;
        Metadata = metadata;
        Specificity = specificity;
        Important = important;
    }
}
