using PdfSharp.Fonts;

namespace RaspberryBeret.Typography;

/// <summary>
/// Resolves fonts using provided resources
/// </summary>
/// <see cref="https://stackoverflow.com/questions/27606877/pdfsharp-private-fonts-for-azure-1-50"/>
class ProvidedFontResolver : IFontResolver
{
    public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        var name = familyName.ToLower().Split('#')[0];
        var fontService = TypographyService.GetTypeface(name);
        if (fontService is not null)
        {
            if (isBold)
            {
                if (isItalic)
                {
                    return new FontResolverInfo($"{fontService.Name}#bi");
                }
                return new FontResolverInfo($"{fontService.Name}#b");
            }
            if (isItalic)
            {
                return new FontResolverInfo($"{fontService.Name}#i");
            }
            return new FontResolverInfo($"{fontService.Name}#");
        }

        // Try to resolve fonts not provided on the local machine
        return PlatformFontResolver.ResolveTypeface(familyName, isBold, isItalic);
    }

    public byte[]? GetFont(string faceName)
    {
        var name = faceName.ToLower().Split('#')[0];
        var fontService = TypographyService.GetTypeface(name);
        if (fontService is not null)
        {
            if(faceName == $"{fontService.Name}#b")
            {
                return fontService.GetBold();
            }
            else if (faceName == $"{fontService.Name}#i")
            {
                return fontService.GetItalic();
            }
            else if (faceName == $"{fontService.Name}#bi")
            {
                return fontService.GetBoldItalic();
            }
            else
            {
                return fontService.GetNormal();
            }
        }

        return null;
    }

    internal static ProvidedFontResolver? GlobalFontResolver = null;

    /// <summary>
    /// Register this as the font resolver
    /// </summary>
    internal static void Apply()
    {
        if (GlobalFontResolver == null || GlobalFontSettings.FontResolver == null)
        {
            if (GlobalFontResolver == null)
                GlobalFontResolver = new ProvidedFontResolver();

            GlobalFontSettings.FontResolver = GlobalFontResolver;
        }
    }
}
