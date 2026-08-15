using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModelTimer;

public partial class AskHistoryWindow : Window
{
    private readonly string _shiftDataPath;
    private readonly string _crmDataPath;
    private readonly string _settingsPath;
    private List<ShiftEntry> _lastShiftMatches = new();
    private List<CrmEntry> _lastCrmMatches = new();
    private string _lastQuery = string.Empty;

    public AskHistoryWindow()
    {
        InitializeComponent();

        _shiftDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        _crmDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crm_data.json");
        _settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");

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

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void QueryTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e)
    {
        RunSearch();
    }

    private void RunSearch()
    {
        var query = QueryTextBox.Text?.Trim() ?? string.Empty;
        ShiftResultsPanel.Children.Clear();
        CrmResultsPanel.Children.Clear();
        AiAnswerBorder.IsVisible = false;
        _lastQuery = query;

        if (string.IsNullOrWhiteSpace(query))
        {
            HintText.Text = "Type a keyword or question above, then press Search.";
            _lastShiftMatches = new List<ShiftEntry>();
            _lastCrmMatches = new List<CrmEntry>();
            return;
        }

        var shiftData = JsonStore.Load<ShiftDataFile>(_shiftDataPath);
        var crmData = JsonStore.Load<CrmDataFile>(_crmDataPath);

        _lastShiftMatches = (shiftData?.Shifts ?? new List<ShiftEntry>())
            .Where(s => MatchesShift(s, query))
            .OrderByDescending(s => s.Timestamp)
            .Take(20)
            .ToList();

        _lastCrmMatches = (crmData?.Records ?? new List<CrmEntry>())
            .Where(r => MatchesCrm(r, query))
            .OrderByDescending(r => r.CreatedAt)
            .Take(20)
            .ToList();

        if (_lastShiftMatches.Count == 0)
        {
            ShiftResultsPanel.Children.Add(BuildEmptyRow("No shift reports matched."));
        }
        else
        {
            foreach (var s in _lastShiftMatches)
            {
                ShiftResultsPanel.Children.Add(BuildShiftRow(s));
            }
        }

        if (_lastCrmMatches.Count == 0)
        {
            CrmResultsPanel.Children.Add(BuildEmptyRow("No fan records matched."));
        }
        else
        {
            foreach (var r in _lastCrmMatches)
            {
                CrmResultsPanel.Children.Add(BuildCrmRow(r));
            }
        }

        var total = _lastShiftMatches.Count + _lastCrmMatches.Count;
        HintText.Text = total == 0
            ? "No matches. Try a different keyword."
            : $"Found {total} match(es) for \"{query}\".";
    }

    private static bool MatchesShift(ShiftEntry s, string query) =>
        Contains(s.Model, query) || Contains(s.Moderator, query) || Contains(s.SessionSummary, query) ||
        Contains(s.GoodMembers, query) || Contains(s.IssuesToWatch, query) || Contains(s.Notes, query);

    private static bool MatchesCrm(CrmEntry r, string query) =>
        Contains(r.User, query) || Contains(r.Model, query) || Contains(r.Site, query) ||
        Contains(r.Habits, query) || Contains(r.Triggers, query) || Contains(r.Notes, query);

    private static bool Contains(string? field, string query) =>
        !string.IsNullOrEmpty(field) && field.Contains(query, StringComparison.OrdinalIgnoreCase);

