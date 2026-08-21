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

namespace ModelTimer;

public partial class ActivityWindow : Window
{
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
    private List<CrmEntry> _crmRecords = new();
    private static readonly string[] SpendTierLabels = { "-", "$", "$$", "$$$", "$$$$", "$$$$$" };

    public ActivityWindow()
    {
        InitializeComponent();
        LoadShiftHistory();
        LoadCrmRecords();

        FromDatePicker.SelectedDateChanged += FromDatePicker_SelectedDateChanged;
        ToDatePicker.SelectedDateChanged += ToDatePicker_SelectedDateChanged;
        ModeratorComboBox.SelectionChanged += (s, e) => RefreshDashboard();
        ModelComboBox.SelectionChanged += (s, e) => RefreshDashboard();
        BtnApply.Click += (s, e) => RefreshDashboard();
        BtnRefresh.Click += (s, e) => { LoadShiftHistory(); LoadCrmRecords(); RefreshDashboard(); };
        BtnClear.Click += (s, e) =>
        {
            _isInitializing = true;
            if (_minDate.HasValue) FromDatePicker.SelectedDate = _minDate.Value;
            if (_maxDate.HasValue) ToDatePicker.SelectedDate = _maxDate.Value;
            ModeratorComboBox.SelectedIndex = 0;
            ModelComboBox.SelectedIndex = 0;
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
            ModelComboBox.IsEnabled = false;
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

            var models = _allShifts.Select(s => s.Model).Distinct().OrderBy(m => m).ToList();
            ModelComboBox.ItemsSource = new[] { "All" }.Concat(models);
            ModelComboBox.SelectedIndex = 0;
        }

        RefreshDashboard();
        ApplyTheme();

        Dispatcher.UIThread.Post(() => DrawChart(), DispatcherPriority.Loaded);

        ShiftDataStore.Changed += DataStore_Changed;
        CrmDataStore.Changed += DataStore_Changed;
        Closed += (s, e) =>
        {
            ShiftDataStore.Changed -= DataStore_Changed;
            CrmDataStore.Changed -= DataStore_Changed;
        };
    }

    private void DataStore_Changed()
    {
        LoadShiftHistory();
        LoadCrmRecords();
        RefreshDashboard();
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

    private double GetDurationHours(ShiftHistoryItem item) =>
        ShiftMath.DurationHours(item.ElapsedHours, item.ElapsedMinutes, item.ElapsedSeconds);

    private double GetLostTimeHours(ShiftHistoryItem item) => ShiftMath.LostTimeHours(item.LostTimeSeconds);

    private double GetPlannedHours(ShiftHistoryItem item) =>
        ShiftMath.PlannedHours(item.DurationHours, item.DurationMinutes);

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
            _filteredShifts = new List<ShiftHistoryItem>();
            RefreshInsights();
            return;
        }

        var fromDate = FromDatePicker.SelectedDate?.DateTime;
        var toDate = ToDatePicker.SelectedDate?.DateTime;
        var moderator = ModeratorComboBox.SelectedItem as string;
        var model = ModelComboBox.SelectedItem as string;

