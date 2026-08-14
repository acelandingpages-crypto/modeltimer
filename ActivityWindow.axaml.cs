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
using System.Text;
using System.Text.Json;

namespace ModelTimer;

public partial class ActivityWindow : Window
{
    private string _dataFilePath = string.Empty;
    private List<ShiftHistoryItem> _allShifts = new();
    private List<ShiftHistoryItem> _filteredShifts = new();
    private DateTime? _minDate;
    private DateTime? _maxDate;
    private bool _isInitializing;
    private double _chartOffsetX = 0.0;
    private double _chartOffsetY = 0.0;
    private double _yAxisMax = 1.0;
    private bool _isPanning = false;
    private Point _panStart;
    private double _panOffsetStartX;
    private List<DateTime> _lastChartDates = new();
    private List<double> _lastChartValues = new();

    public ActivityWindow()
    {
        InitializeComponent();
        _dataFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        LoadShiftHistory();

        FromDatePicker.SelectedDateChanged += FromDatePicker_SelectedDateChanged;
        ToDatePicker.SelectedDateChanged += ToDatePicker_SelectedDateChanged;
        ModeratorComboBox.SelectionChanged += (s, e) => RefreshDashboard();
        BtnApply.Click += (s, e) => RefreshDashboard();
        BtnRefresh.Click += (s, e) => { LoadShiftHistory(); RefreshDashboard(); };
        BtnClear.Click += (s, e) =>
        {
            _isInitializing = true;
            if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
            if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
            ModeratorComboBox.SelectedIndex = 0;
            _isInitializing = false;
            RefreshDashboard();
        };

        ChartCanvas.AddHandler(InputElement.PointerWheelChangedEvent, ChartCanvas_PointerWheelChanged, RoutingStrategies.Tunnel);
        ChartCanvas.PointerPressed += ChartCanvas_PointerPressed;
        ChartCanvas.PointerReleased += ChartCanvas_PointerReleased;
        ChartCanvas.PointerMoved += ChartCanvas_PointerMoved;

        BtnExportCsv.Click += BtnExportCsv_Click;
        BtnExportExcel.Click += BtnExportExcel_Click;

        if (!_allShifts.Any())
        {
            FromDatePicker.IsEnabled = false;
            ToDatePicker.IsEnabled = false;
            ModeratorComboBox.IsEnabled = false;
            BtnApply.IsEnabled = false;
            BtnRefresh.IsEnabled = false;
            BtnClear.IsEnabled = false;
            BtnExportCsv.IsEnabled = false;
            BtnExportExcel.IsEnabled = false;
        }
        else
        {
            _isInitializing = true;
            if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
            if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
            _isInitializing = false;

            var moderators = _allShifts.Select(s => s.Moderator).Distinct().OrderBy(m => m).ToList();
            ModeratorComboBox.ItemsSource = new[] { "All" }.Concat(moderators);
            ModeratorComboBox.SelectedIndex = 0;
        }

        RefreshDashboard();
        ApplyTheme();

        Dispatcher.UIThread.Post(() => DrawChart(), DispatcherPriority.Loaded);
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
            return;
        }

        if (ToDatePicker.SelectedDate is DateTimeOffset to && fromDate.HasValue && fromDate.Value.Date > to.DateTime.Date)
        {
            _isInitializing = true;
            ToDatePicker.SelectedDate = fromDate.Value;
            _isInitializing = false;
        }

        RefreshDashboard();
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

