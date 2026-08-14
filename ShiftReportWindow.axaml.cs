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
    private readonly string _dataFilePath;
    private bool _submitted;

    public ShiftReportWindow(string model, string moderator, TimeSpan elapsed)
    {
        InitializeComponent();

        _model = model;
        _moderator = moderator;
        _elapsed = elapsed;
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
        ShowInfoDialog("Save Failed", "Couldn't save the shift report to disk. Please note it down manually, then try Submit again.");
    }

    private async void BtnDraftAi_Click(object sender, RoutedEventArgs e)
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        var settings = JsonStore.Load<AppSettings>(settingsPath);

        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Drafting Not Set Up", "Add a provider and API key under Settings to enable AI-drafted summaries. For now, write this one yourself.");
            return;
        }

        try
        {
            BtnDraftAi.IsEnabled = false;
            var draft = await AiSummaryService.DraftSummaryAsync(settings!, _model, _moderator, _elapsed);
            SummaryTextBox.Text = draft;
        }
        catch (Exception ex)
        {
            ShowInfoDialog("AI Drafting Unavailable", ex.Message);
        }
        finally
        {
            BtnDraftAi.IsEnabled = true;
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
