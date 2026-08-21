using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace ModelTimer;

/// <summary>
/// Assigns each moderator a distinct, stable color for the Activity chart and its legend. Pulled
/// out of ActivityWindow so the assignment logic (and its "never collide with red" guarantee,
/// since red is reserved for the Lost Time indicator) can be unit tested directly.
/// </summary>
internal static class ModeratorColorAssigner
{
    private static readonly Color[] Palette =
    {
        Color.FromRgb(0xa6, 0xe3, 0xa1), // green
        Color.FromRgb(0x89, 0xb4, 0xfa), // blue
        Color.FromRgb(0xcb, 0xa6, 0xf7), // mauve
        Color.FromRgb(0xf9, 0xe2, 0xaf), // yellow
        Color.FromRgb(0xfa, 0xb3, 0x87), // peach
        Color.FromRgb(0x94, 0xe2, 0xd5), // teal
        Color.FromRgb(0xf5, 0xc2, 0xe7), // pink
        Color.FromRgb(0x74, 0xc7, 0xec), // sapphire
        Color.FromRgb(0xb4, 0xbe, 0xfe), // lavender
        Color.FromRgb(0x89, 0xdc, 0xeb), // sky
    };

    /// <summary>Builds a case-insensitive name-&gt;color map. Order is alphabetical so the
    /// assignment is stable across refreshes and independent of which date range is filtered.</summary>
    public static Dictionary<string, Color> Assign(IEnumerable<string> moderatorNames)
    {
        var names = moderatorNames
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => m, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var map = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < names.Count; i++)
        {
            if (i < Palette.Length)
            {
                map[names[i]] = Palette[i];
                continue;
            }

            // Beyond the fixed palette, spread hues evenly and steer clear of the red band
            // (~335-25 deg, a wide margin around the reserved Lost Time red at ~350 deg) so
            // extra moderators never collide with, or land uncomfortably close to, that color.
            var hue = (i * 47) % 360;
            if (hue < 25 || hue > 335) hue = (hue + 40) % 360;
            map[names[i]] = HsvToColor(hue, 0.55, 0.85);
        }

        return map;
    }

    internal static Color HsvToColor(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var x = c * (1 - Math.Abs((hue / 60.0) % 2 - 1));
        var m = value - c;
        double r1, g1, b1;
        if (hue < 60) (r1, g1, b1) = (c, x, 0.0);
        else if (hue < 120) (r1, g1, b1) = (x, c, 0.0);
        else if (hue < 180) (r1, g1, b1) = (0.0, c, x);
        else if (hue < 240) (r1, g1, b1) = (0.0, x, c);
        else if (hue < 300) (r1, g1, b1) = (x, 0.0, c);
        else (r1, g1, b1) = (c, 0.0, x);

        return Color.FromRgb((byte)((r1 + m) * 255), (byte)((g1 + m) * 255), (byte)((b1 + m) * 255));
    }
}
