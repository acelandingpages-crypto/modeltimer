using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.Threading;
using System;
using System.IO;
using System.Threading.Tasks;

namespace ModelTimer;

public partial class MainWindow : Window
{
    private DispatcherTimer? _timer;
    private TimeSpan _totalTime = TimeSpan.FromHours(10);
    private TimeSpan _elapsed = TimeSpan.Zero;
    private bool _isRunning = false;
    private bool _isPaused = false;

    private string _currentModel = string.Empty;
    private string _currentModerator = string.Empty;
    private Image? _currentImage;
    private string? _currentImageName;
    private Viewbox? _readyViewbox;
    private NewShiftWindow? _newShiftWindow;
    private SettingsWindow? _settingsWindow;
    private int _dialogOffsetX;
    private int _dialogOffsetY;
    private bool _notified15Min;
    private bool _notified5Min;
    private bool _notifiedComplete;
    private bool _notifyOnShiftComplete = true;
    private bool _warn5Min = true;
    private bool _warn15Min = true;
    private string _showHotkey = "ctrl + s";
    private string _privateHotkey = "ctrl + p";
    private string _typingHotkey = "ctrl + k";
    private string _activeShiftPath = string.Empty;
    private bool _closeConfirmed;

    public MainWindow()
    {
        InitializeComponent();
        SetupTimer();
        UpdateDisplay();
        SetupShortcuts();
        SizeChanged += (s, e) => UpdateDisplay();
        _readyViewbox = ReadyViewbox;
        UpdateShiftStatus();
        UpdateButtonStates();
        PositionChanged += MainWindow_PositionChanged;
        Closing += MainWindow_Closing;

        LoadThemeOnStartup();

        Dispatcher.UIThread.Post(() => CheckForActiveShift(), DispatcherPriority.Loaded);

        try
        {
            Icon = new WindowIcon(new Bitmap(AssetLoader.Open(new Uri("avares://ModelTimer/Assets/favicon.ico"))));
        }
        catch
        {
        }
    }

    private void MainWindow_PositionChanged(object? sender, EventArgs e)
    {
        if (_newShiftWindow != null && _newShiftWindow.IsVisible)
        {
            _newShiftWindow.Position = new PixelPoint(Position.X + _dialogOffsetX, Position.Y + _dialogOffsetY);
        }
    }

    private void SetupTimer()
    {
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_isRunning || _isPaused) return;

        _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        TimeSpan remaining = _totalTime - _elapsed;
        if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

        ElapsedText.Text = _elapsed.ToString(@"hh\:mm\:ss");
        MilestoneText.Text = _isRunning ? GetMilestoneMessage(_elapsed) : string.Empty;

        double progressPercent = _totalTime.TotalSeconds > 0 ? (_elapsed.TotalSeconds / _totalTime.TotalSeconds) * 100 : 0;
        if (progressPercent > 100) progressPercent = 100;
        ProgressText.Text = $"{progressPercent:F0}%";

        double totalWidth = ProgressBar.Bounds.Width;
        if (totalWidth > 0)
        {
            ProgressGreen.Width = totalWidth * (progressPercent / 100.0);
            ProgressRed.Width = totalWidth * ((100 - progressPercent) / 100.0);
        }