        _filteredShifts = _allShifts.Where(item =>
        {
            bool matchesFrom = !fromDate.HasValue || item.Timestamp.Date >= fromDate.Value.Date;
            bool matchesTo = !toDate.HasValue || item.Timestamp.Date <= toDate.Value.Date;
            bool matchesMod = string.IsNullOrEmpty(moderator) || moderator == "All" || item.Moderator == moderator;
            bool matchesModel = string.IsNullOrEmpty(model) || model == "All" || item.Model == model;
            return matchesFrom && matchesTo && matchesMod && matchesModel;
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
            RefreshInsights();
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
            .Select(g => g.Sum(s => GetDurationHours(s)) + g.Sum(s => GetLostTimeHours(s)))
            .ToList();

        var dataMax = dailyTotals.DefaultIfEmpty(0).Max();
        if (dataMax <= 0) dataMax = 0.1;
        _yAxisMax = dataMax;

        DrawChart();
        RefreshInsights();
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
        
        // Must match DrawChart's own group sizing exactly, or the pan clamp drifts out of sync
        // with where the chart actually ends.
        var padding = new Thickness(40, 20, 20, 40);
        var baseCanvasWidth = padding.Left + padding.Right + _lastChartDates.Count * (GroupWidth + GroupGap);

        _chartOffsetX = Math.Min(0, Math.Max(_chartOffsetX, viewportWidth - baseCanvasWidth));
        _chartOffsetY = 0;
    }

    private const double GroupWidth = 80.0;
    private const double GroupGap = 20.0;

    private void DrawChart()
    {
        ChartCanvas.Children.Clear();

        var dailyByModerator = _filteredShifts.GroupBy(s => s.Timestamp.Date)
            .Select(g => new
            {
                Date = g.Key,
                ModeratorHours = g.GroupBy(s => s.Moderator)
                    .Select(mg => new
                    {
                        Moderator = mg.Key,
                        // Worked time is split at the planned shift length: anything beyond it is
                        // "overrun" (the shift ran long) rather than folded silently into worked hours.
                        Hours = mg.Sum(s => Math.Min(GetDurationHours(s), GetPlannedHours(s))),
                        OverrunHours = mg.Sum(s => ShiftMath.OverrunHours(GetDurationHours(s), GetPlannedHours(s))),
                        LostHours = mg.Sum(s => GetLostTimeHours(s))
                    })
                    .OrderBy(m => m.Moderator)
                    .ToList()
            })
            .OrderBy(x => x.Date)
            .ToList();

        _lastChartDates = dailyByModerator.Select(x => x.Date).ToList();
        _lastChartValues = dailyByModerator.Select(x => x.ModeratorHours.Sum(m => m.Hours + m.OverrunHours + m.LostHours)).ToList();

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
        var groupWidth = GroupWidth;
        var groupGap = GroupGap;
        var baseCanvasWidth = padding.Left + padding.Right + dailyByModerator.Count * (groupWidth + groupGap);
        
        ChartCanvas.Width = Math.Max(baseCanvasWidth, viewportWidth);
        ChartCanvas.Height = viewportHeight;
        ChartCanvas.InvalidateMeasure();
        ChartScrollViewer.InvalidateMeasure();
        
        var chartLeft = padding.Left + _chartOffsetX;
        var chartTop = padding.Top + _chartOffsetY;
        var chartWidth = baseCanvasWidth - padding.Left - padding.Right;
        var chartHeight = viewportHeight - padding.Top - padding.Bottom;
        
        var dailyTotals = dailyByModerator.Select(g => g.ModeratorHours.Sum(m => m.Hours + m.OverrunHours + m.LostHours)).ToList();
        var dataMax = dailyTotals.DefaultIfEmpty(0).Max();
        if (dataMax <= 0) dataMax = 0.1;
        if (_yAxisMax < dataMax) _yAxisMax = dataMax * 1.1;

        var avgValue = dailyTotals.Average();

        var gridColor = Color.FromRgb(0x44, 0x44, 0x44);
        var textColor = Color.FromRgb(0xFF, 0xFF, 0xFF);
        var secondaryColor = Color.FromRgb(0x33, 0x33, 0x33);
        var lostTimeColor = Color.FromRgb(0xf3, 0x8b, 0xa8);
        var overrunColor = Color.FromRgb(0xf9, 0xa8, 0x4b);

        var moderatorColorMap = ModeratorColorAssigner.Assign(_allShifts.Select(s => s.Moderator));

        Color GetModeratorColor(string mod)
        {
            return moderatorColorMap.TryGetValue(mod, out var c) ? c : Color.FromRgb(0xcc, 0xcc, 0xcc);
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
                var overrunHours = mods[m].OverrunHours;
                var lostHours = mods[m].LostHours;
                var barHeight = (modHours / _yAxisMax) * chartHeight;
                var overrunHeight = (overrunHours / _yAxisMax) * chartHeight;
                var lostHeight = (lostHours / _yAxisMax) * chartHeight;
                var x = groupX + m * (barWidth + 4);
                var topY = chartTop + chartHeight - barHeight - overrunHeight - lostHeight;

                // Stacked bottom-to-top: planned worked time (moderator color), then any
                // overrun beyond the planned shift length (amber), then paused/lost time
                // (red) - all in the SAME bar for that moderator/day, never a separate one.
                if (lostHeight > 0)
                {
                    var lostBar = new Border
                    {
                        Width = barWidth,
                        Height = Math.Max(1, lostHeight),
                        Background = new SolidColorBrush(lostTimeColor)
                    };
                    Canvas.SetLeft(lostBar, x);
                    Canvas.SetTop(lostBar, topY);
                    ChartCanvas.Children.Add(lostBar);
                }

                if (overrunHeight > 0)
                {
                    var overrunBar = new Border
                    {
                        Width = barWidth,
                        Height = Math.Max(1, overrunHeight),
                        Background = new SolidColorBrush(overrunColor)
                    };
                    Canvas.SetLeft(overrunBar, x);
                    Canvas.SetTop(overrunBar, topY + lostHeight);
                    ChartCanvas.Children.Add(overrunBar);
                }

                var bar = new Border
                {
                    Width = barWidth,
                    Height = Math.Max(1, barHeight),
                    Background = new SolidColorBrush(GetModeratorColor(mods[m].Moderator))
                };
                Canvas.SetLeft(bar, x);
                Canvas.SetTop(bar, topY + lostHeight + overrunHeight);
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

        // The chart clears LegendPanel every draw, so the static "Avg" swatch defined in XAML
        // never actually survives to be seen - rebuild it here alongside everything else.
        var avgItem = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
        avgItem.Children.Add(new Border { Width = 12, Height = 2, Background = new SolidColorBrush(Color.FromRgb(0xcb, 0xa6, 0xf7)) });
        avgItem.Children.Add(new TextBlock { Text = "Avg (average hrs/day)", Foreground = new SolidColorBrush(textColor), FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        LegendPanel.Children.Add(avgItem);

        var addedModerators = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var anyLostTime = false;
        var anyOverrun = false;
        foreach (var group in dailyByModerator)
        {
            foreach (var mod in group.ModeratorHours)
            {
                if (mod.LostHours > 0) anyLostTime = true;
                if (mod.OverrunHours > 0) anyOverrun = true;
                if (!addedModerators.Add(mod.Moderator)) continue;

                var item = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5, Tag = mod.Moderator };
                item.Children.Add(new Border { Width = 12, Height = 12, Background = new SolidColorBrush(GetModeratorColor(mod.Moderator)) });
                item.Children.Add(new TextBlock { Text = mod.Moderator, Foreground = new SolidColorBrush(textColor), FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
                LegendPanel.Children.Add(item);
            }
        }

        if (anyOverrun)
        {
            var overrunItem = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            overrunItem.Children.Add(new Border { Width = 12, Height = 12, Background = new SolidColorBrush(overrunColor) });
            overrunItem.Children.Add(new TextBlock { Text = "Overrun", Foreground = new SolidColorBrush(textColor), FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            LegendPanel.Children.Add(overrunItem);
        }

        if (anyLostTime)
        {
            var lostItem = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 5 };
            lostItem.Children.Add(new Border { Width = 12, Height = 12, Background = new SolidColorBrush(lostTimeColor) });
            lostItem.Children.Add(new TextBlock { Text = "Lost Time", Foreground = new SolidColorBrush(textColor), FontSize = 10, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            LegendPanel.Children.Add(lostItem);
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
        try
        {
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception ex)
        {
            JsonStore.LogError($"Failed to write {path}", ex);
        }
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
        try
        {
            File.WriteAllText(path, sb.ToString());
        }
        catch (Exception ex)
        {
            JsonStore.LogError($"Failed to write {path}", ex);
        }
    }

    private void TitleBar_PointerPressed(object sender, PointerPressedEventArgs e)
    {
        BeginMoveDrag(e);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void BtnSuggestCoverage_Click(object sender, RoutedEventArgs e)
    {
        var settings = SettingsStore.Load();
        if (!AiSummaryService.IsConfigured(settings))
        {
            ShowInfoDialog("AI Not Set Up", "Add a provider and API key under Settings to get coverage suggestions.");
            return;
        }

        if (!_allShifts.Any())
        {
            ShowInfoDialog("Suggest Coverage", "No shift history yet to base a suggestion on.");
            return;
        }

        if (!await AiConsentService.EnsureConsentAsync(this, settings)) return;

        var originalContent = BtnSuggestCoverage.Content;
        try
        {
            BtnSuggestCoverage.IsEnabled = false;
            BtnSuggestCoverage.Content = "Thinking...";
            CoverageAnswerPanel.Children.Clear();
            CoverageAnswerPanel.Children.Add(new TextBlock
            {
                Text = "Analyzing coverage, hang on...",
                Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap
            });
            CoverageAnswerBorder.IsVisible = true;

            var context = BuildCoverageContext();
            const string question = "Based on this shift coverage data (hours worked, grouped by day of week and " +
                "by moderator), which day(s) of the week are under-covered relative to the others, and which " +
                "moderator would make sense to schedule there based on their existing pattern? Be specific and practical.";

            var result = await AiSummaryService.AskAsync(settings, question, context);
            AskResultRenderer.Render(CoverageAnswerPanel, result);
        }
        catch (Exception ex)
        {
            CoverageAnswerBorder.IsVisible = false;
            ShowInfoDialog("AI Unavailable", ex.Message);
        }
        finally
        {
            BtnSuggestCoverage.IsEnabled = true;
            BtnSuggestCoverage.Content = originalContent;
        }
    }

    private string BuildCoverageContext()
    {
        var sb = new StringBuilder();
        sb.AppendLine("HOURS WORKED BY DAY OF WEEK (all moderators combined):");
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var hours = _allShifts.Where(s => s.Timestamp.DayOfWeek == day).Sum(GetDurationHours);
            sb.AppendLine($"- {day}: {hours:0.0} hrs");
        }

        sb.AppendLine();
        sb.AppendLine("HOURS WORKED BY MODERATOR, BROKEN DOWN BY DAY OF WEEK:");
        foreach (var modGroup in _allShifts.GroupBy(s => s.Moderator))
        {
            var byDay = modGroup.GroupBy(s => s.Timestamp.DayOfWeek)
                .Select(g => $"{g.Key}: {g.Sum(GetDurationHours):0.0}h")
                .ToList();
            sb.AppendLine($"- {modGroup.Key}: {string.Join(", ", byDay)}");
        }

        return sb.ToString();
    }

    private void ShowInfoDialog(string title, string message) => AppDialog.ShowInfo(this, title, message);

    private void LoadShiftHistory()
    {
        _allShifts.Clear();
        _filteredShifts.Clear();
        _minDate = null;
        _maxDate = null;

        var data = ShiftDataStore.Load();
        if (data.Shifts == null) return;

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
                ElapsedSeconds = entry.ElapsedSeconds,
                LostTimeSeconds = entry.LostTimeSeconds
            };

            _allShifts.Add(item);
        }
    }

    private void LoadCrmRecords()
    {
        _crmRecords.Clear();
        var data = CrmDataStore.Load();
        if (data.Records == null) return;
        _crmRecords.AddRange(data.Records);
    }

    private void RefreshInsights()
    {
        TopSpendersPanel.Children.Clear();
        ChurnRiskPanel.Children.Clear();
        BestSlotsPanel.Children.Clear();

        var topSpenders = _crmRecords
            .Where(r => r.SpendTier > 0)
            .OrderByDescending(r => r.SpendTier)
            .ThenByDescending(r => r.CreatedAt)
            .Take(5)
            .ToList();

        if (topSpenders.Count == 0)
        {
            AddInsightRow(TopSpendersPanel, "No spend-tier fans tagged yet.", "#FF888888");
        }
        else
        {
            foreach (var fan in topSpenders)
            {
                var tier = SpendTierLabels[Math.Clamp(fan.SpendTier, 0, SpendTierLabels.Length - 1)];
                var site = string.IsNullOrWhiteSpace(fan.Site) ? "Unknown site" : fan.Site;
                AddInsightRow(TopSpendersPanel, $"{fan.User} · {site} · {fan.Model} — {tier}", "#FFFFFFFF");
            }
        }

        var churnCutoff = DateTime.Now.AddDays(-14);
        var churnRisk = _crmRecords
            .Where(r => r.CreatedAt < churnCutoff)
            .OrderBy(r => r.CreatedAt)
            .Take(5)
            .ToList();

        if (churnRisk.Count == 0)
        {
            AddInsightRow(ChurnRiskPanel, "No fans overdue for a check-in.", "#FF888888");
        }
        else
        {
            foreach (var fan in churnRisk)
            {
                var daysAgo = (int)(DateTime.Now - fan.CreatedAt).TotalDays;
                var site = string.IsNullOrWhiteSpace(fan.Site) ? "Unknown site" : fan.Site;
                AddInsightRow(ChurnRiskPanel, $"{fan.User} · {site} · {fan.Model} — {daysAgo}d since last touch", "#FFFFFFFF");
            }
        }

        var bestDays = _filteredShifts
            .GroupBy(s => s.Timestamp.DayOfWeek)
            .Select(g => new { Day = g.Key, Hours = g.Sum(GetDurationHours) })
            .OrderByDescending(x => x.Hours)
            .Take(3)
            .ToList();

        if (bestDays.Count == 0)
        {
            AddInsightRow(BestSlotsPanel, "No shift data in range.", "#FF888888");
        }
        else
        {
            foreach (var d in bestDays)
            {
                AddInsightRow(BestSlotsPanel, $"{d.Day} — {FormatDuration(d.Hours)} logged", "#FFFFFFFF");
            }
        }
    }

    private void AddInsightRow(StackPanel panel, string text, string colorHex)
    {
        panel.Children.Add(new TextBlock
        {
            Text = text,
            Foreground = new SolidColorBrush(Color.Parse(colorHex)),
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap
        });
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
        FromLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        FromDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        FromDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ToLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ToDatePicker.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ToDatePicker.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModeratorComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ModeratorComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModelLabel.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ModelComboBox.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        ModelComboBox.Foreground = new SolidColorBrush(Color.Parse(fgMain));
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

    private string FormatDuration(double totalHours) => ShiftMath.FormatDuration(totalHours);

}