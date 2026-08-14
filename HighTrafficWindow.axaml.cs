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

    private string _dataFilePath = string.Empty;
    private List<CrmEntry> _records = new();
    private int _nextId = 1;
    private int _editingId = 0;

    public HighTrafficWindow()
    {
        InitializeComponent();
        _dataFilePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "crm_data.json");
        LoadRecords();
        LoadModelsList();
        RefreshTable();
        ApplyTheme();
        SiteFilterComboBox.SelectionChanged += SiteFilterComboBox_SelectionChanged;
    }

    private void LoadModelsList()
    {
        var shiftDataPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shift_data.json");
        var data = JsonStore.Load<ShiftDataFile>(shiftDataPath);
        if (data?.Models == null) return;
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

        var data = JsonStore.Load<CrmDataFile>(_dataFilePath);
        if (data?.Records == null) return;
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

    private void SaveRecords()
    {
        var data = new CrmDataFile { Records = _records };
        JsonStore.Save(_dataFilePath, data);
    }

    private void RefreshTable()
    {
        TableGrid.Children.Clear();
        TableGrid.RowDefinitions.Clear();

        var siteFilter = SiteFilterComboBox.SelectedItem as ComboBoxItem;
        var filterText = siteFilter?.Content?.ToString() ?? "All";
        var filtered = _records.Where(r => filterText == "All" || r.Site.Equals(filterText, StringComparison.OrdinalIgnoreCase)).ToList();

        if (!filtered.Any())
        {
            NoDataText.IsVisible = true;
            return;
        }
        NoDataText.IsVisible = false;

        AddHeaderRow();

        int rowIndex = 1;
        foreach (var item in filtered)
        {
            AddDataRow(item, rowIndex++);
        }
    }

    private void AddHeaderRow()
    {
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var headers = new[] { "User", "Model", "Site", "Spend", "Habits", "Triggers", "Notes", "Action" };
        for (int col = 0; col < headers.Length; col++)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FF3E3E42")),
                BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
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

    private void AddDataRow(CrmEntry item, int rowIndex)
    {
        TableGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var bgBrush = rowIndex % 2 == 0
            ? new SolidColorBrush(Color.Parse("#FF2D2D30"))
            : new SolidColorBrush(Color.Parse("#FF252526"));

        var cells = new[] { item.User, item.Model, item.Site, SpendTierLabels[Math.Clamp(item.SpendTier, 0, SpendTierLabels.Length - 1)], item.Habits, item.Triggers, item.Notes };

        for (int col = 0; col < cells.Length; col++)
        {
            var border = new Border
            {
                Background = bgBrush,
                BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(6)
            };

            if (col == 6)
            {
                var text = new SelectableTextBlock
                {
                    Text = cells[col] ?? string.Empty,
                    Foreground = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
                    FontSize = 12,
                    TextTrimming = TextTrimming.CharacterEllipsis
                };
                border.Child = text;
                
                border.PointerEntered += (s, e) => border.Cursor = new Cursor(StandardCursorType.Hand);
                border.PointerExited += (s, e) => border.Cursor = Cursor.Default;
                border.PointerPressed += (s, e) =>
                {
                    if (!string.IsNullOrEmpty(item.Notes))
                    {
                        ShowNotesPopup(item.Notes);
                    }
                };
            }
            else if (col == 0)
            {
                var textBox = new TextBox
                {
                    Text = cells[col] ?? string.Empty,
                    Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
                    FontSize = 12,
                    IsReadOnly = true,
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0)
                };
                border.Child = textBox;
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

        // Action column with Edit and Delete buttons
        var actionBorder = new Border
        {
            Background = bgBrush,
            BorderBrush = new SolidColorBrush(Color.Parse("#FF555555")),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(4)
        };
        var actionGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto"), ColumnSpacing = 8, Width = 120 };
        
        var editBtn = new Button
        {
            Content = "Edit",
            Tag = item.Id,
            Background = new SolidColorBrush(Color.Parse("#FFcba6f7")),
            Foreground = new SolidColorBrush(Color.Parse("#FF000000")),
            Padding = new Thickness(6, 2),
            Height = 25
        };
        editBtn.Click += EditButton_Click;
        Grid.SetColumn(editBtn, 0);
        actionGrid.Children.Add(editBtn);
        
        var deleteBtn = new Button
        {
            Content = "Delete",
            Tag = item.Id,
            Background = new SolidColorBrush(Color.Parse("#FFFF0000")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            Padding = new Thickness(6, 2),
            Height = 25
        };
        deleteBtn.Click += DeleteButton_Click;
        Grid.SetColumn(deleteBtn, 1);
        actionGrid.Children.Add(deleteBtn);
        
        actionBorder.Child = actionGrid;
        Grid.SetRow(actionBorder, rowIndex);
        Grid.SetColumn(actionBorder, 7);
        TableGrid.Children.Add(actionBorder);
    }

    private void EditButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        var item = _records.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

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

    private void DeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not int id) return;
        var item = _records.FirstOrDefault(r => r.Id == id);
        if (item == null) return;

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
            return;
        }

        var existing = _records.FirstOrDefault(r => r.User.Equals(user, StringComparison.OrdinalIgnoreCase) && r.Site.Equals(siteText, StringComparison.OrdinalIgnoreCase));
        if (existing != null)
        {
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
            return;
        }

        var existing = _records.FirstOrDefault(r => r.Id == _editingId);
        if (existing == null)
        {
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
        var isLight = false;
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
