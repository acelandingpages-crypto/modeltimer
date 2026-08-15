using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ModelTimer;

internal static class AskResultRenderer
{
    public static void Render(StackPanel target, AskResult result)
    {
        target.Children.Clear();

        target.Children.Add(new TextBlock
        {
            Text = string.IsNullOrWhiteSpace(result.Headline) ? "No answer returned." : result.Headline,
            Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
            FontSize = 14,
            FontWeight = FontWeight.Bold,
            TextWrapping = TextWrapping.Wrap
        });

        foreach (var detail in result.Details)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
            var bullet = new TextBlock
            {
                Text = "•",
                Foreground = new SolidColorBrush(Color.Parse("#FFCCCCCC")),
                FontSize = 13,
                Margin = new Thickness(0, 0, 6, 0)
            };
            var text = new TextBlock
            {
                Text = detail,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(bullet, 0);
            Grid.SetColumn(text, 1);
            row.Children.Add(bullet);
            row.Children.Add(text);
            target.Children.Add(row);
        }

        if (result.Chart.Count > 0)
        {
            target.Children.Add(BuildMiniChart(result.ChartTitle, result.Chart));
        }
    }

    private static StackPanel BuildMiniChart(string title, List<AskChartPoint> points)
    {
        var panel = new StackPanel { Spacing = 6, Margin = new Thickness(0, 6, 0, 0) };

        if (!string.IsNullOrWhiteSpace(title))
        {
            panel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = new SolidColorBrush(Color.Parse("#FFcba6f7")),
                FontSize = 12,
                FontWeight = FontWeight.Bold
            });
        }

        var max = points.Count > 0 ? points.Max(p => Math.Abs(p.Value)) : 0;
        if (max <= 0) max = 1;

        foreach (var point in points)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("110,*,60"), ColumnSpacing = 8 };

            var label = new TextBlock
            {
                Text = point.Label,
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center
            };

            var fraction = Math.Clamp(Math.Abs(point.Value) / max, 0.02, 1.0);
            var barGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions($"{fraction:0.###}*,{Math.Max(0.001, 1 - fraction):0.###}*")
            };
            var barFill = new Border { Background = new SolidColorBrush(Color.Parse("#FFa6e3a1")) };
            Grid.SetColumn(barFill, 0);
            barGrid.Children.Add(barFill);

            var track = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FF3E3E42")),
                CornerRadius = new CornerRadius(3),
                Height = 16,
                ClipToBounds = true,
                VerticalAlignment = VerticalAlignment.Center,
                Child = barGrid
            };

            var valueText = new TextBlock
            {
                Text = FormatChartValue(point.Value),
                Foreground = new SolidColorBrush(Color.Parse("#FFCCCCCC")),
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            Grid.SetColumn(label, 0);
            Grid.SetColumn(track, 1);
            Grid.SetColumn(valueText, 2);
            row.Children.Add(label);
            row.Children.Add(track);
            row.Children.Add(valueText);
            panel.Children.Add(row);
        }

        return panel;
    }

    private static string FormatChartValue(double value) =>
        value == Math.Floor(value) ? value.ToString("0") : value.ToString("0.0");
}
