using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.IO;
using System.Text.Json;

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
    public bool Confirmed { get; private set; }

    private string _dataFilePath = string.Empty;
    private TextBox? _activeHotkeyBox;

    public SettingsWindow()
    {
        InitializeComponent();
        _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        LoadSettings();

        ShowHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);
        PrivateHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);
        TypingHotkeyBox.AddHandler(KeyDownEvent, HotkeyBox_KeyDown, RoutingStrategies.Tunnel);

        ApplyTheme();
    }

    private void ApplyTheme()
    {
        try
        {
            var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(settingsPath))
            {
                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null && settings.Theme == "Light")
                {
                    RequestedThemeVariant = ThemeVariant.Light;
                    UpdateThemeColors(true);
                }
                else
                {
                    UpdateThemeColors(false);
                }
            }
            else
            {
                UpdateThemeColors(false);
            }
        }
        catch
        {
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
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);
                if (settings != null)
                {
                    SelectedTheme = settings.Theme ?? "Dark";
                    ShowHotkey = settings.ShowHotkey ?? "ctrl + s";
                    PrivateHotkey = settings.PrivateHotkey ?? "ctrl + p";
                    TypingHotkey = settings.TypingHotkey ?? "ctrl + k";
                    NotifyOnShiftComplete = settings.NotifyOnShiftComplete;
                    Warn5Min = settings.Warn5Min;
                    Warn15Min = settings.Warn15Min;
                }
            }
        }
        catch
        {
        }

        ThemeComboBox.SelectedItem = SelectedTheme == "Light" ? ThemeComboBox.Items[0] : ThemeComboBox.Items[1];
        ShowHotkeyBox.Text = ShowHotkey;
        PrivateHotkeyBox.Text = PrivateHotkey;
        TypingHotkeyBox.Text = TypingHotkey;
        ChkNotifyOnShiftComplete.IsChecked = NotifyOnShiftComplete;
        ChkWarn5Min.IsChecked = Warn5Min;
        ChkWarn15Min.IsChecked = Warn15Min;
    }

    private void SaveSettings()
    {
        try
        {
            var settings = new AppSettings
            {
                Theme = ThemeComboBox.SelectedItem is ComboBoxItem item ? item.Content?.ToString() : "Dark",
                ShowHotkey = ShowHotkeyBox.Text ?? "ctrl + s",
                PrivateHotkey = PrivateHotkeyBox.Text ?? "ctrl + p",
                TypingHotkey = TypingHotkeyBox.Text ?? "ctrl + k",
                NotifyOnShiftComplete = ChkNotifyOnShiftComplete.IsChecked ?? false,
                Warn5Min = ChkWarn5Min.IsChecked ?? false,
                Warn15Min = ChkWarn15Min.IsChecked ?? false
            };

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataFilePath, json);
        }
        catch
        {
        }
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
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        SelectedTheme = ThemeComboBox.SelectedItem is ComboBoxItem item ? item.Content?.ToString() ?? "Dark" : "Dark";
        ShowHotkey = ShowHotkeyBox.Text ?? "ctrl + s";
        PrivateHotkey = PrivateHotkeyBox.Text ?? "ctrl + p";
        TypingHotkey = TypingHotkeyBox.Text ?? "ctrl + k";
        NotifyOnShiftComplete = ChkNotifyOnShiftComplete.IsChecked ?? false;
        Warn5Min = ChkWarn5Min.IsChecked ?? false;
        Warn15Min = ChkWarn15Min.IsChecked ?? false;
        Confirmed = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
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

    private class AppSettings
    {
        public string? Theme { get; set; }
        public string? ShowHotkey { get; set; }
        public string? PrivateHotkey { get; set; }
        public string? TypingHotkey { get; set; }
        public bool NotifyOnShiftComplete { get; set; }
        public bool Warn5Min { get; set; }
        public bool Warn15Min { get; set; }
    }
}
