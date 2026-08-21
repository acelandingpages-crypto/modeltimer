using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ModelTimer;

public partial class HighTrafficWindow : Window
{
    private static readonly string[] SpendTierLabels = { "-", "$", "$$", "$$$", "$$$$", "$$$$$" };

    private List<CrmEntry> _records = new();
    private int _nextId = 1;
    private int _editingId = 0;
    private CrmEntry? _lastDeletedEntry;
    private DispatcherTimer? _undoTimer;

    public HighTrafficWindow()
    {
        InitializeComponent();
        LoadRecords();
        LoadModelsList();
        RefreshTable();
        ApplyTheme();
        SiteFilterComboBox.SelectionChanged += SiteFilterComboBox_SelectionChanged;

        CrmDataStore.Changed += CrmDataStore_Changed;
        Closed += (s, e) => CrmDataStore.Changed -= CrmDataStore_Changed;
    }

    private void CrmDataStore_Changed()
    {
        if (_editingId != 0) return; // don't yank the form out from under an in-progress edit
        LoadRecords();
        RefreshTable();
    }

    private void LoadModelsList()
    {
        var data = ShiftDataStore.Load();
        if (data.Models == null) return;
        foreach (var model in data.Models)
        {
            ModelComboBox.Items.Add(model);
        }
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is Visual source)
        {
            if (IsDescendantOf(SiteFilterComboBox, source)) return;
            if (IsDescendantOf(CloseButton, source)) return;
        }
        BeginMoveDrag(e);
    }

    private bool IsDescendantOf(Visual parent, Visual child)
    {
        Visual? current = child;
        while (current != null)
        {
            if (current == parent) return true;
            current = current.Parent as Visual;
        }
        return false;
    }

    private void SiteFilterComboBox_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshTable();
    }

    private void LoadRecords()
    {
        _records.Clear();
        _nextId = 1;

        var data = CrmDataStore.Load();
        if (data.Records == null) return;
        foreach (var r in data.Records.OrderBy(x => x.Id))
        {
            r.User ??= string.Empty;
            r.Model ??= string.Empty;
            r.Site ??= string.Empty;
            r.Habits ??= string.Empty;
            r.Triggers ??= string.Empty;
            r.Notes ??= string.Empty;
            _records.Add(r);
            if (r.Id >= _nextId) _nextId = r.Id + 1;
        }
    }

    private bool SaveRecords()
    {
        var data = new CrmDataFile { Records = _records };
        return CrmDataStore.Save(data);
    }

    private void RefreshTable()
    {
        var siteFilter = SiteFilterComboBox.SelectedItem as ComboBoxItem;
        var filterText = siteFilter?.Content?.ToString() ?? "All";
        var filtered = _records.Where(r => filterText == "All" || r.Site.Equals(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        CrmGrid.ItemsSource = filtered;
        NoDataText.IsVisible = filtered.Count == 0;
    }

    private void NotesCell_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: CrmEntry item } && !string.IsNullOrEmpty(item.Notes))
        {
            ShowNotesPopup(item.Notes);
        }
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CrmEntry item }) return;

        _editingId = item.Id;
        UserTextBox.Text = item.User;
        ModelComboBox.SelectedItem = item.Model;
        ModelComboBox.Text = item.Model;
        PlatformComboBox.SelectedItem = PlatformComboBox.Items.OfType<ComboBoxItem>().FirstOrDefault(c => c.Content?.ToString() == item.Site);
        HabitsTextBox.Text = item.Habits;
        TriggersTextBox.Text = item.Triggers;
        NotesTextBox.Text = item.Notes;
        SpendTierComboBox.SelectedIndex = Math.Clamp(item.SpendTier, 0, SpendTierComboBox.ItemCount - 1);
    }

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CrmEntry item }) return;

        var confirmed = await AppDialog.ShowConfirm(this, "Delete Fan Record",
            $"Delete the record for \"{item.User}\" on {item.Site}? You can undo this right after.");
        if (!confirmed) return;

        _records.Remove(item);
        SaveRecords();
        RefreshTable();

        UserTextBox.Text = string.Empty;
        ModelComboBox.Text = string.Empty;
        HabitsTextBox.Text = string.Empty;
        TriggersTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        PlatformComboBox.SelectedIndex = 0;
        SpendTierComboBox.SelectedIndex = 0;

        ShowUndoBanner(item, $"Deleted the record for \"{item.User}\" on {item.Site}.");
    }

    private void ShowUndoBanner(CrmEntry removed, string message)
    {
        _lastDeletedEntry = removed;
        UndoBannerText.Text = message;
        UndoBanner.IsVisible = true;

        _undoTimer?.Stop();
        _undoTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _undoTimer.Tick += (s, e) =>
        {
            _undoTimer?.Stop();
            UndoBanner.IsVisible = false;
            _lastDeletedEntry = null;
        };
        _undoTimer.Start();
    }

    private void BtnUndoDelete_Click(object? sender, RoutedEventArgs e)
    {
        _undoTimer?.Stop();
        UndoBanner.IsVisible = false;

        if (_lastDeletedEntry == null) return;

        _records.Add(_lastDeletedEntry);
        if (_lastDeletedEntry.Id >= _nextId) _nextId = _lastDeletedEntry.Id + 1;
        SaveRecords();

        _lastDeletedEntry = null;
        RefreshTable();
    }

    private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
    {
        var user = UserTextBox.Text?.Trim() ?? string.Empty;
        var model = ModelComboBox.Text?.Trim() ?? string.Empty;
        var site = PlatformComboBox.SelectedItem as ComboBoxItem;
        var siteText = site?.Content?.ToString() ?? string.Empty;
        var habits = HabitsTextBox.Text?.Trim() ?? string.Empty;
        var triggers = TriggersTextBox.Text?.Trim() ?? string.Empty;
        var notes = NotesTextBox.Text?.Trim() ?? string.Empty;
        var spendTier = SpendTierComboBox.SelectedIndex;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(siteText))
        {
            AppDialog.ShowInfo(this, "Missing Info", "Username and Site are both required to save a fan record.");
            return;
        }

        var existing = _records.FirstOrDefault(r => r.User.Equals(user, StringComparison.OrdinalIgnoreCase) && r.Site.Equals(siteText, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
            AppDialog.ShowInfo(this, "Already on File", $"\"{user}\" on {siteText} is already in the CRM. Use Edit on that row to update it instead.");
            return;
        }

        _records.Add(new CrmEntry
        {
            Id = _nextId++,
            User = user,
            Model = model,
            Site = siteText,
            Habits = habits,
            Triggers = triggers,
            Notes = notes,
            CreatedAt = DateTime.Now,
            SpendTier = spendTier
        });

        SaveRecords();
        UserTextBox.Text = string.Empty;
        ModelComboBox.Text = string.Empty;
        HabitsTextBox.Text = string.Empty;
        TriggersTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        PlatformComboBox.SelectedIndex = 0;
        SpendTierComboBox.SelectedIndex = 0;
        _editingId = 0;
        RefreshTable();
    }

    private void BtnUpdateProfile_Click(object sender, RoutedEventArgs e)
    {
        var user = UserTextBox.Text?.Trim() ?? string.Empty;
        var model = ModelComboBox.Text?.Trim() ?? string.Empty;
        var site = PlatformComboBox.SelectedItem as ComboBoxItem;
        var siteText = site?.Content?.ToString() ?? string.Empty;
        var habits = HabitsTextBox.Text?.Trim() ?? string.Empty;
        var triggers = TriggersTextBox.Text?.Trim() ?? string.Empty;
        var notes = NotesTextBox.Text?.Trim() ?? string.Empty;
        var spendTier = SpendTierComboBox.SelectedIndex;

        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(siteText))
        {
            AppDialog.ShowInfo(this, "Missing Info", "Username and Site are both required to save a fan record.");
            return;
        }

        var existing = _records.FirstOrDefault(r => r.Id == _editingId);
        if (existing == null)
        {
            AppDialog.ShowInfo(this, "Nothing to Update", "Click Edit on a row first, then Update.");
            return;
        }

        existing.User = user;
        existing.Model = model;
        existing.Site = siteText;
        existing.Habits = habits;
        existing.Triggers = triggers;
        existing.Notes = notes;
        existing.CreatedAt = DateTime.Now;
        existing.SpendTier = spendTier;

        SaveRecords();
        UserTextBox.Text = string.Empty;
        ModelComboBox.Text = string.Empty;
        HabitsTextBox.Text = string.Empty;
        TriggersTextBox.Text = string.Empty;
        NotesTextBox.Text = string.Empty;
        PlatformComboBox.SelectedIndex = 0;
        SpendTierComboBox.SelectedIndex = 0;
        _editingId = 0;
        RefreshTable();
    }

    private async void BtnSuggestTag_Click(object sender, RoutedEventArgs e)
    {
        var user = UserTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(user))
        {
            ShowInfoDialog("Suggest Tier & Check Duplicates", "Type a username first, then try again.");
            return;
        }

        var settings = SettingsStore.Load();
        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Not Set Up", "Add a provider and API key under Settings to enable tier suggestions and duplicate checks.");
            return;
        }

        if (!await AiConsentService.EnsureConsentAsync(this, settings)) return;

        var includeSensitive = settings.AiIncludeSensitiveNotes;
        var habits = includeSensitive ? HabitsTextBox.Text?.Trim() ?? string.Empty : string.Empty;
        var triggers = includeSensitive ? TriggersTextBox.Text?.Trim() ?? string.Empty : string.Empty;
        var notes = includeSensitive ? NotesTextBox.Text?.Trim() ?? string.Empty : string.Empty;
        var existingUsers = _records
            .Where(r => r.Id != _editingId)
            .Select(r => r.User)
            .Where(u => !string.IsNullOrWhiteSpace(u))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var originalContent = BtnSuggestTag.Content;
        try
        {
            BtnSuggestTag.IsEnabled = false;
            BtnSuggestTag.Content = "Checking...";

            var suggestion = await AiSummaryService.SuggestFanTagAsync(settings, user, habits, triggers, notes, existingUsers);

            var appliedTier = false;
            if (suggestion.SuggestedTier is >= 1 and <= 5 && SpendTierComboBox.SelectedIndex == 0)
            {
                SpendTierComboBox.SelectedIndex = suggestion.SuggestedTier;
                appliedTier = true;
            }

            if (!string.IsNullOrWhiteSpace(suggestion.LikelyDuplicateOf))
            {
                ShowInfoDialog("Possible Duplicate", $"This looks similar to an existing record for \"{suggestion.LikelyDuplicateOf}\" — check before saving to avoid a duplicate fan entry.");
            }
            else if (appliedTier)
            {
                ShowInfoDialog("Suggested Tier Applied", $"Set spend tier to {SpendTierLabels[suggestion.SuggestedTier]} based on the habits/triggers/notes text. No likely duplicates found.");
            }
            else
            {
                ShowInfoDialog("No Suggestions", "AI didn't find a confident tier suggestion or a likely duplicate. Nothing changed.");
            }
        }
        catch (Exception ex)
        {
            ShowInfoDialog("AI Unavailable", ex.Message);
        }
        finally
        {
            BtnSuggestTag.IsEnabled = true;
            BtnSuggestTag.Content = originalContent;
        }
    }

    private void ShowInfoDialog(string title, string message) => AppDialog.ShowInfo(this, title, message);

    private void ShowNotesPopup(string notes)
    {
        var popup = new Window
        {
            Title = "Notes",
            Width = 400,
            Height = 300,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };
        var border = new Border
        {
            Padding = new Thickness(20),
            BorderBrush = new SolidColorBrush(Color.Parse("#FFcba6f7")),
            BorderThickness = new Thickness(1)
        };
        
        var textBox = new TextBox
        {
            Text = notes,
            Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            IsReadOnly = true,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0)
        };
        border.Child = textBox;
        popup.Content = border;
        popup.Show(this);
    }

    private void ApplyTheme()
    {
        var settings = SettingsStore.Load();
        var isLight = settings.Theme == "Light";
        if (isLight)
        {
            RequestedThemeVariant = ThemeVariant.Light;
        }
        var bgMain = isLight ? "#FFF0F0F0" : "#FF1E1E1E";
        var bgSurface = isLight ? "#FFFFFFFF" : "#FF252526";
        var bgToolbar = isLight ? "#FFE0E0E0" : "#FF3E3E42";
        var fgMain = isLight ? "#FF000000" : "#FFFFFFFF";

        Background = new SolidColorBrush(Color.Parse(bgMain));
        UserTextBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        UserTextBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        HabitsTextBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        HabitsTextBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        TriggersTextBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        TriggersTextBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        NotesTextBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        NotesTextBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        PlatformComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        PlatformComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        CloseButton.Background = new SolidColorBrush(Color.Parse("#FFFF0000"));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

}
