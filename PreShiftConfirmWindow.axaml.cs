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
using System.Linq;
using System.Text.Json;

namespace ModelTimer;

public partial class PreShiftConfirmWindow : Window
{
    public bool Confirmed { get; private set; }

    private readonly string _model;
    private readonly string _moderator;
    private readonly int _durationHours;
    private readonly int _durationMinutes;

    public PreShiftConfirmWindow(string model, string moderator, int durationHours, int durationMinutes)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _durationHours = durationHours;
        _durationMinutes = durationMinutes;

        RecapModelText.Text = $"Model: {_model}";
        RecapModeratorText.Text = $"Moderator: {_moderator}";
        RecapDurationText.Text = $"Duration goal: {_durationHours}h {_durationMinutes}min";

        LoadHandoffNotes();
        LoadVipFans();

        ChkCamera.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkMic.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkHotkeys.IsCheckedChanged += (s, e) => UpdateStartButtonState();

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

    private void UpdateStartButtonState()
    {
        BtnConfirmStart.IsEnabled = (ChkCamera.IsChecked ?? false) &&
                                     (ChkMic.IsChecked ?? false) &&
                                     (ChkHotkeys.IsChecked ?? false);
    }

    private static readonly string[] RatingLabels =
    {
        "Not rated", "1 - Rough", "2 - Okay", "3 - Good", "4 - Great", "5 - Amazing"
    };

    private void LoadHandoffNotes()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
            if (!File.Exists(path)) return;

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<ShiftDataFile>(json);
            if (data?.Shifts == null) return;

            var lastShift = data.Shifts
                .Where(s => s.StopTime.HasValue && string.Equals(s.Model?.Trim(), _model.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(s => s.StopTime)
                .FirstOrDefault();

            if (lastShift == null) return;

            var worked = new TimeSpan(0, 0, lastShift.ElapsedHours, lastShift.ElapsedMinutes, lastShift.ElapsedSeconds);
            HandoffMetaText.Text = $"By {lastShift.Moderator} · {lastShift.StopTime:yyyy-MM-dd HH:mm} · Worked {worked:hh\\:mm\\:ss}";
            HandoffSummaryText.Text = string.IsNullOrWhiteSpace(lastShift.SessionSummary)
                ? "No summary was left for the previous shift."
                : lastShift.SessionSummary;

            if (!string.IsNullOrWhiteSpace(lastShift.GoodMembers))
            {
                HandoffGoodMembersText.Text = $"✅ Good members: {lastShift.GoodMembers}";
                HandoffGoodMembersText.IsVisible = true;
            }

            if (!string.IsNullOrWhiteSpace(lastShift.IssuesToWatch))
            {
                HandoffIssuesText.Text = $"⚠ Watch for: {lastShift.IssuesToWatch}";
                HandoffIssuesText.IsVisible = true;
            }

            if (lastShift.PerformanceRating > 0 && lastShift.PerformanceRating < RatingLabels.Length)
            {
                HandoffRatingText.Text = $"Model mood last shift: {RatingLabels[lastShift.PerformanceRating]}";
                HandoffRatingText.IsVisible = true;
            }
        }
        catch
        {
        }
    }

    private void LoadVipFans()
    {
        VipFansPanel.Children.Clear();
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crm_data.json");
            if (!File.Exists(path))
            {
                AddNoFansMessage();
                return;
            }

            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<CrmDataFile>(json);
            var matches = data?.Records?
                .Where(r => string.Equals(r.Model?.Trim(), _model.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList() ?? new List<CrmEntry>();

            if (matches.Count == 0)
            {
                AddNoFansMessage();
                return;
            }

            foreach (var fan in matches)
            {
                VipFansPanel.Children.Add(BuildFanRow(fan));
            }
        }
        catch
        {
            AddNoFansMessage();
        }
    }

    private void AddNoFansMessage()
    {
        VipFansPanel.Children.Add(new TextBlock
        {
            Text = "No flagged fan records yet for this model.",
            Foreground = new SolidColorBrush(Color.Parse("#FF888888")),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
    }

    private Border BuildFanRow(CrmEntry fan)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = fan.User,
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 13,
            FontWeight = FontWeight.Bold
        });

        if (!string.IsNullOrWhiteSpace(fan.Habits))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Habits: {fan.Habits}",
                Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(fan.Triggers))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Triggers: {fan.Triggers}",
                Foreground = new SolidColorBrush(Color.Parse("#FFf9e2af")),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        if (!string.IsNullOrWhiteSpace(fan.Notes))
        {
            panel.Children.Add(new TextBlock
            {
                Text = $"Notes: {fan.Notes}",
                Foreground = new SolidColorBrush(Color.Parse("#FFCCCCCC")),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            });
        }

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = false;
        Close();
    }

    private void BtnConfirmStart_Click(object sender, RoutedEventArgs e)
    {
        Confirmed = true;
        Close();
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

    private class CrmDataFile
    {
        public List<CrmEntry>? Records { get; set; }
    }

    private class CrmEntry
    {
        public int Id { get; set; }
        public string User { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public string Site { get; set; } = string.Empty;
        public string Habits { get; set; } = string.Empty;
        public string Triggers { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
