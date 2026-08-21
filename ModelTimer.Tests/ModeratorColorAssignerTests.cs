using Avalonia.Media;

namespace ModelTimer.Tests;

public class ModeratorColorAssignerTests
{
    [Fact]
    public void Assign_GivesEveryModeratorADistinctColor()
    {
        var map = ModeratorColorAssigner.Assign(new[] { "Luna", "Mato", "Katy", "Ujin" });

        Assert.Equal(4, map.Count);
        Assert.Equal(4, map.Values.Distinct().Count());
    }

    [Fact]
    public void Assign_IsCaseInsensitiveAndDeduplicates()
    {
        // Regression test for the bug where "Luna" and "luna" used to be treated as two
        // different people and could end up with two different bar colors.
        var map = ModeratorColorAssigner.Assign(new[] { "Luna", "luna", "LUNA" });

        Assert.Single(map);
        Assert.True(map.ContainsKey("luna"));
        Assert.True(map.ContainsKey("LUNA"));
    }

    [Fact]
    public void Assign_IsStableAcrossCalls()
    {
        var names = new[] { "Zed", "Amy", "Beth", "Cara", "Dee" };

        var first = ModeratorColorAssigner.Assign(names);
        var second = ModeratorColorAssigner.Assign(names.Reverse());

        foreach (var name in names)
        {
            Assert.Equal(first[name], second[name]);
        }
    }

    [Fact]
    public void Assign_NeverProducesAColorThatCollidesWithTheLostTimeRed()
    {
        // ActivityWindow reserves #f38ba8 for the "Lost Time" bar segment - moderator colors,
        // including the generated overflow palette, must never land close to that hue.
        var lostTimeHue = 350.0; // approx hue of #f38ba8

        var names = Enumerable.Range(0, 40).Select(i => $"Moderator{i}").ToArray();
        var map = ModeratorColorAssigner.Assign(names);

        foreach (var color in map.Values)
        {
            var hue = ToHue(color);
            var distance = Math.Min(Math.Abs(hue - lostTimeHue), 360 - Math.Abs(hue - lostTimeHue));
            Assert.True(distance > 10, $"Color with hue {hue} is too close to the reserved lost-time red.");
        }
    }

    private static double ToHue(Color c)
    {
        double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;
        if (delta == 0) return 0;

        double hue;
        if (max == r) hue = 60 * (((g - b) / delta) % 6);
        else if (max == g) hue = 60 * (((b - r) / delta) + 2);
        else hue = 60 * (((r - g) / delta) + 4);

        return hue < 0 ? hue + 360 : hue;
    }
}
