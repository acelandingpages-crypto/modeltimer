using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModelTimer;

public partial class ShiftHistoryWindow : Window
{
    private string _dataFilePath = string.Empty;
    private List<ShiftHistoryItem> _allShifts = new();
    private List<ShiftHistoryItem> _filteredShifts = new();
    private DateTime? _minDate;
    private DateTime? _maxDate;
    private bool _isInitializing;

    public ShiftHistoryWindow()
    {
        InitializeComponent();
        _dataFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        LoadShiftHistory();

        SearchBox.TextChanged += (s, e) => RefreshTable();
        FromDatePicker.SelectedDateChanged += FromDatePicker_SelectedDateChanged;
        ToDatePicker.SelectedDateChanged += ToDatePicker_SelectedDateChanged;
        BtnApply.Click += (s, e) => RefreshTable();

        if (!_allShifts.Any())
        {
            FromDatePicker.IsEnabled = false;
            ToDatePicker.IsEnabled = false;
            NoDataText.IsVisible = true;
        }
        else
        {
            _isInitializing = true;
            if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
            if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
            _isInitializing = false;
        }

        RefreshTable();
        ApplyTheme();
    }

    private void FromDatePicker_SelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (_isInitializing || !FromDatePicker.IsEnabled) return;

        var fromDate = FromDatePicker.SelectedDate?.DateTime;
        if (fromDate.HasValue && _minDate.HasValue && fromDate.Value.Date < _minDate.Value.Date)
        {
            _isInitializing = true;
            FromDatePicker.SelectedDate = _minDate.Value;
            _isInitializing = false;
        }

        if (ToDatePicker.SelectedDate is DateTimeOffset to && fromDate.HasValue && fromDate.Value.Date > to.DateTime.Date)
        {
            _isInitializing = true;
            ToDatePicker.SelectedDate = fromDate.Value;
            _isInitializing = false;
        }

