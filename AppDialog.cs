using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;

namespace ModelTimer;

/// <summary>
/// Shared modal dialog helpers. Every window used to hand-roll its own ~30-line ShowInfoDialog
/// method (six near-identical copies); this is the one place that styling now lives.
/// </summary>
internal static class AppDialog
{
    public static void ShowInfo(Window owner, string title, string message)
    {
        var dialog = BuildBase(title, out var panel);
        AddMessage(panel, message);

        var okBtn = new Button
        {
            Content = "OK",
            Width = 100,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#FFf9e2af")),
            Foreground = new SolidColorBrush(Color.Parse("#FF000000")),
            HorizontalAlignment = HorizontalAlignment.Center
        };
        okBtn.Click += (s, e) => dialog.Close();
        panel.Children.Add(okBtn);

        dialog.Content = panel;
        dialog.ShowDialog(owner);
    }

    /// <summary>Shows a Cancel/confirm dialog and returns true only if the confirm button was pressed.
    /// Used before any permanent, irreversible action (deleting a shift or fan record, etc.).</summary>
    public static async Task<bool> ShowConfirm(Window owner, string title, string message, string confirmLabel = "Delete")
    {
        var dialog = BuildBase(title, out var panel);
        AddMessage(panel, message);

        var result = false;
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Center };

        var cancelBtn = new Button
        {
            Content = "Cancel",
            Width = 100,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#FFa6e3a1")),
            Foreground = new SolidColorBrush(Color.Parse("#FF000000"))
        };
        cancelBtn.Click += (s, e) => { result = false; dialog.Close(); };

        var confirmBtn = new Button
        {
            Content = confirmLabel,
            Width = 100,
            Height = 30,
            Background = new SolidColorBrush(Color.Parse("#FFFF0000")),
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF"))
        };
        confirmBtn.Click += (s, e) => { result = true; dialog.Close(); };

        btnPanel.Children.Add(cancelBtn);
        btnPanel.Children.Add(confirmBtn);
        panel.Children.Add(btnPanel);

        dialog.Content = panel;
        await dialog.ShowDialog(owner);
        return result;
    }

    private static Window BuildBase(string title, out StackPanel panel)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false
        };

        panel = new StackPanel { Spacing = 15, Margin = new Thickness(20) };
        return dialog;
    }

    private static void AddMessage(StackPanel panel, string message)
    {
        panel.Children.Add(new TextBlock
        {
            Text = message,
            Foreground = new SolidColorBrush(Color.Parse("#FFFFFFFF")),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap
        });
    }
}
