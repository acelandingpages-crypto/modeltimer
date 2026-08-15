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
using System.Threading.Tasks;

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
        _ = LoadRiskFlagAsync();

        ChkLighting.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkFraming.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkObsScene.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkMic.IsCheckedChanged += (s, e) => UpdateStartButtonState();
        ChkBackground.IsCheckedChanged += (s, e) => UpdateStartButtonState();
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
        BtnConfirmStart.IsEnabled = (ChkLighting.IsChecked ?? false) &&
                                     (ChkFraming.IsChecked ?? false) &&
                                     (ChkObsScene.IsChecked ?? false) &&
                                     (ChkMic.IsChecked ?? false) &&
                                     (ChkBackground.IsChecked ?? false) &&
                                     (ChkHotkeys.IsChecked ?? false);
    }

    private static readonly string[] RatingLabels =
    {
        "Not rated", "1 - Rough", "2 - Okay", "3 - Good", "4 - Great", "5 - Amazing"
    };

    private void LoadHandoffNotes()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        var data = JsonStore.Load<ShiftDataFile>(path);
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

    private async Task LoadRiskFlagAsync()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            var settings = JsonStore.Load<AppSettings>(settingsPath);
            if (!AiSummaryService.IsConfigured(settings)) return;

            var shiftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
            var crmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crm_data.json");
            var shiftData = JsonStore.Load<ShiftDataFile>(shiftPath);
            var crmData = JsonStore.Load<CrmDataFile>(crmPath);

            var recentIssues = (shiftData?.Shifts ?? new List<ShiftEntry>())
                .Where(s => string.Equals(s.Model?.Trim(), _model.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(s.IssuesToWatch))
                .OrderByDescending(s => s.Timestamp)
                .Take(10)
                .Select(s => $"- {s.Timestamp:yyyy-MM-dd} ({s.Moderator}): {s.IssuesToWatch}")
                .ToList();

            var fanTriggers = (crmData?.Records ?? new List<CrmEntry>())
                .Where(r => string.Equals(r.Model?.Trim(), _model.Trim(), StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(r.Triggers))
                .Select(r => $"- {r.User}: {r.Triggers}")
                .ToList();

            // Not enough signal to look for a "recurring" pattern - skip the AI call entirely.
            if (recentIssues.Count < 2 && fanTriggers.Count == 0) return;

            var context = "ISSUES FROM RECENT SHIFTS:\n" + string.Join("\n", recentIssues) +
                          "\n\nFAN TRIGGERS:\n" + string.Join("\n", fanTriggers);

            var flag = await AiSummaryService.CheckRiskPatternAsync(settings!, _model, context);
            if (flag.HasPattern && !string.IsNullOrWhiteSpace(flag.Summary))
            {
                RiskFlagText.Text = flag.Summary;
                RiskFlagBorder.IsVisible = true;
            }
        }
        catch
        {
            // Best-effort background check - a failure here should never block starting a shift.
        }
    }

    private void LoadVipFans()
    {
        VipFansPanel.Children.Clear();

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crm_data.json");
        var data = JsonStore.Load<CrmDataFile>(path);
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

}
