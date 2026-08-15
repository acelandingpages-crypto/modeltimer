using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Linq;

namespace ModelTimer;

public partial class EditShiftReportWindow : Window
{
    private readonly string _model;
    private readonly string _moderator;
    private readonly DateTime _timestamp;
    private readonly TimeSpan _elapsed;
    private readonly string _dataFilePath;
    public bool Saved { get; private set; }

    public EditShiftReportWindow(string model, string moderator, DateTime timestamp, TimeSpan elapsed,
        string sessionSummary, string goodMembers, string issuesToWatch, int performanceRating)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _timestamp = timestamp;
        _elapsed = elapsed;
        _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");

        RecapText.Text = $"Model: {model}  |  Moderator: {moderator}  |  Worked: {elapsed:hh\\:mm\\:ss}";

        SummaryTextBox.Text = sessionSummary;
        GoodMembersTextBox.Text = goodMembers;
        IssuesTextBox.Text = issuesToWatch;
        RatingComboBox.SelectedIndex = performanceRating >= 0 && performanceRating < RatingComboBox.ItemCount
            ? performanceRating
            : 0;

        BtnDraftAi.IsVisible = !string.IsNullOrWhiteSpace(sessionSummary);

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
        BtnDraftAi.IsVisible = !string.IsNullOrWhiteSpace(SummaryTextBox.Text);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (SaveReport())
        {
            Saved = true;
            Close();
        }
        else
        {
            ShowInfoDialog("Save Failed", "Couldn't find the original shift record to update, or the save to disk failed. No changes were made.");
        }
    }

    private bool SaveReport()
    {
        var data = JsonStore.Load<ShiftDataFile>(_dataFilePath);
        if (data?.Shifts == null) return false;

        var entry = data.Shifts.FirstOrDefault(s =>
            s.Model == _model &&
            s.Moderator == _moderator &&
            s.Timestamp == _timestamp);

        if (entry == null) return false;

        entry.SessionSummary = SummaryTextBox.Text?.Trim() ?? string.Empty;
        entry.GoodMembers = GoodMembersTextBox.Text?.Trim() ?? string.Empty;
        entry.IssuesToWatch = IssuesTextBox.Text?.Trim() ?? string.Empty;
        entry.PerformanceRating = RatingComboBox.SelectedIndex;

        return JsonStore.Save(_dataFilePath, data);
    }

    private async void BtnDraftAi_Click(object sender, RoutedEventArgs e)
    {
        var notes = SummaryTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notes))
        {
            ShowInfoDialog("Nothing to Polish", "Write your own notes on how the shift went first — AI polishes what you wrote, it doesn't invent a summary from nothing.");
            return;
        }

        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        var settings = JsonStore.Load<AppSettings>(settingsPath);

        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Polishing Not Set Up", "Add a provider and API key under Settings to enable AI polishing. Your own notes above will still be saved as-is.");
            return;
        }

        var originalContent = BtnDraftAi.Content;
        try
        {
            BtnDraftAi.IsEnabled = false;
            BtnDraftAi.Content = "Polishing...";
            var polished = await AiSummaryService.PolishSummaryAsync(settings!, _model, _moderator, _elapsed, notes);
            SummaryTextBox.Text = polished;
        }
        catch (Exception ex)
        {
            ShowInfoDialog("AI Polishing Unavailable", ex.Message);
        }
        finally
        {
            BtnDraftAi.IsEnabled = true;
            BtnDraftAi.Content = originalContent;
        }
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
