using System.Reflection;
using QuestPDF.Drawing;

namespace Kasa.Api.Pdf;

/// <summary>
/// Sarabun (Thai) font ailesini QuestPDF'e bir kez kaydeder. TTF'ler assembly'ye gömülü
/// (EmbeddedResource) olduğundan kayıt host'un font zincirinden bağımsızdır: Docker'da da,
/// test host'unda da aynı garanti ("container font zincirinde yok" dersine karşı sigorta).
/// </summary>
internal static class SarabunFonts
{
    private static readonly Lock Gate = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        if (_registered)
            return;

        lock (Gate)
        {
            if (_registered)
                return;

            var assembly = typeof(SarabunFonts).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase))
                    continue;

                using var stream = assembly.GetManifestResourceStream(name);
                if (stream is not null)
                    FontManager.RegisterFont(stream);
            }

            _registered = true;
        }
    }
}
