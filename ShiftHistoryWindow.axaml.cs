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
    private List<ShiftHistoryItem> _allShifts = new();
    private List<ShiftHistoryItem> _filteredShifts = new();
    private DateTime? _minDate;
    private DateTime? _maxDate;
    private bool _isInitializing;
    private ShiftEntry? _lastDeletedEntry;
    private DispatcherTimer? _undoTimer;

    public ShiftHistoryWindow()
    {
        InitializeComponent();
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

        ShiftDataStore.Changed += ShiftDataStore_Changed;
        Closed += (s, e) => ShiftDataStore.Changed -= ShiftDataStore_Changed;
    }

    private void ShiftDataStore_Changed()
    {
        LoadShiftHistory();
        RefreshTable();
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

        var data = ShiftDataStore.Load();
        if (data.Shifts == null) return;

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
                ElapsedSeconds = entry.ElapsedSeconds,
                LostTimeSeconds = entry.LostTimeSeconds,
                SessionSummary = entry.SessionSummary ?? string.Empty,
                GoodMembers = entry.GoodMembers ?? string.Empty,
                IssuesToWatch = entry.IssuesToWatch ?? string.Empty,
                PerformanceRating = entry.PerformanceRating
            };

            _allShifts.Add(item);
        }
    }

    private void RefreshTable()
    {
        if (!_allShifts.Any())
        {
            ShiftGrid.ItemsSource = null;
            NoDataText.IsVisible = true;
            return;
        }

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

        ShiftGrid.ItemsSource = _filteredShifts;
        NoDataText.IsVisible = _filteredShifts.Count == 0;
    }

    private void NotesCell_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: ShiftHistoryItem item } && !string.IsNullOrEmpty(item.Notes))
        {
            ShowNotesPopup(item.Notes);
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

    private async void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShiftHistoryItem item }) return;

        var confirmed = await AppDialog.ShowConfirm(this, "Delete Shift Record",
            $"Delete the {item.Date} shift for {item.Model} / {item.Moderator}? You can undo this right after.");
        if (!confirmed) return;

        var data = ShiftDataStore.Load();
        if (data.Shifts == null) return;

        var toRemove = data.Shifts.FirstOrDefault(s =>
            s.Model == item.Model &&
            s.Moderator == item.Moderator &&
            s.Timestamp == item.Timestamp);

        if (toRemove != null)
        {
            data.Shifts.Remove(toRemove);
            if (!ShiftDataStore.Save(data)) return;
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
            ShowUndoBanner(toRemove, $"Deleted the {item.Date} shift for {item.Model} / {item.Moderator}.");
        }
    }

    private void ShowUndoBanner(ShiftEntry removed, string message)
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

        var data = ShiftDataStore.Load();
        data.Shifts ??= new List<ShiftEntry>();
        data.Shifts.Add(_lastDeletedEntry);
        ShiftDataStore.Save(data);

        _lastDeletedEntry = null;
        LoadShiftHistory();
        RefreshTable();
    }

    private async void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ShiftHistoryItem item }) return;

        var elapsed = new TimeSpan(0, item.ElapsedHours, item.ElapsedMinutes, item.ElapsedSeconds);
        var editWindow = new EditShiftReportWindow(
            item.Model,
            item.Moderator,
            item.Timestamp,
            elapsed,
            item.SessionSummary,
            item.GoodMembers,
            item.IssuesToWatch,
            item.PerformanceRating);

        await editWindow.ShowDialog(this);

        if (editWindow.Saved)
        {
            LoadShiftHistory();
            RefreshTable();
        }
    }

    private void BtnExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        if (!_filteredShifts.Any()) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("No,Model,Moderator,Date,Start Time,Stop Time,Duration,Rating,Notes");
        int no = 1;
        foreach (var item in _filteredShifts.OrderByDescending(s => s.Timestamp))
        {
            sb.AppendLine($"{no++},{item.Model},{item.Moderator},{item.Date},{item.StartTimeDisplay},{item.StopTimeDisplay},{item.DurationDisplay},{item.PerformanceRating},{item.Notes}");
        }

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_history_export.csv");
        try
        {
            File.WriteAllText(path, sb.ToString());
            AppDialog.ShowInfo(this, "Exported", $"Saved to {path}");
        }
        catch (Exception ex)
        {
            JsonStore.LogError($"Failed to write {path}", ex);
            AppDialog.ShowInfo(this, "Export Failed", "Couldn't write the export file — see error_log.txt for details.");
        }
    }

    private void BtnExportExcel_Click(object? sender, RoutedEventArgs e)
    {
        if (!_filteredShifts.Any()) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("No\tModel\tModerator\tDate\tStart Time\tStop Time\tDuration\tRating\tNotes");
        int no = 1;
        foreach (var item in _filteredShifts.OrderByDescending(s => s.Timestamp))
        {
            sb.AppendLine($"{no++}\t{item.Model}\t{item.Moderator}\t{item.Date}\t{item.StartTimeDisplay}\t{item.StopTimeDisplay}\t{item.DurationDisplay}\t{item.PerformanceRating}\t{item.Notes}");
        }

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_history_export.tsv");
        try
        {
            File.WriteAllText(path, sb.ToString());
            AppDialog.ShowInfo(this, "Exported", $"Saved to {path}");
        }
        catch (Exception ex)
        {
            JsonStore.LogError($"Failed to write {path}", ex);
            AppDialog.ShowInfo(this, "Export Failed", "Couldn't write the export file — see error_log.txt for details.");
        }
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

}