        RefreshDashboard();
    }

    private double GetDurationHours(ShiftHistoryItem item)
    {
        return item.ElapsedHours + item.ElapsedMinutes / 60.0 + item.ElapsedSeconds / 3600.0;
    }

    private void RefreshDashboard()
    {
        if (!_allShifts.Any())
        {
            TotalText.Text = "Total: 0.00 hrs";
            DailyAvgText.Text = "0.00 hrs";
            TopPerformerText.Text = "-";
            DaysTrackedText.Text = "0";
            NoChartText.IsVisible = true;
            ChartCanvas.Children.Clear();
            return;
        }

        var fromDate = FromDatePicker.SelectedDate?.DateTime;
        var toDate = ToDatePicker.SelectedDate?.DateTime;
        var moderator = ModeratorComboBox.SelectedItem as string;

        _filteredShifts = _allShifts.Where(item =>
        {
            bool matchesFrom = !fromDate.HasValue || item.Timestamp.Date >= fromDate.Value.Date;
            bool matchesTo = !toDate.HasValue || item.Timestamp.Date <= toDate.Value.Date;
            bool matchesMod = string.IsNullOrEmpty(moderator) || moderator == "All" || item.Moderator == moderator;
            return matchesFrom && matchesTo && matchesMod;
        }).Where(item => item.ElapsedHours > 0 || item.ElapsedMinutes > 0 || item.ElapsedSeconds > 0)
          .OrderByDescending(s => s.Timestamp)
          .ToList();

        if (!_filteredShifts.Any())
        {
            TotalText.Text = "Total: 0.00 hrs";
            DailyAvgText.Text = "0.00 hrs";
            TopPerformerText.Text = "-";
            DaysTrackedText.Text = "0";
            NoChartText.IsVisible = true;
            ChartCanvas.Children.Clear();
            return;
        }

        var totalHours = _filteredShifts.Sum(GetDurationHours);
        var distinctDays = _filteredShifts.Select(s => s.Timestamp.Date).Distinct().Count();
        var dailyAvg = distinctDays > 0 ? totalHours / distinctDays : 0;
        var topPerformer = _filteredShifts.GroupBy(s => s.Moderator).OrderByDescending(g => g.Sum(s => GetDurationHours(s))).FirstOrDefault();
        var topPerformerName = topPerformer?.Key ?? "-";

        TotalText.Text = $"Total: {FormatDuration(totalHours)}";
        DailyAvgText.Text = $"Daily avg: {FormatDuration(dailyAvg)}";
        TopPerformerText.Text = topPerformerName;
        DaysTrackedText.Text = distinctDays.ToString();

        if (!_filteredShifts.Any())
        {
            NoChartText.IsVisible = true;
            ChartCanvas.Children.Clear();
            return;
        }

        NoChartText.IsVisible = false;

        _chartOffsetX = 0.0;
        _chartOffsetY = 0.0;
        
        var dailyTotals = _filteredShifts
            .GroupBy(s => s.Timestamp.Date)
            .Select(g => g.Sum(s => GetDurationHours(s)))
            .ToList();
        
        var dataMax = dailyTotals.DefaultIfEmpty(0).Max();
        if (dataMax <= 0) dataMax = 0.1;
        _yAxisMax = dataMax;
        
        DrawChart();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        RefreshDashboard();
    }

    private void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        LoadShiftHistory();
        RefreshDashboard();
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
        if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
        ModeratorComboBox.SelectedIndex = 0;
        _isInitializing = false;
        RefreshDashboard();
    }

    private void ChartCanvas_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (e.KeyModifiers != KeyModifiers.Control) return;
        
        var delta = e.Delta.Y > 0 ? 1.5 : 0.67;
        
        var unit = "hrs";
        if (_yAxisMax < 1.0)
        {
            unit = "min";
            if (_yAxisMax < 1.0 / 60.0)
            {
                unit = "sec";
            }
        }
        
        var maxY = unit switch
        {
            "sec" => 1.0 / 60.0,
            "min" => 1.0,
            _ => 24.0
        };
        
        _yAxisMax /= delta;
        _yAxisMax = Math.Max(1.0 / 120.0, Math.Min(maxY, _yAxisMax));
        
        ClampPan();
        DrawChart();
        e.Handled = true;
    }

    private void ChartCanvas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(null).Properties;
        if (props.IsMiddleButtonPressed)
        {
            _isPanning = true;
            _panStart = e.GetPosition(ChartCanvas);
            _panOffsetStartX = _chartOffsetX;
            ChartCanvas.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }
    }

    private void ChartCanvas_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            ChartCanvas.Cursor = Cursor.Default;
            e.Handled = true;
        }
    }

    private void ChartCanvas_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var currentPos = e.GetPosition(ChartCanvas);
        var dx = currentPos.X - _panStart.X;
        _chartOffsetX = _panOffsetStartX + dx;
        ClampPan();
        DrawChart();
        e.Handled = true;
    }

    private void ClampPan()
    {
        if (!_lastChartDates.Any()) return;
        
        var viewportWidth = ChartScrollViewer.Viewport.Width;
        if (viewportWidth <= 0) return;
        
        var padding = new Thickness(40, 20, 20, 40);
        var barWidth = 40.0;
        var barGap = 20.0;
        var baseCanvasWidth = padding.Left + padding.Right + _lastChartDates.Count * (barWidth + barGap);
        
        _chartOffsetX = Math.Min(0, Math.Max(_chartOffsetX, viewportWidth - baseCanvasWidth));
        _chartOffsetY = 0;
    }

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var dailyByModerator = _filteredShifts.GroupBy(s => s.Timestamp.Date)
            .Select(g => new
            {
                Date = g.Key,
                ModeratorHours = g.GroupBy(s => s.Moderator)
                    .Select(mg => new { Moderator = mg.Key, Hours = mg.Sum(s => GetDurationHours(s)) })
                    .OrderBy(m => m.Moderator)
                    .ToList()
            })
            .OrderBy(x => x.Date)
            .ToList();
        
        _lastChartDates = dailyByModerator.Select(x => x.Date).ToList();
        _lastChartValues = dailyByModerator.Select(x => x.ModeratorHours.Sum(m => m.Hours)).ToList();

        if (!dailyByModerator.Any())
        {
            var noData = new TextBlock { Text = "No data", Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)), FontSize = 14 };
            Canvas.SetLeft(noData, 10);
            Canvas.SetTop(noData, 10);
            ChartCanvas.Children.Add(noData);
            return;
        }

        var viewportWidth = ChartScrollViewer.Viewport.Width;
        var viewportHeight = ChartScrollViewer.Viewport.Height;
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            Dispatcher.UIThread.Post(() => DrawChart(), DispatcherPriority.Loaded);
            return;
        }

        var padding = new Thickness(40, 20, 20, 40);
        var groupWidth = 80.0;
        var groupGap = 20.0;
        var baseCanvasWidth = padding.Left + padding.Right + dailyByModerator.Count * (groupWidth + groupGap);
        
        ChartCanvas.Width = Math.Max(baseCanvasWidth, viewportWidth);
        ChartCanvas.Height = viewportHeight;
        ChartCanvas.InvalidateMeasure();
        ChartScrollViewer.InvalidateMeasure();
        
        var chartLeft = padding.Left + _chartOffsetX;
        var chartTop = padding.Top + _chartOffsetY;
        var chartWidth = baseCanvasWidth - padding.Left - padding.Right;
        var chartHeight = viewportHeight - padding.Top - padding.Bottom;
        
        var dailyTotals = dailyByModerator.Select(g => g.ModeratorHours.Sum(m => m.Hours)).ToList();
        var dataMax = dailyTotals.DefaultIfEmpty(0).Max();
        if (dataMax <= 0) dataMax = 0.1;
        if (_yAxisMax < dataMax) _yAxisMax = dataMax * 1.1;
        
        if (_yAxisMax < dataMax)
        {
            _yAxisMax = dataMax * 1.1;
        }
        
        var avgValue = dailyTotals.Average();
        
        var gridColor = Color.FromRgb(0x44, 0x44, 0x44);
        var textColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        var secondaryColor = Color.FromRgb(0x33, 0x33, 0x33);
        
        var moderatorColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "ujin", Color.FromRgb(0xa6, 0xe3, 0xa1) },
            { "mato", Color.FromRgb(0xcb, 0xa6, 0xf7) },
            { "luna", Color.FromRgb(0x89, 0xb4, 0xfa) },
            { "katy", Color.FromRgb(0xf9, 0xe2, 0xaf) }
        };
        
        Color GetModeratorColor(string mod)
        {
            if (moderatorColors.TryGetValue(mod, out var c)) return c;
            var hash = mod.GetHashCode();
            var r = (byte)((hash & 0xFF0000) >> 16);
            var g = (byte)((hash & 0x00FF00) >> 8);
            var b = (byte)(hash & 0x0000FF);
            return Color.FromRgb(r, g, b);
        }

        var mainLineCount = 13;
        
        var unit = "hrs";
        if (_yAxisMax < 1.0)
        {
            unit = "min";
            if (_yAxisMax < 1.0 / 60.0)
            {
                unit = "sec";
            }
        }
        
        var maxInUnit = unit switch
        {
            "sec" => _yAxisMax * 3600.0,
            "min" => _yAxisMax * 60.0,
            _ => _yAxisMax
        };
        
        var rawStep = maxInUnit / (mainLineCount - 1);
        var step = GetNiceStep(rawStep);
        
        for (int i = 0; i < mainLineCount; i++)
        {
            var valueInUnit = i * step;
            var fraction = valueInUnit / (step * (mainLineCount - 1));
            if (fraction > 1) fraction = 1;
            
            var y = chartTop + chartHeight - chartHeight * fraction;
            var line = new Border
            {
                Width = chartWidth,
                Height = 1,
                Background = new SolidColorBrush(gridColor)
            };
            Canvas.SetLeft(line, chartLeft);
            Canvas.SetTop(line, y);
            ChartCanvas.Children.Add(line);

            var label = new TextBlock
            {
                Text = $"{valueInUnit:0.#} {unit}",
                Foreground = new SolidColorBrush(textColor),
                FontSize = 10
            };
            Canvas.SetLeft(label, 2);
            Canvas.SetTop(label, y - 6);
            ChartCanvas.Children.Add(label);
        }
        
        for (int i = 0; i < mainLineCount - 1; i++)
        {
            var y = chartTop + chartHeight - chartHeight * (i + 0.5) / (mainLineCount - 1);
            var line = new Border
            {
                Width = chartWidth,
                Height = 1,
                Background = new SolidColorBrush(secondaryColor)
            };
            Canvas.SetLeft(line, chartLeft);
            Canvas.SetTop(line, y);
            ChartCanvas.Children.Add(line);
        }

        for (int i = 0; i < dailyByModerator.Count; i++)
        {
            var group = dailyByModerator[i];
            var mods = group.ModeratorHours.ToList();
            var barWidth = (groupWidth - (mods.Count - 1) * 4) / mods.Count;
            var groupX = chartLeft + i * (groupWidth + groupGap);
            
            for (int m = 0; m < mods.Count; m++)
            {
                var modHours = mods[m].Hours;
                var barHeight = (modHours / _yAxisMax) * chartHeight;
                var y = chartTop + chartHeight - barHeight;
                var x = groupX + m * (barWidth + 4);
                
                var bar = new Border
                {
                    Width = barWidth,
                    Height = Math.Max(1, barHeight),
                    Background = new SolidColorBrush(GetModeratorColor(mods[m].Moderator))
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, y);
                ChartCanvas.Children.Add(bar);
            }
            
            var dateStr = group.Date.ToString("MM-dd");
            var dateLabel = new TextBlock
            {
                Text = dateStr,
                Foreground = new SolidColorBrush(textColor),
                FontSize = 10
            };
            Canvas.SetLeft(dateLabel, groupX);
            Canvas.SetTop(dateLabel, chartTop + chartHeight + 5);
            ChartCanvas.Children.Add(dateLabel);
        }

        var avgY = chartTop + chartHeight - (avgValue / _yAxisMax) * chartHeight;
        var avgLine = new Border
        {
            Width = chartWidth,
            Height = 2,
            Background = new SolidColorBrush(Color.FromRgb(0xcb, 0xa6, 0xf7))
        };
        Canvas.SetLeft(avgLine, chartLeft);
        Canvas.SetTop(avgLine, avgY);
        ChartCanvas.Children.Add(avgLine);

        var avgLabel = new TextBlock
        {
            Text = $"Avg: {FormatDuration(avgValue)}",
            Foreground = new SolidColorBrush(Color.FromRgb(0xcb, 0xa6, 0xf7)),
            FontSize = 9
        };
        Canvas.SetLeft(avgLabel, chartLeft + chartWidth - 60);
        Canvas.SetTop(avgLabel, avgY - 12);
        ChartCanvas.Children.Add(avgLabel);

        LegendPanel.Children.Clear();
        var modLegendItem = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
        var modColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "ujin", Color.FromRgb(0xa6, 0xe3, 0xa1) },
            { "mato", Color.FromRgb(0xcb, 0xa6, 0xf7) },
            { "luna", Color.FromRgb(0x89, 0xb4, 0xfa) },
            { "katy", Color.FromRgb(0xf9, 0xe2, 0xaf) }
        };
        foreach (var group in dailyByModerator)
        {
            foreach (var mod in group.ModeratorHours)
            {
                if (LegendPanel.Children.OfType<StackPanel>().Any(s => s.Tag as string == mod.Moderator)) continue;
                var item = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5, Tag = mod.Moderator };
                var color = modColors.ContainsKey(mod.Moderator.ToLower()) ? modColors[mod.Moderator.ToLower()] : Color.FromRgb(0xff, 0x92, 0xd2);
                item.Children.Add(new Border { Width = 12, Height = 12, Background = new SolidColorBrush(color) });
                item.Children.Add(new TextBlock { Text = mod.Moderator, Foreground = new SolidColorBrush(textColor), FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
                LegendPanel.Children.Add(item);
            }
        }
    }

    private void BtnExportCsv_Click(object? sender, RoutedEventArgs e)
    {
        if (!_filteredShifts.Any()) return;

        var sb = new StringBuilder();
        sb.AppendLine("No,Model,Moderator,Date,Start Time,Stop Time,Duration,Notes");
        int no = 1;
        foreach (var item in _filteredShifts.OrderByDescending(s => s.Timestamp))
        {
            sb.AppendLine($"{no++},{item.Model},{item.Moderator},{item.Date},{item.StartTimeDisplay},{item.StopTimeDisplay},{item.DurationDisplay},{item.Notes}");
        }

        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_export.csv");
        File.WriteAllText(path, sb.ToString());
    }

    private void BtnExportExcel_Click(object? sender, RoutedEventArgs e)
    {
        if (!_filteredShifts.Any()) return;

        var sb = new StringBuilder();
        sb.AppendLine("No\tModel\tModerator\tDate\tStart Time\tStop Time\tDuration\tNotes");
        int no = 1;
        foreach (var item in _filteredShifts.OrderByDescending(s => s.Timestamp))
        {
            sb.AppendLine($"{no++}\t{item.Model}\t{item.Moderator}\t{item.Date}\t{item.StartTimeDisplay}\t{item.StopTimeDisplay}\t{item.DurationDisplay}\t{item.Notes}");
        }

        var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "activity_export.tsv");
        File.WriteAllText(path, sb.ToString());
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void LoadShiftHistory()
    {
        _allShifts.Clear();
        _filteredShifts.Clear();
        _minDate = null;
        _maxDate = null;

        try
        {
            if (!System.IO.File.Exists(_dataFilePath)) return;

            var json = System.IO.File.ReadAllText(_dataFilePath);
            var data = JsonSerializer.Deserialize<ShiftDataFile>(json);
            if (data?.Shifts == null) return;

            foreach (var entry in data.Shifts.OrderBy(s => s.Timestamp))
            {
                if (!_minDate.HasValue || entry.Timestamp.Date < _minDate.Value.Date) _minDate = entry.Timestamp.Date;
                if (!_maxDate.HasValue || entry.Timestamp.Date > _maxDate.Value.Date) _maxDate = entry.Timestamp.Date;

                var item = new ShiftHistoryItem
                {
                    Id = _allShifts.Count + 1,
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
        catch
        {
        }
    }

    private void ApplyTheme()
    {
        try
        {
            var settingsPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (System.IO.File.Exists(settingsPath))
            {
                var json = System.IO.File.ReadAllText(settingsPath);
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

        Background = new SolidColorBrush(Color.Parse(bgMain));
        FilterBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        FromLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        FromDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        FromDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ToLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ToDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ToDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModeratorComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ModeratorComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        DashboardTitle.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        TotalText.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        DailyAvgText.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        TopPerformerText.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        DaysTrackedText.Foreground = new SolidColorBrush(Color.Parse(fgMain));
    }

    private double GetNiceStep(double rawStep)
    {
        if (rawStep <= 0) return 1;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rawStep)));
        var normalized = rawStep / magnitude;
        if (normalized <= 1) return magnitude;
        if (normalized <= 2) return 2 * magnitude;
        if (normalized <= 3) return 3 * magnitude;
        if (normalized <= 5) return 5 * magnitude;
        if (normalized <= 7) return 7 * magnitude;
        return 10 * magnitude;
    }

    private string FormatDuration(double totalHours)
    {
        var totalSeconds = (int)Math.Round(totalHours * 3600);
        if (totalSeconds <= 0) return "0 sec";
        
        var hours = totalSeconds / 3600;
        var minutes = (totalSeconds % 3600) / 60;
        var seconds = totalSeconds % 60;
        
        if (hours > 0)
        {
            if (minutes > 0 && seconds > 0)
                return $"{hours} hour {minutes} min {seconds} sec";
            if (minutes > 0)
                return $"{hours} hour {minutes} min";
            return $"{hours} hour";
        }
        
        if (minutes > 0)
        {
            if (seconds > 0)
                return $"{minutes} min {seconds} sec";
            return $"{minutes} min";
        }
        
        return $"{seconds} sec";
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