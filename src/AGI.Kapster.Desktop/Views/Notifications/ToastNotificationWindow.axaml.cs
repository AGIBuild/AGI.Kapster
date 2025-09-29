namespace AGI.Kapster.Desktop.Views.Notifications;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Serilog;
using AGI.Kapster.Desktop.Services;

public partial class ToastNotificationWindow : Window
{
    private const int MarginFromScreen = 20;

    public ToastNotificationWindow()
    {
        InitializeComponent();

        TransparencyBackgroundFallback = Brushes.Transparent;
        ShowActivated = false;
        Opacity = 0;

        Opened += (_, _) =>
        {
            if (!IsPositionManaged)
            {
                PositionAbove(MarginFromScreen);
            }
            Opacity = 1;
        };

        CloseButton.Click += (_, _) => Close();
    }

    internal bool IsPositionManaged { get; set; }

    public void SetNotification(NotificationRequest request, CancellationToken cancellationToken)
    {
        TitleText.Text = request.Title;
        MessageText.Text = request.Message;

        if (!string.IsNullOrWhiteSpace(request.IconPath) && File.Exists(request.IconPath))
        {
            try
            {
                IconImage.Source = new Bitmap(request.IconPath);
                IconImage.IsVisible = true;
            }
            catch
            {
                IconImage.IsVisible = false;
            }
        }
        else
        {
            IconImage.IsVisible = false;
        }

        if (request.Buttons is { Count: > 0 })
        {
            ConfigureButtons(request.Buttons, cancellationToken);
        }
        else
        {
            ButtonsPanel.IsVisible = false;
            ButtonsPanel.Children.Clear();
        }
    }

    public void ConfigureButtons(IReadOnlyList<NotificationButton> buttons, CancellationToken cancellationToken)
    {
        ButtonsPanel.Children.Clear();

        foreach (var button in buttons)
        {
            var actionButton = new Button
            {
                Content = button.Title,
                MinWidth = 80,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 0)
            };

            actionButton.Click += async (_, _) =>
            {
                try
                {
                    if (button.Callback != null)
                    {
                        await button.Callback.Invoke(cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Notification button callback failed: {ButtonId}", button.Id);
                }
                finally
                {
                    Close();
                }
            };

            ButtonsPanel.Children.Add(actionButton);
        }

        ButtonsPanel.IsVisible = ButtonsPanel.Children.Count > 0;
    }

    internal void PositionAbove(int offsetFromTop)
    {
        var workingArea = GetWorkingArea();
        var width = (int)Math.Round(Bounds.Width);

        var x = workingArea.X + workingArea.Width - width - MarginFromScreen;
        var y = workingArea.Y + offsetFromTop;

        Position = new PixelPoint(
            Math.Max(workingArea.X + MarginFromScreen, x),
            Math.Max(workingArea.Y + MarginFromScreen, y));
    }

    private PixelRect GetWorkingArea()
    {
        PixelRect? workingArea = null;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktopLifetime && desktopLifetime.MainWindow != null)
        {
            workingArea = desktopLifetime.MainWindow.Screens?
                .ScreenFromVisual(desktopLifetime.MainWindow)?.WorkingArea;
        }

        workingArea ??= Screens?.ScreenFromVisual(this)?.WorkingArea;
        workingArea ??= Screens?.Primary?.WorkingArea;

        return workingArea ?? new PixelRect(0, 0, 1920, 1080);
    }
}
