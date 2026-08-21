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
    public bool Saved { get; private set; }

    public EditShiftReportWindow(string model, string moderator, DateTime timestamp, TimeSpan elapsed,
        string sessionSummary, string goodMembers, string issuesToWatch, int performanceRating)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _timestamp = timestamp;
        _elapsed = elapsed;

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
        var data = ShiftDataStore.Load();
        if (data.Shifts == null) return false;

        var entry = data.Shifts.FirstOrDefault(s =>
            s.Model == _model &&
            s.Moderator == _moderator &&
            s.Timestamp == _timestamp);

        if (entry == null) return false;

        entry.SessionSummary = SummaryTextBox.Text?.Trim() ?? string.Empty;
        entry.GoodMembers = GoodMembersTextBox.Text?.Trim() ?? string.Empty;
        entry.IssuesToWatch = IssuesTextBox.Text?.Trim() ?? string.Empty;
        entry.PerformanceRating = RatingComboBox.SelectedIndex;

        return ShiftDataStore.Save(data);
    }

    private async void BtnDraftAi_Click(object sender, RoutedEventArgs e)
    {
        var notes = SummaryTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(notes))
        {
            ShowInfoDialog("Nothing to Polish", "Write your own notes on how the shift went first — AI polishes what you wrote, it doesn't invent a summary from nothing.");
            return;
        }

        var settings = SettingsStore.Load();

        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Polishing Not Set Up", "Add a provider and API key under Settings to enable AI polishing. Your own notes above will still be saved as-is.");
            return;
        }

        if (!await AiConsentService.EnsureConsentAsync(this, settings)) return;

        var originalContent = BtnDraftAi.Content;
        try
        {
            BtnDraftAi.IsEnabled = false;
            BtnDraftAi.Content = "Polishing...";
            var polished = await AiSummaryService.PolishSummaryAsync(settings, _model, _moderator, _elapsed, notes);
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

    private void ShowInfoDialog(string title, string message) => AppDialog.ShowInfo(this, title, message);
}
