using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using System;
using System.IO;
using System.Text.Json;

namespace ModelTimer;

public partial class NewShiftWindow : Window
{
    public string SelectedModel { get; private set; } = string.Empty;
    public string SelectedModerator { get; private set; } = string.Empty;
    public string ShiftNotes { get; private set; } = string.Empty;
    public int DurationHours { get; private set; }
    public int DurationMinutes { get; private set; }
    public bool Confirmed { get; private set; }

    private ComboBox? _activeComboBox;
    private string _lastSelectedModel = string.Empty;
    private string _lastSelectedModerator = string.Empty;
    private string _dataFilePath = string.Empty;

    public NewShiftWindow()
    {
        InitializeComponent();
        _dataFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        PopulateDurationDropdowns();
        LoadSavedData();
        UpdateStartButtonState();

        ModelComboBox.SelectionChanged += (s, e) =>
        {
            if (ModelComboBox.SelectedItem is string selected)
            {
                _lastSelectedModel = selected;
            }
            UpdateStartButtonState();
        };
        ModeratorComboBox.SelectionChanged += (s, e) =>
        {
            if (ModeratorComboBox.SelectedItem is string selected)
            {
                _lastSelectedModerator = selected;
            }
            UpdateStartButtonState();
        };
        HoursComboBox.SelectionChanged += (s, e) => UpdateStartButtonState();
        MinutesComboBox.SelectionChanged += (s, e) => UpdateStartButtonState();

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

    private void PopulateDurationDropdowns()
    {
        for (int i = 0; i <= 12; i++)
        {
            HoursComboBox.Items.Add(i);
        }
        HoursComboBox.SelectedItem = 0;

        for (int i = 0; i <= 55; i += 5)
        {
            MinutesComboBox.Items.Add(i);
        }
        MinutesComboBox.SelectedItem = 0;
    }

    private void LoadSavedData()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                var data = JsonSerializer.Deserialize<ShiftDataFile>(json);
                if (data != null)
                {
                    if (data.Models != null)
                    {
                        foreach (var model in data.Models)
                        {
                            ModelComboBox.Items.Add(model);
                        }
                    }
                    if (data.Moderators != null)
                    {
                        foreach (var moderator in data.Moderators)
                        {
                            ModeratorComboBox.Items.Add(moderator);
                        }
                    }
                }
            }
        }
        catch
        {
        }
    }

    private void SaveData()
    {
        try
        {
            var models = new System.Collections.Generic.List<string>();
            foreach (var item in ModelComboBox.Items)
            {
                if (item is string s) models.Add(s);
            }

            var moderators = new System.Collections.Generic.List<string>();
            foreach (var item in ModeratorComboBox.Items)
            {
                if (item is string s) moderators.Add(s);
            }

            ShiftDataFile? existing = null;
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                existing = JsonSerializer.Deserialize<ShiftDataFile>(json);
            }

            var data = existing ?? new ShiftDataFile();
            data.Models = models;
            data.Moderators = moderators;

            var jsonOut = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataFilePath, jsonOut);
        }
        catch
        {
        }
    }

    private void SaveShiftHistory()
    {
        try
        {
            ShiftDataFile? data = null;
            if (File.Exists(_dataFilePath))
            {
                var json = File.ReadAllText(_dataFilePath);
                data = JsonSerializer.Deserialize<ShiftDataFile>(json);
            }

            data ??= new ShiftDataFile();

            if (data.Models == null) data.Models = new System.Collections.Generic.List<string>();
            if (data.Moderators == null) data.Moderators = new System.Collections.Generic.List<string>();

            foreach (var item in ModelComboBox.Items)
            {
                if (item is string s && !data.Models.Contains(s)) data.Models.Add(s);
            }
            foreach (var item in ModeratorComboBox.Items)
            {
                if (item is string s && !data.Moderators.Contains(s)) data.Moderators.Add(s);
            }

            var entry = new ShiftEntry
            {
                Model = ModelComboBox.Text?.Trim() ?? string.Empty,
                Moderator = ModeratorComboBox.Text?.Trim() ?? string.Empty,
                Notes = ShiftNotesTextBox.Text ?? string.Empty,
                DurationHours = HoursComboBox.SelectedItem is int h ? h : 0,
                DurationMinutes = MinutesComboBox.SelectedItem is int m ? m : 0,
                StartTime = DateTime.Now,
                StopTime = null,
                Timestamp = DateTime.Now
            };

            data.Shifts ??= new System.Collections.Generic.List<ShiftEntry>();
            data.Shifts.Add(entry);

            var jsonOut = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_dataFilePath, jsonOut);
        }
        catch
        {
        }
    }

    private void UpdateStartButtonState()
    {
        var model = ModelComboBox.Text?.Trim() ?? string.Empty;
        var moderator = ModeratorComboBox.Text?.Trim() ?? string.Empty;
        var hours = HoursComboBox.SelectedItem is int h ? h : 0;
        var minutes = MinutesComboBox.SelectedItem is int m ? m : 0;

        BtnConfirm.IsEnabled = !string.IsNullOrEmpty(model) &&
                                !string.IsNullOrEmpty(moderator) &&
                                (hours > 0 || minutes > 0);
    }

    private void SetActiveComboBox(object sender, RoutedEventArgs e)
    {
        if (sender is ComboBox comboBox)
        {
            _activeComboBox = comboBox;
        }
    }

    private ComboBox? GetTargetComboBox(Button button)
    {
        return button.Name switch
        {
            "BtnModelAdd" or "BtnModelEdit" or "BtnModelDelete" => ModelComboBox,
            "BtnModeratorAdd" or "BtnModeratorEdit" or "BtnModeratorDelete" => ModeratorComboBox,
            _ => _activeComboBox
        };
    }

    private void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var comboBox = GetTargetComboBox(button);
        if (comboBox == null) return;

        var text = comboBox.Text?.Trim();
        if (!string.IsNullOrEmpty(text))
        {
            if (!comboBox.Items.Contains(text))
            {
                comboBox.Items.Add(text);
                comboBox.SelectedItem = text;
                _activeComboBox = comboBox;
                SaveData();
                UpdateStartButtonState();
            }
        }
    }

    private void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var comboBox = GetTargetComboBox(button);
        if (comboBox == null) return;

        var newText = comboBox.Text?.Trim();
        if (string.IsNullOrEmpty(newText)) return;

        var oldText = comboBox == ModelComboBox ? _lastSelectedModel : _lastSelectedModerator;

        if (string.IsNullOrEmpty(oldText)) return;

        if (newText == oldText) return;

        if (comboBox.Items.Contains(newText))
        {
            comboBox.BorderBrush = new SolidColorBrush(Color.Parse("#FFFF0000"));
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
            timer.Tick += (s, args) =>
            {
                comboBox.BorderBrush = new SolidColorBrush(Color.Parse("#FF555555"));
                timer.Stop();
            };
            timer.Start();
            return;
        }

        var index = comboBox.Items.IndexOf(oldText);
        if (index >= 0)
        {
            comboBox.Items[index] = newText;
            comboBox.SelectedItem = newText;
            if (comboBox == ModelComboBox)
            {
                _lastSelectedModel = newText;
            }
            else if (comboBox == ModeratorComboBox)
            {
                _lastSelectedModerator = newText;
            }
            _activeComboBox = comboBox;
            SaveData();
            UpdateStartButtonState();
        }
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button) return;
        var comboBox = GetTargetComboBox(button);
        if (comboBox == null) return;

        if (comboBox.SelectedItem is string selected)
        {
            comboBox.Items.Remove(selected);
            comboBox.SelectedItem = null;
            _activeComboBox = comboBox;
            SaveData();
            UpdateStartButtonState();
        }
    }

    private void Duration_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateStartButtonState();
    }

    private void BtnConfirm_Click(object sender, RoutedEventArgs e)
    {
        SelectedModel = ModelComboBox.Text?.Trim() ?? string.Empty;
        SelectedModerator = ModeratorComboBox.Text?.Trim() ?? string.Empty;
        ShiftNotes = ShiftNotesTextBox.Text ?? string.Empty;
        DurationHours = HoursComboBox.SelectedItem is int h ? h : 0;
        DurationMinutes = MinutesComboBox.SelectedItem is int m ? m : 0;
        Confirmed = true;
        SaveData();
        SaveShiftHistory();
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private class ShiftDataFile
    {
        public System.Collections.Generic.List<string>? Models { get; set; }
        public System.Collections.Generic.List<string>? Moderators { get; set; }
        public System.Collections.Generic.List<ShiftEntry>? Shifts { get; set; }
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
}
