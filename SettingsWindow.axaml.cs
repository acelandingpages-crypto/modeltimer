using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Linq;

namespace ModelTimer;

public partial class SettingsWindow : Window
{
    public string SelectedTheme { get; private set; } = "Dark";
    public string ShowHotkey { get; private set; } = "ctrl + s";
    public string PrivateHotkey { get; private set; } = "ctrl + p";
    public string TypingHotkey { get; private set; } = "ctrl + k";
    public bool NotifyOnShiftComplete { get; private set; } = true;
    public bool Warn5Min { get; private set; } = true;
    public bool Warn15Min { get; private set; } = true;
    public string AiProvider { get; private set; } = "None";
    public string AiApiKey { get; private set; } = string.Empty;
    public bool Confirmed { get; private set; }

    private TextBox? _activeHotkeyBox;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();

        ShowHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);
        PrivateHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);
        TypingHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);

        UpdateVersionText.Text = $"Version {AppUpdateService.CurrentVersion}";

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        var settings = SettingsStore.Load();
        if (settings.Theme == "Light")
        {
            RequestedThemeVariant = ThemeVariant.Light;
            UpdateThemeColors(true);
        }
        else
        {
            UpdateThemeColors(false);
        }
    }

    private void UpdateThemeColors(bool isLight)
    {
        var bgMain = isLight ? "#FFF0F0F0" : "#FF1E1E1E";
        var bgSurface = isLight ? "#FFFFFFFF" : "#FF252526";
        var bgToolbar = isLight ? "#FFE0E0E0" : "#FF3E3E42";
        var fgMain = isLight ? "#FF000000" : "#FFFFFFFF";
        var fgMuted = isLight ? "#FF666666" : "#FF888888";

        Background = new SolidColorBrush(Color.Parse(bgMain));
        ContentBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        NotifyBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        ThemeLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ShowHotkeyLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        PrivateHotkeyLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        TypingHotkeyLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ShowHotkeyBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ShowHotkeyBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        PrivateHotkeyBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        PrivateHotkeyBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        TypingHotkeyBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        TypingHotkeyBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ThemeComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ThemeComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnResetDefaults.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnResetDefaults.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ChkNotifyOnShiftComplete.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ChkWarn5Min.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ChkWarn15Min.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        AiSectionLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiProviderLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiApiKeyLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiProviderComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        AiProviderComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiApiKeyBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        AiApiKeyBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ChkAiIncludeSensitiveNotes.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        AiHintText.Foreground = new SolidColorBrush(Color.Parse(fgMuted));
        BackupBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        BackupSectionLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BackupHintText.Foreground = new SolidColorBrush(Color.Parse(fgMuted));
        BtnRestoreShiftBackup.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnRestoreShiftBackup.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnRestoreCrmBackup.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnRestoreCrmBackup.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        UpdateBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        UpdateSectionLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        UpdateVersionText.Foreground = new SolidColorBrush(Color.Parse(fgMuted));
        BtnCheckForUpdates.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnCheckForUpdates.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnCancel.Background = new SolidColorBrush(Color.Parse("#FFFF0000"));
        BtnCancel.Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        BtnSave.Background = new SolidColorBrush(Color.Parse("#FFa6e3a1"));
        BtnSave.Foreground = new SolidColorBrush(Color.Parse("#FF000000"));
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        BtnCancel_Click(sender, e);
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void LoadSettings()
    {
        var settings = SettingsStore.Load();

        SelectedTheme = settings.Theme ?? "Dark";
        ShowHotkey = settings.ShowHotkey ?? "ctrl + s";
        PrivateHotkey = settings.PrivateHotkey ?? "ctrl + p";
        TypingHotkey = settings.TypingHotkey ?? "ctrl + k";
        NotifyOnShiftComplete = settings.NotifyOnShiftComplete;
        Warn5Min = settings.Warn5Min;
        Warn15Min = settings.Warn15Min;
        AiProvider = settings.AiProvider;
        AiApiKey = settings.AiApiKey;

        ThemeComboBox.SelectedItem = SelectedTheme == "Light" ? ThemeComboBox.Items[0] : ThemeComboBox.Items[1];
        ShowHotkeyBox.Text = ShowHotkey;
        PrivateHotkeyBox.Text = PrivateHotkey;
        TypingHotkeyBox.Text = TypingHotkey;
        ChkNotifyOnShiftComplete.IsChecked = NotifyOnShiftComplete;
        ChkWarn5Min.IsChecked = Warn5Min;
        ChkWarn15Min.IsChecked = Warn15Min;
        AiProviderComboBox.SelectedIndex = AiProvider switch
        {
            "Anthropic Claude" => 1,
            "OpenRouter" => 2,
            _ => 0
        };
        AiApiKeyBox.Text = AiApiKey;
        ChkAiIncludeSensitiveNotes.IsChecked = settings.AiIncludeSensitiveNotes;
    }

    private bool SaveSettings()
    {
        var existing = SettingsStore.Load();

        var settings = new AppSettings
        {
            Theme = ThemeComboBox.SelectedItem is ComboBoxItem item ? item.Content?.ToString() : "Dark",
            ShowHotkey = ShowHotkeyBox.Text ?? "ctrl + s",
            PrivateHotkey = PrivateHotkeyBox.Text ?? "ctrl + p",
            TypingHotkey = TypingHotkeyBox.Text ?? "ctrl + k",
            NotifyOnShiftComplete = ChkNotifyOnShiftComplete.IsChecked ?? false,
            Warn5Min = ChkWarn5Min.IsChecked ?? false,
            Warn15Min = ChkWarn15Min.IsChecked ?? false,
            AiProvider = AiProviderComboBox.SelectedItem is ComboBoxItem aiItem ? aiItem.Content?.ToString() ?? "None" : "None",
            AiApiKey = AiApiKeyBox.Text ?? string.Empty,
            AiConsentAcknowledged = existing.AiConsentAcknowledged,
            AiIncludeSensitiveNotes = ChkAiIncludeSensitiveNotes.IsChecked ?? true
        };

        return SettingsStore.Save(settings);
    }

    private void BtnResetDefaults_Click(object sender, RoutedEventArgs e)
    {
        ThemeComboBox.SelectedIndex = 1;
        ShowHotkeyBox.Text = "ctrl + s";
        PrivateHotkeyBox.Text = "ctrl + p";
        TypingHotkeyBox.Text = "ctrl + k";
        ChkNotifyOnShiftComplete.IsChecked = true;
        ChkWarn5Min.IsChecked = true;
        ChkWarn15Min.IsChecked = true;
        AiProviderComboBox.SelectedIndex = 0;
        AiApiKeyBox.Text = string.Empty;
        ChkAiIncludeSensitiveNotes.IsChecked = true;
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (!HotkeysAreUnique())
        {
            AppDialog.ShowInfo(this, "Duplicate Hotkey",
                "Show, Private, and Typing hotkeys must all be different — right now two of them are bound to the same combo, so only one would ever fire. Change one before saving.");
            return;
        }

        if (!SaveSettings())
        {
            AppDialog.ShowInfo(this, "Save Failed", "Couldn't save settings to disk. Please try again.");
            return;
        }

        SelectedTheme = ThemeComboBox.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "Dark" : "Dark";
        ShowHotkey = ShowHotkeyBox.Text ?? "ctrl + s";
        PrivateHotkey = PrivateHotkeyBox.Text ?? "ctrl + p";
        TypingHotkey = TypingHotkeyBox.Text ?? "ctrl + k";
        NotifyOnShiftComplete = ChkNotifyOnShiftComplete.IsChecked ?? false;
        Warn5Min = ChkWarn5Min.IsChecked ?? false;
        Warn15Min = ChkWarn15Min.IsChecked ?? false;
        AiProvider = AiProviderComboBox.SelectedItem is ComboBoxItem aiSaveItem ? aiSaveItem.Content?.ToString() ?? "None" : "None";
        AiApiKey = AiApiKeyBox.Text ?? string.Empty;
        Confirmed = true;
        Close();
    }

    private bool HotkeysAreUnique()
    {
        var combos = new[] { ShowHotkeyBox.Text, PrivateHotkeyBox.Text, TypingHotkeyBox.Text }
            .Select(h => h?.Trim().ToLowerInvariant() ?? string.Empty)
            .Where(h => !string.IsNullOrEmpty(h))
            .ToList();

        return combos.Distinct().Count() == combos.Count;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BtnRestoreShiftBackup_Click(object sender, RoutedEventArgs e)
    {
        await RestoreBackup(
            "Restore Shift Data",
            "This replaces the current shift history with the most recent automatic backup. The current file is itself backed up first, so this can be undone by restoring again.",
            () => ShiftDataStore.GetLatestBackupTime(),
            () => ShiftDataStore.RestoreLatestBackup());
    }

    private async void BtnRestoreCrmBackup_Click(object sender, RoutedEventArgs e)
    {
        await RestoreBackup(
            "Restore Fan Data",
            "This replaces the current fan (CRM) data with the most recent automatic backup. The current file is itself backed up first, so this can be undone by restoring again.",
            () => CrmDataStore.GetLatestBackupTime(),
            () => CrmDataStore.RestoreLatestBackup());
    }

    private async System.Threading.Tasks.Task RestoreBackup(string title, string confirmMessage, Func<DateTime?> getLatestBackupTime, Func<bool> restore)
    {
        var latest = getLatestBackupTime();
        if (latest == null)
        {
            AppDialog.ShowInfo(this, title, "No backup is available yet — one is created automatically the next time this data is saved.");
            return;
        }

        var message = $"{confirmMessage}\n\nMost recent backup: {latest:yyyy-MM-dd HH:mm:ss}.";
        var confirmed = await AppDialog.ShowConfirm(this, title, message, confirmLabel: "Restore");
        if (!confirmed) return;

        if (restore())
        {
            AppDialog.ShowInfo(this, title, "Restored. Any open windows showing this data will refresh automatically.");
        }
        else
        {
            AppDialog.ShowInfo(this, title, "Restore failed — see error_log.txt for details.");
        }
    }

    private async void BtnCheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (!AppUpdateService.IsConfigured)
        {
            AppDialog.ShowInfo(this, "Updates Not Set Up", "This build isn't wired up to an update feed yet.");
            return;
        }

        if (!AppUpdateService.IsInstalledCopy)
        {
            AppDialog.ShowInfo(this, "Not an Installed Copy", "This is a development build, not one installed via the updater - nothing to check.");
            return;
        }

        var originalContent = BtnCheckForUpdates.Content;
        try
        {
            BtnCheckForUpdates.IsEnabled = false;
            BtnCheckForUpdates.Content = "Checking...";

            var update = await AppUpdateService.CheckForUpdateAsync();
            if (update == null)
            {
                AppDialog.ShowInfo(this, "Up to Date", $"You're on the latest version ({AppUpdateService.CurrentVersion}).");
                return;
            }

            var version = update.TargetFullRelease.Version;
            var confirmed = await AppDialog.ShowConfirm(this, "Update Available",
                $"ModelTimer {version} is available (you have {AppUpdateService.CurrentVersion}). Update and restart now?",
                confirmLabel: "Update");
            if (!confirmed) return;

            BtnCheckForUpdates.Content = "Updating...";
            var applied = await AppUpdateService.DownloadAndApplyAsync(update);
            if (!applied)
            {
                AppDialog.ShowInfo(this, "Update Failed", "Couldn't download or apply the update. Try again later, or check error_log.txt.");
            }
        }
        finally
        {
            BtnCheckForUpdates.IsEnabled = true;
            BtnCheckForUpdates.Content = originalContent;
        }
    }

    private void HotkeyBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box)
        {
            _activeHotkeyBox = box;
            box.Text = "Press keys...";
            box.SelectAll();
        }
    }

    private void HotkeyBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (_activeHotkeyBox == null) return;

        var key = e.Key;
        var modifiers = e.KeyModifiers;

        var modifierKeys = new[] { Key.LeftShift, Key.RightShift, Key.System, Key.LeftCtrl, Key.RightCtrl, Key.LWin, Key.RWin, Key.LeftAlt, Key.RightAlt };
        if (modifierKeys.Contains(key))
        {
            e.Handled = true;
            return;
        }

        if (key == Key.A || key == Key.Y || key == Key.Z)
        {
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
            {
                e.Handled = true;
                return;
            }
        }

        string hotkey = "";
        if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control) hotkey += "ctrl + ";
        if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift) hotkey += "shift + ";
        if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt) hotkey += "alt + ";

        hotkey += key.ToString().ToLower();

        _activeHotkeyBox.Text = hotkey;
        e.Handled = true;

        _activeHotkeyBox = null;
    }

    private void HotkeyBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.Text == "Press keys...")
        {
            box.Text = box.Name switch
            {
                "ShowHotkeyBox" => ShowHotkey,
                "PrivateHotkeyBox" => PrivateHotkey,
                "TypingHotkeyBox" => TypingHotkey,
                _ => ""
            };
        }
    }

}