        RefreshTable();
    }

    private void ToDatePicker_SelectedDateChanged(object? sender, DatePickerSelectedValueChangedEventArgs e)
    {
        if (_isInitializing || !ToDatePicker.IsEnabled) return;

        var toDate = ToDatePicker.SelectedDate?.DateTime;
        if (toDate.HasValue && _maxDate.HasValue && toDate.Value.Date > _maxDate.Value.Date)
        {
            _isInitializing = true;
            ToDatePicker.SelectedDate = _maxDate.Value;
            _isInitializing = false;
            return;
        }

        if (FromDatePicker.SelectedDate is DateTimeOffset from && toDate.HasValue && toDate.Value.Date < from.DateTime.Date)
        {
            _isInitializing = true;
            FromDatePicker.SelectedDate = toDate.Value;
            _isInitializing = false;
        }

        RefreshTable();
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        RefreshTable();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        SearchBox.Text = string.Empty;
        if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
        if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
        _isInitializing = false;
        RefreshTable();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadShiftHistory();
        _isInitializing = true;
        if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
        if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
        _isInitializing = false;
        RefreshTable();
    }

    private void LoadShiftHistory()
    {
        _allShifts.Clear();
        _filteredShifts.Clear();
        _minDate = null;
        _maxDate = null;

        var data = JsonStore.Load<ShiftDataFile>(_dataFilePath);
        if (data?.Shifts == null) return;

        int no = 1;
        foreach (var entry in data.Shifts.OrderBy(s => s.Timestamp))
        {
            if (!_minDate.HasValue || entry.Timestamp.Date < _minDate.Value.Date) _minDate = entry.Timestamp.Date;
            if (!_maxDate.HasValue || entry.Timestamp.Date > _maxDate.Value.Date) _maxDate = entry.Timestamp.Date;

            var item = new ShiftHistoryItem
            {
                Id = no++,
                Model = entry.Model ?? string.Empty,
                Moderator = entry.Moderator ?? string.Empty,
                Notes = entry.Notes ?? string.Empty,
                StartTime = entry.StartTime,
                StopTime = entry.StopTime,
                Timestamp = entry.Timestamp,
                DurationHours = entry.DurationHours,
                DurationMinutes = entry.DurationMinutes,
                ElapsedHours = entry.ElapsedHours,
                ElapsedMinutes = entry.ElapsedMinutes,
                ElapsedSeconds = entry.ElapsedSeconds
            };

            _allShifts.Add(item);
        }
    }

    private void RefreshTable()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();

        if (!_allShifts.Any())
        {
            NoDataText.IsVisible = true;
            return;
        }

        NoDataText.IsVisible = false;

        var searchText = SearchBox.Text?.Trim().ToLower() ?? string.Empty;
        DateTime? fromDate = FromDatePicker.SelectedDate?.DateTime;
        DateTime? toDate = ToDatePicker.SelectedDate?.DateTime;

        _filteredShifts = _allShifts.Where(item =>
        {
            bool matchesSearch = string.IsNullOrEmpty(searchText) ||
                (item.Model.ToLower().Contains(searchText) || item.Moderator.ToLower().Contains(searchText));

            bool matchesFrom = !fromDate.HasValue || item.Timestamp.Date >= fromDate.Value.Date;
            bool matchesTo = !toDate.HasValue || item.Timestamp.Date <= toDate.Value.Date;

            return matchesSearch && matchesFrom && matchesTo;
        }).OrderByDescending(s => s.Timestamp).ToList();

        AddHeaderRow();

        int rowIndex = 1;
        foreach (var item in _filteredShifts)
        {
            AddDataRow(item, rowIndex++);
        }

        if (rowIndex == 1)
        {
            NoDataText.IsVisible = true;
        }
    }

    private void AddHeaderRow()
    {
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var headers = new[] { "No", "Model", "Moderator", "Date", "Start Time", "Stop Time", "Duration", "Notes", "Delete" };
        for (int col = 0; col < headers.Length; col++)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FF3E3E42")),
                BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            var text = new TextBlock
            {
                Text = headers[col],
                Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                FontWeight = FontWeight.Bold,
                FontSize = 12
            };
            border.Child = text;
            Grid.SetRow(border, 0);
            Grid.SetColumn(border, col);
            TableGrid.Children.Add(border);
        }
    }

    private void AddDataRow(ShiftHistoryItem item, int rowIndex)
    {
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var bgBrush = rowIndex % 2 == 0
            ? new SolidColorBrush(Color.Parse("#FF2D2D30"))
            : new SolidColorBrush(Color.Parse("#FF252526"));

        var cells = new[]
        {
            item.No.ToString(),
            item.Model,
            item.Moderator,
            item.Date,
            item.StartTimeDisplay,
            item.StopTimeDisplay,
            item.DurationDisplay,
            item.Notes,
            "Delete"
        };

        for (int col = 0; col < cells.Length; col++)
        {
            var border = new Border
            {
                Background = bgBrush,
                BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };

            if (col == 8)
            {
                var btn = new Button
                {
                    Content = "Delete",
                    Tag = item.Id,
                    Background = new SolidColorBrush(Color.Parse("#FFcba6f7")),
                    Foreground = new SolidColorBrush(Color.Parse("#FF000000")),
                    Padding = new Thickness(5, 2),
                    Height = 25
                };
                btn.Click += DeleteButton_Click;
                border.Child = btn;
            }
            else if (col == 7)
            {
                var notesBg = rowIndex % 2 == 0
                    ? new SolidColorBrush(Color.Parse("#FF1a2e1a"))
                    : new SolidColorBrush(Color.Parse("#FF152815"));
                
                border.Background = notesBg;
                
                var text = new TextBlock
                {
                    Text = cells[col] ?? string.Empty,
                    Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                
                border.PointerEntered += (s, e) => border.Cursor = new Cursor(StandardCursorType.Hand);
                border.PointerExited += (s, e) => border.Cursor = Cursor.Default;
                border.PointerPressed += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(item.Notes))
                    {
                        ShowNotesPopup(item.Notes);
                    }
                };
                
                border.Child = text;
            }
            else
            {
                var text = new TextBlock
                {
                    Text = cells[col] ?? string.Empty,
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                border.Child = text;
            }

            Grid.SetRow(border, rowIndex);
            Grid.SetColumn(border, col);
            TableGrid.Children.Add(border);
        }
    }
    
    private void ShowNotesPopup(string notes)
    {
        var popup = new Window
        {
            Title = "Shift Notes",
            Width = 400,
            Height = 300,
            MinWidth = 300,
            MinHeight = 200,
            Background = new SolidColorBrush(Color.Parse("#FF252526")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner
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
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
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

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not int id) return;

        var item = _filteredShifts.FirstOrDefault(s => s.Id == id);
        if (item == null) return;

        var data = JsonStore.Load<ShiftDataFile>(_dataFilePath);
        if (data?.Shifts == null) return;

        var toRemove = data.Shifts.FirstOrDefault(s =>
            s.Model == item.Model &&
            s.Moderator == item.Moderator &&
            s.Timestamp == item.Timestamp);

        if (toRemove != null)
        {
            data.Shifts.Remove(toRemove);
            if (!JsonStore.Save(_dataFilePath, data)) return;
            LoadShiftHistory();

            if (!_allShifts.Any())
            {
                FromDatePicker.IsEnabled = false;
                ToDatePicker.IsEnabled = false;
                FromDatePicker.SelectedDate = null;
                ToDatePicker.SelectedDate = null;
            }
            else
            {
                if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
                if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
            }

            RefreshTable();
        }
    }

    private void ApplyTheme()
    {
        var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        var settings = JsonStore.Load<AppSettings>(settingsPath);
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

    private void UpdateThemeColors(bool isLight)
    {
        var bgMain = isLight ? "#FFF0F0F0" : "#FF1E1E1E";
        var bgSurface = isLight ? "#FFFFFFFF" : "#FF252526";
        var bgToolbar = isLight ? "#FFE0E0E0" : "#FF3E3E42";
        var fgMain = isLight ? "#FF000000" : "#FFFFFFFF";

        Background = new SolidColorBrush(Color.Parse(bgMain));
        FilterBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        DataBorder.Background = new SolidColorBrush(Color.Parse(bgMain));
        SearchBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        SearchBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        FromDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        FromDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ToDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ToDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        CloseButton.Background = new SolidColorBrush(Color.Parse("#FFFF0000"));
        CloseButton.Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF"));
        BtnApply.Background = new SolidColorBrush(Color.Parse(isLight ? "#FF107C10" : "#FFa6e3a1"));
        BtnApply.Foreground = new SolidColorBrush(Color.Parse(isLight ? "#FFFFFFFF" : "#FF000000"));
        NoDataText.Foreground = new SolidColorBrush(Color.Parse(isLight ? "#FF666666" : "#FF888888"));
    }

    private class AppSettings
    {
        public string? Theme { get; set; }
    }

    private class ShiftDataFile
    {
        public List<string>? Models { get; set; }
        public List<string>? Moderators { get; set; }
        public List<ShiftEntry>? Shifts { get; set; }
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