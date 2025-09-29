using System;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Serilog;

namespace AGI.Kapster.Desktop.Dialogs;

public partial class AboutDialog : Window
{
    public AboutDialog()
    {
        InitializeComponent();
        SetupEventHandlers();
        LoadVersionInfo();

        // Handle window closing
        Closing += (_, _) => Hide();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupEventHandlers()
    {
        // Close button
        if (this.FindControl<Button>("CloseButton") is { } closeButton)
        {
            closeButton.Click += OnCloseClick;
        }

        // Handle Escape key
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape)
            {
                Close();
            }
        };
    }

    private void LoadVersionInfo()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;

            if (this.FindControl<TextBlock>("VersionText") is { } versionText)
            {
                var versionString = version != null
                    ? $"Version {version}" 
                    : "Version 1.0.0.0";

                versionText.Text = versionString;
                Log.Debug("About dialog loaded with version: {Version}", versionString);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to load version information for About dialog");

            // Fallback to default version
            if (this.FindControl<TextBlock>("VersionText") is { } versionText)
            {
                versionText.Text = "Version 1.0.0";
            }
        }
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    /// <summary>
    /// Show the About dialog as a modal dialog
    /// </summary>
    public static void ShowDialogWindow(Window? owner = null)
    {
        try
        {
            var dialog = new AboutDialog();

            if (owner != null)
            {
                _ = dialog.ShowDialog(owner);
            }
            else
            {
                dialog.Show();
                dialog.Activate();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to show About dialog");
        }
    }
}
