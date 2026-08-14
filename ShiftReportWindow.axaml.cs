using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.IO;

namespace ModelTimer;

public partial class ShiftReportWindow : Window
{
    private readonly string _model;
    private readonly string _moderator;
    private readonly string _dataFilePath;
    private bool _submitted;

    public ShiftReportWindow(string model, string moderator, TimeSpan elapsed)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");

        RecapText.Text = $"Model: {model}  |  Moderator: {moderator}  |  Worked: {elapsed:hh\\:mm\\:ss}";

        Closing += (s, e) =>
        {
            if (!_submitted) e.Cancel = true;
        };

        try
        {
            Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://ModelTimer/Assets/favicon.ico"))));
        }
        catch
        {
        }
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void SummaryTextBox_TextChanged(object? sender, TextChangedEventArgs e)
    {
        BtnSubmit.IsEnabled = !string.IsNullOrWhiteSpace(SummaryTextBox.Text);
    }

    private void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        if (SaveReport())
        {
            _submitted = true;
            Close();
        }
        else
        {
            ShowSaveFailedWarning();
        }
    }

    private bool SaveReport()
    {
        var data = JsonStore.Load<ShiftDataFile>(_dataFilePath);
        if (data?.Shifts == null || data.Shifts.Count == 0) return false;

        var lastEntry = data.Shifts[^1];
        if (!string.Equals(lastEntry.Model?.Trim(), _model.Trim(), StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(lastEntry.Moderator?.Trim(), _moderator.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        lastEntry.SessionSummary = SummaryTextBox.Text?.Trim() ?? string.Empty;
        lastEntry.GoodMembers = GoodMembersTextBox.Text?.Trim() ?? string.Empty;
        lastEntry.IssuesToWatch = IssuesTextBox.Text?.Trim() ?? string.Empty;
        lastEntry.PerformanceRating = RatingComboBox.SelectedIndex;

        return JsonStore.Save(_dataFilePath, data);
    }

    private void ShowSaveFailedWarning()
    {
        var dialog = new Window
        {
            Title = "Save Failed",
            Width = 380,
            Height = 190,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = "Couldn't save the shift report to disk. Please note it down manually, then try Submit again.",
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });

        var okBtn = new Button
        {
            Content = "OK",
            Width = 100,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#FFf9e2af")),
            Foreground = new SolidColorBrush(Color.Parse("#FF000000")),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };
        okBtn.Click += (s, e) => dialog.Close();

        panel.Children.Add(okBtn);
        dialog.Content = panel;
        dialog.ShowDialog(this);
    }

    private class ShiftDataFile
    {
        public List<string>? Models { get; set; }
        public List<string>? Moderators { get; set; }
        public List<ShiftEntry>? Shifts { get; set; }
    }

    private class ShiftEntry
    {
        public string Model { get; set; } = string.Empty;
        public string Moderator { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public int DurationHours { get; set; }
        public int DurationMinutes { get; set; }
        public int ElapsedHours { get; set; }
        public int ElapsedMinutes { get; set; }
        public int ElapsedSeconds { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? StopTime { get; set; }
        public DateTime Timestamp { get; set; }
        public string SessionSummary { get; set; } = string.Empty;
        public string GoodMembers { get; set; } = string.Empty;
        public string IssuesToWatch { get; set; } = string.Empty;
        public int PerformanceRating { get; set; }
    }
}
