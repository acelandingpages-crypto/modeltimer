using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ModelTimer;

public partial class ShiftReportWindow : Window
{
    private readonly string _model;
    private readonly string _moderator;
    private readonly TimeSpan _elapsed;
    private bool _submitted;

    public ShiftReportWindow(string model, string moderator, TimeSpan elapsed)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _elapsed = elapsed;

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
        var hasText = !string.IsNullOrWhiteSpace(SummaryTextBox.Text);
        BtnSubmit.IsEnabled = hasText;
        BtnDraftAi.IsVisible = hasText;
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
        var data = ShiftDataStore.Load();
        if (data.Shifts == null || data.Shifts.Count == 0) return false;

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

        return ShiftDataStore.Save(data);
    }

    private void ShowSaveFailedWarning()
    {
        ShowInfoDialog("Save Failed", "Couldn't save the shift report to disk. Please note it down manually, then try Submit again.");
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
