using BWJ.Core;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RaspberryBeret.Typography;
internal static class TypographyService
{
    public static void RegisterTypeface(ITypefaceService typefaceService)
    {
        MethodGuard.NoNull(new { typefaceService });
        if (Stringy.Empty(typefaceService.Name))
        {
            throw new Exception("Font name must be provided on typeface service");
        }

        lock(@lock)
        {
            var key = typefaceService.Name.ToLower().Trim();
            if(typefaces.ContainsKey(key))
            {
                typefaces[key] = typefaceService;
            }
            else
            {
                typefaces.Add(key, typefaceService);
            }
        }
    }

    public static ITypefaceService? GetTypeface(string fontName)
    {
        MethodGuard.NoEmptyString(new { fontName });

        ITypefaceService? typefaceService = null;
        lock (@lock)
        {
            var key = fontName.ToLower().Trim();
            if(typefaces.ContainsKey(key))
            {
                typefaceService = typefaces[key];
            }
        }

        return typefaceService;
    }

    public static bool InUse()
    {
        lock(@lock)
        {
            return typefaces.Any();
        }
    }

    private static readonly Dictionary<string, ITypefaceService> typefaces = new();
    private static object @lock = new();
}