    private Border BuildShiftRow(ShiftEntry s)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{s.Model} · {s.Moderator} · {s.Timestamp:yyyy-MM-dd}",
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 13,
            FontWeight = FontWeight.Bold
        });

        AddSnippetIfMatch(panel, "Summary", s.SessionSummary, "#FFCCCCCC");
        AddSnippetIfMatch(panel, "Good members", s.GoodMembers, "#FFa6e3a1");
        AddSnippetIfMatch(panel, "Issues", s.IssuesToWatch, "#FFf9e2af");
        AddSnippetIfMatch(panel, "Notes", s.Notes, "#FFCCCCCC");

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FF252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    private Border BuildCrmRow(CrmEntry r)
    {
        var panel = new StackPanel { Spacing = 2 };
        panel.Children.Add(new TextBlock
        {
            Text = $"{r.User} · {r.Site} · {r.Model}",
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 13,
            FontWeight = FontWeight.Bold
        });

        AddSnippetIfMatch(panel, "Habits", r.Habits, "#FFa6e3a1");
        AddSnippetIfMatch(panel, "Triggers", r.Triggers, "#FFf9e2af");
        AddSnippetIfMatch(panel, "Notes", r.Notes, "#FFCCCCCC");

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#FF252526")),
            BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10),
            Child = panel
        };
    }

    private void AddSnippetIfMatch(StackPanel panel, string label, string? field, string colorHex)
    {
        if (string.IsNullOrWhiteSpace(field)) return;
        panel.Children.Add(new TextBlock
        {
            Text = $"{label}: {field}",
            Foreground = new SolidColorBrush(Color.Parse(colorHex)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
    }

    private Border BuildEmptyRow(string message)
    {
        return new Border
        {
            Padding = new Thickness(4),
            Child = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.Parse("#FF888888")),
                FontSize = 12
            }
        };
    }

    private const int MaxRecordsPerType = 500;

    private async void BtnAskAi_Click(object sender, RoutedEventArgs e)
    {
        var question = QueryTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(question))
        {
            ShowInfoDialog("Ask AI", "Type a question above first — e.g. \"which model brought in the most hours this month?\" or \"who are our top spenders on Chaturbate?\"");
            return;
        }

        var settings = JsonStore.Load<AppSettings>(_settingsPath);
        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Answering Not Set Up", "Add a provider and API key under Settings to ask questions over the full shift/CRM history. The keyword search above works without AI.");
            return;
        }

        var originalContent = BtnAskAi.Content;
        try
        {
            BtnAskAi.IsEnabled = false;
            BtnAskAi.Content = "Thinking...";
            AiAnswerPanel.Children.Clear();
            AiAnswerPanel.Children.Add(new TextBlock
            {
                Text = "Asking AI, hang on...",
                Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            AiAnswerBorder.IsVisible = true;

            var database = BuildFullDatabaseContext();
            var result = await AiSummaryService.AskAsync(settings!, question, database);
            AskResultRenderer.Render(AiAnswerPanel, result);
        }
        catch (Exception ex)
        {
            AiAnswerBorder.IsVisible = false;
            ShowInfoDialog("AI Answering Unavailable", ex.Message);
        }
        finally
        {
            BtnAskAi.IsEnabled = true;
            BtnAskAi.Content = originalContent;
        }
    }

    private string BuildFullDatabaseContext()
    {
        var shiftData = JsonStore.Load<ShiftDataFile>(_shiftDataPath);
        var crmData = JsonStore.Load<CrmDataFile>(_crmDataPath);

        var shifts = (shiftData?.Shifts ?? new List<ShiftEntry>())
            .OrderByDescending(s => s.Timestamp)
            .Take(MaxRecordsPerType)
            .ToList();
        var fans = (crmData?.Records ?? new List<CrmEntry>())
            .OrderByDescending(r => r.CreatedAt)
            .Take(MaxRecordsPerType)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"SHIFTS ({shifts.Count} of {shiftData?.Shifts?.Count ?? 0} total, most recent first):");
        foreach (var s in shifts)
        {
            var worked = new TimeSpan(0, 0, s.ElapsedHours, s.ElapsedMinutes, s.ElapsedSeconds);
            sb.AppendLine($"- {s.Timestamp:yyyy-MM-dd} | Model: {s.Model} | Moderator: {s.Moderator} | Worked: {worked:hh\\:mm\\:ss} | Rating: {s.PerformanceRating}/5 | Summary: {s.SessionSummary} | Good members: {s.GoodMembers} | Issues: {s.IssuesToWatch}");
        }

        sb.AppendLine();
        sb.AppendLine($"FANS/CRM ({fans.Count} of {crmData?.Records?.Count ?? 0} total, most recently touched first):");
        foreach (var r in fans)
        {
            sb.AppendLine($"- {r.CreatedAt:yyyy-MM-dd} | User: {r.User} | Site: {r.Site} | Model: {r.Model} | SpendTier: {r.SpendTier}/5 | Habits: {r.Habits} | Triggers: {r.Triggers} | Notes: {r.Notes}");
        }

        return sb.ToString();
    }

    private void ShowInfoDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 190,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
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
}