        if (_isRunning && !_isPaused)
        {
            var remainingSeconds = remaining.TotalSeconds;
            if (remainingSeconds <= 0 && !_notifiedComplete && _notifyOnShiftComplete)
            {
                _notifiedComplete = true;
                ShiftStatusText.Text = "Shift complete!";
                ShiftStatusText.Foreground = new SolidColorBrush(Color.Parse("#FF00FF00"));
            }
            else if (remainingSeconds >= 299 && remainingSeconds <= 301 && !_notified5Min && _warn5Min)
            {
                _notified5Min = true;
                ShiftStatusText.Text = "5 minutes remaining!";
                ShiftStatusText.Foreground = new SolidColorBrush(Color.Parse("#FFFF0000"));
                Dispatcher.UIThread.Post(() => ShowWarningDialog("5 Minute Warning", "Only 5 minutes remaining in this shift!"), DispatcherPriority.Loaded);
            }
            else if (remainingSeconds >= 899 && remainingSeconds <= 901 && !_notified15Min && _warn15Min)
            {
                _notified15Min = true;
                ShiftStatusText.Text = "15 minutes remaining!";
                ShiftStatusText.Foreground = new SolidColorBrush(Color.Parse("#FFFFAA00"));
                Dispatcher.UIThread.Post(() => ShowWarningDialog("15 Minute Warning", "Only 15 minutes remaining in this shift!"), DispatcherPriority.Loaded);
            }
        }
    }

    private string GetMilestoneMessage(TimeSpan elapsed)
    {
        var hours = elapsed.TotalHours;
        return hours switch
        {
            >= 5 => "🏆 5+ hours — legendary shift!",
            >= 4 => "🚀 4 hours — powering through!",
            >= 3 => "💪 3 hours — crushing it!",
            >= 2 => "⚡ 2 hours — great pace!",
            >= 1 => "🔥 1 hour in — warmed up!",
            _ => "Just getting started..."
        };
    }

    private void StartResume_Click(object? sender, RoutedEventArgs e)
    {
        if (!_isRunning && !_isPaused)
        {
            if (_newShiftWindow != null && _newShiftWindow.IsVisible)
            {
                _newShiftWindow.Activate();
                return;
            }

            _newShiftWindow = new NewShiftWindow();
            _newShiftWindow.Opened += (s, args) =>
            {
                _dialogOffsetX = _newShiftWindow.Position.X - Position.X;
                _dialogOffsetY = _newShiftWindow.Position.Y - Position.Y;
            };
            _newShiftWindow.Closed += (s, args) =>
            {
                if (_newShiftWindow.Confirmed)
                {
                    var model = _newShiftWindow.SelectedModel;
                    var moderator = _newShiftWindow.SelectedModerator;
                    var hours = _newShiftWindow.DurationHours;
                    var minutes = _newShiftWindow.DurationMinutes;
                    _newShiftWindow = null;

                    var preShift = new PreShiftConfirmWindow(model, moderator, hours, minutes);
                    preShift.Closed += (s2, args2) =>
                    {
                        if (preShift.Confirmed)
                        {
                            BeginShift(model, moderator, hours, minutes);
                        }
                    };
                    preShift.Show(this);
                    return;
                }
                _newShiftWindow = null;
            };
            _newShiftWindow.Show(this);
        }
    }

    private void BeginShift(string model, string moderator, int durationHours, int durationMinutes)
    {
        var totalMinutes = durationHours * 60 + durationMinutes;
        _totalTime = TimeSpan.FromMinutes(totalMinutes);
        _elapsed = TimeSpan.Zero;
        _isRunning = true;
        _isPaused = false;
        _currentModel = model;
        _currentModerator = moderator;
        _notified15Min = false;
        _notified5Min = false;
        _notifiedComplete = false;
        _timer?.Start();
        UpdateButtonStates();
        UpdateShiftStatus();
        UpdateDisplay();
        ShowStatusImage("show.png", 390, 280);
        SaveActiveShiftState();
    }

    private void Pause_Click(object? sender, RoutedEventArgs e)
    {
        if (_isRunning && !_isPaused)
        {
            _isPaused = true;
            _timer?.Stop();
            BtnPause.Content = "Resume";
        }
        else if (_isPaused)
        {
            _isPaused = false;
            _timer?.Start();
            BtnPause.Content = "Pause";
        }
    }

    private void Stop_Click(object? sender, RoutedEventArgs e)
    {
        var actualElapsed = _elapsed;
        var finishedModel = _currentModel;
        var finishedModerator = _currentModerator;
        StopTimer();
        _elapsed = TimeSpan.Zero;
        UpdateDisplay();
        UpdateButtonStates();
        UpdateShiftStatus();

        StatusImage.Source = null;
        StatusImage.IsVisible = false;
        StatusImageBorder.IsVisible = false;
        StatusImagePanel.IsVisible = false;
        if (_readyViewbox != null) _readyViewbox.IsVisible = true;
        ImageBorder.Background = new SolidColorBrush(Color.Parse("#FF2D2D30"));

        SaveShiftStopTime(actualElapsed);
        ClearActiveShiftState();

        var reportWindow = new ShiftReportWindow(finishedModel, finishedModerator, actualElapsed);
        reportWindow.Show(this);
    }

    private void StopTimer()
    {
        _isRunning = false;
        _isPaused = false;
        _timer?.Stop();
    }

    private void UpdateButtonStates()
    {
        if (_isRunning)
        {
            BtnStart.IsEnabled = false;
            BtnPause.IsEnabled = true;
            BtnPause.Content = "Pause";
            BtnStop.IsEnabled = true;
        }
        else if (_isPaused)
        {
            BtnStart.IsEnabled = false;
            BtnPause.IsEnabled = true;
            BtnPause.Content = "Resume";
            BtnStop.IsEnabled = true;
        }
        else
        {
            BtnStart.IsEnabled = true;
            BtnPause.IsEnabled = false;
            BtnPause.Content = "Pause";
            BtnStop.IsEnabled = false;
        }
    }

    private void UpdateShiftStatus()
    {
        if (_isRunning)
        {
            ShiftStatusText.IsVisible = false;
            ShiftStatusPanel.IsVisible = true;
            StatusModelName.Text = _currentModel;
            StatusModeratorName.Text = _currentModerator;
        }
        else
        {
            ShiftStatusPanel.IsVisible = false;
            ShiftStatusText.IsVisible = true;
            ShiftStatusText.Text = "No active shift";
            ShiftStatusText.Foreground = new SolidColorBrush(Color.Parse("#FFFF0000"));
        }
    }

    private void ShowWarningDialog(string title, string message)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
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

        var okBtn = new Button { Content = "OK", Width = 100, Height = 30, Background = new SolidColorBrush(Color.Parse("#FFa6e3a1")), Foreground = new SolidColorBrush(Color.Parse("#FF000000")), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        okBtn.Click += (s, e) => dialog.Close();

        panel.Children.Add(okBtn);
        dialog.Content = panel;
        dialog.ShowDialog(this);
    }

    private void SetupShortcuts()
    {
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var pressed = GetHotkeyString(e.Key, e.KeyModifiers);
        if (pressed == _showHotkey)
        {
            ShowStatusImage("show.png", 390, 280);
            e.Handled = true;
        }
        else if (pressed == _privateHotkey)
        {
            ShowStatusImage("pvt.png", 800, 250);
            e.Handled = true;
        }
        else if (pressed == _typingHotkey)
        {
            ShowStatusImage("keyboard.png", 512, 512);
            e.Handled = true;
        }
        else if (e.Key == Key.Space)
        {
            if (e.Source is TextBox) return;
            if (e.Source is ComboBox) return;
            e.Handled = true;
        }
    }

    private string GetHotkeyString(Key key, KeyModifiers modifiers)
    {
        string result = "";
        if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control) result += "ctrl + ";
        if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift) result += "shift + ";
        if ((modifiers & KeyModifiers.Alt) == KeyModifiers.Alt) result += "alt + ";
        result += key.ToString().ToLower();
        return result;
    }

    private void ShowButton_Click(object sender, RoutedEventArgs e) => ShowStatusImage("show.png", 390, 280);
    private void PrivateButton_Click(object sender, RoutedEventArgs e) => ShowStatusImage("pvt.png", 800, 250);
    private void TypingButton_Click(object sender, RoutedEventArgs e) => ShowStatusImage("keyboard.png", 512, 512);

    private void ShowStatusImage(string imageName, int width, int height)
    {
        if (_currentImageName == imageName && StatusImage.IsVisible)
        {
            return;
        }

        _currentImageName = imageName;

        try
        {
            var uri = new Uri($"avares://ModelTimer/Assets/{imageName}");
            StatusImage.Source = new Bitmap(AssetLoader.Open(uri));
            StatusImage.IsVisible = true;
            StatusImageBorder.IsVisible = true;
            StatusImagePanel.IsVisible = true;
            _currentImage = StatusImage;
            if (_readyViewbox != null) _readyViewbox.IsVisible = false;

            if (StatusImageLabel != null)
            {
                StatusImageLabel.Text = imageName switch
                {
                    "pvt.png" => "PRIVATE",
                    "keyboard.png" => "TYPING",
                    "show.png" => "SHOW",
                    _ => string.Empty
                };
            }

            ImageBorder.Background = imageName switch
            {
                "pvt.png" => new SolidColorBrush(Color.Parse("#FFcba6f7")),
                "keyboard.png" => new SolidColorBrush(Color.Parse("#FF89dceb")),
                "show.png" => new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                _ => new SolidColorBrush(Color.Parse("#FF2D2D30"))
            };
        }
        catch
        {
            StatusImage.Source = null;
            StatusImage.IsVisible = false;
            StatusImageBorder.IsVisible = false;
            StatusImagePanel.IsVisible = false;
            _currentImage = null;
            ImageBorder.Background = new SolidColorBrush(Color.Parse("#FF2D2D30"));
            if (_readyViewbox != null) _readyViewbox.IsVisible = true;
        }
    }

    private void BtnHighTraffic_Click(object sender, RoutedEventArgs e)
    {
        var highTrafficWindow = new HighTrafficWindow();
        highTrafficWindow.Show(this);
    }
    private void BtnShiftHistory_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = new ShiftHistoryWindow();
        historyWindow.Show(this);
    }
    private void BtnActivity_Click(object sender, RoutedEventArgs e)
    {
        var activityWindow = new ActivityWindow();
        activityWindow.Show(this);
    }
    private void BtnAsk_Click(object sender, RoutedEventArgs e)
    {
        var askWindow = new AskHistoryWindow();
        askWindow.Show(this);
    }
    private void BtnSettings_Click(object sender, RoutedEventArgs e)
    {
        _settingsWindow = new SettingsWindow();
        _settingsWindow.Opened += (s, args) =>
        {
            _dialogOffsetX = _settingsWindow.Position.X - Position.X;
            _dialogOffsetY = _settingsWindow.Position.Y - Position.Y;
        };
        _settingsWindow.Closed += (s, args) =>
        {
            if (_settingsWindow.Confirmed)
            {
                ApplySettings(_settingsWindow);
            }
            _settingsWindow = null;
        };
        _settingsWindow.Show(this);
    }

    private void ApplySettings(SettingsWindow settings)
    {
        _showHotkey = settings.ShowHotkey;
        _privateHotkey = settings.PrivateHotkey;
        _typingHotkey = settings.TypingHotkey;
        _notifyOnShiftComplete = settings.NotifyOnShiftComplete;
        _warn5Min = settings.Warn5Min;
        _warn15Min = settings.Warn15Min;

        if (settings.SelectedTheme == "Light")
        {
            RequestedThemeVariant = ThemeVariant.Light;
            if (Application.Current is App app)
            {
                app.RequestedThemeVariant = ThemeVariant.Light;
            }
            UpdateThemeColors(true);
        }
        else
        {
            RequestedThemeVariant = ThemeVariant.Dark;
            if (Application.Current is App app)
            {
                app.RequestedThemeVariant = ThemeVariant.Dark;
            }
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
        var borderLight = isLight ? "#FFCCCCCC" : "#FF888888";
        var progressBg = isLight ? "#FFCCCCCC" : "#FF555555";

        Background = new SolidColorBrush(Color.Parse(bgMain));
        BtnHighTraffic.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnHighTraffic.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnShiftHistory.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnShiftHistory.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnActivity.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnActivity.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnAsk.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnAsk.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        BtnSettings.Background = new SolidColorBrush(Color.Parse(bgToolbar));
        BtnSettings.Foreground = new SolidColorBrush(Color.Parse(fgMain));

        TimerBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        ProgressBorder.Background = new SolidColorBrush(Color.Parse("#FF2D2D30"));
        ProgressText.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        ReadyText.Foreground = new SolidColorBrush(Color.Parse("#FFFF0000"));
        HourglassText.Foreground = new SolidColorBrush(Color.Parse(fgMuted));

        foreach (var child in ReadyPanel.Children)
        {
            if (child is Border border && border.BorderBrush is SolidColorBrush)
            {
                border.BorderBrush = new SolidColorBrush(Color.Parse(borderLight));
            }
        }

        ShiftStatusBorder.Background = new SolidColorBrush(Color.Parse(bgSurface));
        StatusModelPrefix.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        StatusModeratorPrefix.Foreground = new SolidColorBrush(Color.Parse(fgMain));
        StatusModelName.Foreground = isLight ? new SolidColorBrush(Color.Parse("#FF107C10")) : new SolidColorBrush(Color.Parse("#FFa6e3a1"));
        StatusModeratorName.Foreground = isLight ? new SolidColorBrush(Color.Parse("#FF107C10")) : new SolidColorBrush(Color.Parse("#FFa6e3a1"));
        ShiftStatusText.Foreground = new SolidColorBrush(Color.Parse("#FFFF0000"));
        StatusImageLabel.Foreground = new SolidColorBrush(Color.Parse("#FF000000"));
    }

    private void LoadThemeOnStartup()
    {
        var settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
        var settings = JsonStore.Load<AppSettings>(settingsPath);
        if (settings != null && settings.Theme == "Light")
        {
            RequestedThemeVariant = ThemeVariant.Light;
            if (Application.Current is App app)
            {
                app.RequestedThemeVariant = ThemeVariant.Light;
            }
            UpdateThemeColors(true);
        }
        else
        {
            UpdateThemeColors(false);
        }
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed) return;

        e.Cancel = true;
        var confirmed = await ShowConfirmCloseDialog();
        if (confirmed)
        {
            _closeConfirmed = true;
            Close();
        }
    }

    private async Task<bool> ShowConfirmCloseDialog()
    {
        var dialog = new Window
        {
            Title = "Close ModelTimer?",
            Width = 400,
            Height = 200,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var message = _isRunning
            ? "You have an active shift running. Closing will save your progress so you can resume it next time you open ModelTimer.\n\nAre you sure you want to close?"
            : "Are you sure you want to close ModelTimer?";

        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });

        var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var cancelBtn = new Button { Content = "Cancel", Width = 100, Height = 30, Background = new SolidColorBrush(Color.Parse("#FFa6e3a1")), Foreground = new SolidColorBrush(Color.Parse("#FF000000")) };
        var closeBtn = new Button { Content = "Close", Width = 100, Height = 30, Background = new SolidColorBrush(Color.Parse("#FFC42B1C")), Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")) };

        var result = false;
        cancelBtn.Click += (s, e2) => { result = false; dialog.Close(); };
        closeBtn.Click += (s, e2) => { result = true; dialog.Close(); };

        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(closeBtn);
        panel.Children.Add(btnPanel);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        return result;
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer?.Stop();
        if (_isRunning)
        {
            SaveActiveShiftState();
        }
        StatusImage.Source = null;
        StatusImage.IsVisible = false;
        StatusImageBorder.IsVisible = false;
        StatusImagePanel.IsVisible = false;
        if (_readyViewbox != null) _readyViewbox.IsVisible = true;
        base.OnClosed(e);
    }

    private void SaveShiftStopTime(TimeSpan elapsed)
    {
        var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        var data = JsonStore.Load<ShiftDataFile>(filePath);
        if (data?.Shifts == null || data.Shifts.Count == 0) return;

        var lastEntry = data.Shifts[^1];
        if (lastEntry.StopTime == null)
        {
            lastEntry.StopTime = DateTime.Now;
            lastEntry.ElapsedHours = (int)elapsed.TotalHours;
            lastEntry.ElapsedMinutes = elapsed.Minutes;
            lastEntry.ElapsedSeconds = elapsed.Seconds;
            JsonStore.Save(filePath, data);
        }
    }

    private void SaveActiveShiftState()
    {
        _activeShiftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "active_shift.json");
        var state = new ActiveShiftState
        {
            Model = _currentModel,
            Moderator = _currentModerator,
            ElapsedSeconds = (long)_elapsed.TotalSeconds,
            DurationHours = (int)_totalTime.TotalHours,
            DurationMinutes = _totalTime.Minutes
        };
        JsonStore.Save(_activeShiftPath, state);
    }

    private ActiveShiftState? LoadActiveShiftState()
    {
        _activeShiftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "active_shift.json");
        return JsonStore.Load<ActiveShiftState>(_activeShiftPath);
    }

    private void ClearActiveShiftState()
    {
        try
        {
            _activeShiftPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "active_shift.json");
            if (File.Exists(_activeShiftPath)) File.Delete(_activeShiftPath);
        }
        catch (Exception ex)
        {
            JsonStore.LogError($"Failed to clear {_activeShiftPath}", ex);
        }
    }

    private void CheckForActiveShift()
    {
        var state = LoadActiveShiftState();
        if (state == null) return;

        var dialog = new Window
        {
            Title = "Resume Shift",
            Width = 400,
            Height = 180,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        var panel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        panel.Children.Add(new TextBlock
        {
            Text = $"Active shift found:\nModel: {state.Model}\nModerator: {state.Moderator}\nElapsed: {TimeSpan.FromSeconds(state.ElapsedSeconds):hh\\:mm\\:ss}",
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });

        var btnPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
        var resumeBtn = new Button { Content = "Resume", Width = 100, Height = 30, Background = new SolidColorBrush(Color.Parse("#FFa6e3a1")), Foreground = new SolidColorBrush(Color.Parse("#FF000000")) };
        var freshBtn = new Button { Content = "Start Fresh", Width = 100, Height = 30, Background = new SolidColorBrush(Color.Parse("#FFFF0000")), Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")) };

        resumeBtn.Click += (s, e) =>
        {
            dialog.Close();
            ResumeShift(state);
        };
        freshBtn.Click += (s, e) =>
        {
            dialog.Close();
            SaveShiftStopTime(TimeSpan.Zero);
            ClearActiveShiftState();
        };

        btnPanel.Children.Add(resumeBtn);
        btnPanel.Children.Add(freshBtn);
        panel.Children.Add(btnPanel);
        dialog.Content = panel;
        dialog.Show(this);
    }

    private void ResumeShift(ActiveShiftState state)
    {
        _currentModel = state.Model;
        _currentModerator = state.Moderator;
        _totalTime = TimeSpan.FromMinutes(state.DurationHours * 60 + state.DurationMinutes);
        _elapsed = TimeSpan.FromSeconds(state.ElapsedSeconds);
        _isRunning = true;
        _isPaused = false;
        
        var remainingOnResume = _totalTime - _elapsed;
        if (remainingOnResume.TotalSeconds <= 0)
        {
            _notifiedComplete = true;
            _notified5Min = false;
            _notified15Min = false;
        }
        else if (remainingOnResume.TotalSeconds >= 299 && remainingOnResume.TotalSeconds <= 301)
        {
            _notified5Min = true;
            _notified15Min = false;
            _notifiedComplete = false;
        }
        else if (remainingOnResume.TotalSeconds >= 899 && remainingOnResume.TotalSeconds <= 901)
        {
            _notified15Min = true;
            _notified5Min = false;
            _notifiedComplete = false;
        }
        else
        {
            _notifiedComplete = false;
            _notified5Min = false;
            _notified15Min = false;
        }
        
        _timer?.Start();
        UpdateButtonStates();
        UpdateShiftStatus();
        UpdateDisplay();
        ShowStatusImage("show.png", 390, 280);
    }

}
