using Microsoft.AspNetCore.Components;

namespace AkLogger.Helpers;

/// <summary>Small shared view helpers: the inline line icons and the categorical colour tokens of the design system.</summary>
public static class Ui
{
    private static readonly Dictionary<string, string> Icons = new()
    {
        ["cal"] = "<rect x=\"2\" y=\"3\" width=\"12\" height=\"11\" rx=\"2\"/><path d=\"M2 7h12M5 1.5v3M11 1.5v3\"/>",
        ["list"] = "<path d=\"M6 4h8M6 8h8M6 12h8M2.5 4h.5M2.5 8h.5M2.5 12h.5\"/>",
        ["user"] = "<circle cx=\"8\" cy=\"5.5\" r=\"2.8\"/><path d=\"M2.5 14c.8-3 3-4.5 5.5-4.5s4.7 1.5 5.5 4.5\"/>",
        ["chev"] = "<path d=\"M6 4l4 4-4 4\"/>",
        ["caret"] = "<path d=\"M4 6l4 4 4-4\"/>",
        ["sliders"] = "<path d=\"M2 4.5h7M13 4.5h1M2 11.5h2M8 11.5h6\"/><circle cx=\"11\" cy=\"4.5\" r=\"1.8\"/><circle cx=\"6\" cy=\"11.5\" r=\"1.8\"/>",
        // cog: a thick dashed ring draws the teeth
        ["gear"] = "<circle cx=\"8\" cy=\"8\" r=\"5.6\" stroke-width=\"2.6\" stroke-dasharray=\"2.2 2.2\" stroke-linecap=\"butt\"/><circle cx=\"8\" cy=\"8\" r=\"4.2\"/><circle cx=\"8\" cy=\"8\" r=\"1.6\"/>",
        ["menu"] = "<path d=\"M2.5 4h11M2.5 8h11M2.5 12h11\"/>",
        ["plus"] = "<path d=\"M8 3v10M3 8h10\"/>",
        ["back"] = "<path d=\"M14 8H2M6 4L2 8l4 4\"/>",   // <-: previous page
        ["eye"] = "<path d=\"M1.5 8S3.9 3.5 8 3.5 14.5 8 14.5 8 12.1 12.5 8 12.5 1.5 8 1.5 8z\"/><circle cx=\"8\" cy=\"8\" r=\"2\"/>",
        ["eyeoff"] = "<path d=\"M1.5 8S3.9 3.5 8 3.5 14.5 8 14.5 8 12.1 12.5 8 12.5 1.5 8 1.5 8zM2.5 13.5l11-11\"/>",
        ["expand"] = "<path d=\"M9.5 2.5h4v4M13.5 2.5L9 7M6.5 13.5h-4v-4M2.5 13.5L7 9\"/>",   // focus a compact tile
        ["collapse"] = "<path d=\"M13.5 2.5L9 7M9 3.5V7h3.5M2.5 13.5L7 9M7 12.5V9H3.5\"/>",   // minimise the open tile (narrow windows)
        ["up"] = "<path d=\"M4 10l4-4 4 4\"/>",
        ["down"] = "<path d=\"M4 6l4 4 4-4\"/>",
        ["chart"] = "<path d=\"M2 13.5h12\"/><rect x=\"3.2\" y=\"8\" width=\"2.6\" height=\"5.5\" rx=\".7\"/><rect x=\"6.7\" y=\"3\" width=\"2.6\" height=\"10.5\" rx=\".7\"/><rect x=\"10.2\" y=\"6\" width=\"2.6\" height=\"7.5\" rx=\".7\"/>",
        ["doc"] = "<path d=\"M3.5 1.5h6L12.5 4.5v10h-9zM9.5 1.5v3h3M5.5 8h5M5.5 11h5\"/>",   // a page: the run's log
        ["trash"] = "<path d=\"M2.5 4h11M6 4V2.5h4V4M4 4l.7 9.5h6.6L12 4\"/>",
    };

    private static MarkupString Svg(string inner) => new(
        $"<svg viewBox=\"0 0 16 16\" width=\"14\" height=\"14\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.6\" stroke-linecap=\"round\" stroke-linejoin=\"round\" aria-hidden=\"true\">{inner}</svg>");

    /// <summary>One of the named icons above.</summary>
    public static MarkupString Icon(string name) => Svg(Icons[name]);

    /// <summary>An icon from the data of one svg path in a 16x16 box (a view descriptor's Icon).</summary>
    public static MarkupString PathIcon(string pathData) => Svg($"<path d=\"{System.Net.WebUtility.HtmlEncode(pathData)}\"/>");

    /// <summary>The twelve categorical colour tokens series take in turn; --stone is the neutral "none".</summary>
    public static readonly IReadOnlyList<string> Palette = ["--sky", "--lilac", "--sun", "--peach", "--mint", "--sage", "--rose", "--teal", "--sand", "--green", "--indigo", "--orchid"];

    /// <summary>The value's colour from <paramref name="map"/>, else a stable pick from the palette by its name (string.GetHashCode changes per process).</summary>
    public static string Pick(string name, IReadOnlyDictionary<string, string>? map = null)
    {
        if (map is not null && map.TryGetValue(name, out string? token)) return token;
        uint h = 2166136261;
        foreach (char c in name) h = (h ^ c) * 16777619;
        return Palette[(int)(h % (uint)Palette.Count)];
    }
}
