namespace AGI.Kapster.Desktop.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using AGI.Kapster.Desktop.Views.Notifications;
using System.Collections.Generic;

internal sealed class NotificationService : INotificationService
{
    private static readonly TimeSpan DefaultDisplayDuration = TimeSpan.FromSeconds(8);
    private const int VerticalSpacing = 12;
    private const int MarginFromTop = 20;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly List<ToastNotificationWindow> _activeWindows = new();

    public NotificationService(IServiceProvider serviceProvider)
    {
    }

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public async Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var shouldAutoDismiss = request.IsAutoDismissEnabled;
        var displayDuration = request.DisplayDuration ?? DefaultDisplayDuration;

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            ToastNotificationWindow? window = null;

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                window = new ToastNotificationWindow
                {
                    IsPositionManaged = true
                };
                window.SetNotification(request, cancellationToken);
                window.Closed += OnWindowClosed;
                _activeWindows.Add(window);
                RepositionWindows();
                window.Show();
                Dispatcher.UIThread.Post(RepositionWindows, DispatcherPriority.Background);
            });

            if (shouldAutoDismiss && window != null)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(displayDuration, cancellationToken).ConfigureAwait(false);
                        await Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (window?.IsVisible == true)
                            {
                                window.Close();
                            }
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        // Ignore cancellation
                    }
                }, cancellationToken);
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        if (sender is not ToastNotificationWindow window)
        {
            return;
        }

        window.Closed -= OnWindowClosed;

        _ = Dispatcher.UIThread.InvokeAsync(() =>
        {
            _activeWindows.Remove(window);
            RepositionWindows();
        });
    }

    private void RepositionWindows()
    {
        var yOffset = MarginFromTop;

        foreach (var window in _activeWindows.ToArray())
        {
            if (!window.IsVisible)
            {
                continue;
            }

            window.PositionAbove(yOffset);
            yOffset += (int)window.Bounds.Height + VerticalSpacing;
        }
    }

    public ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        return ValueTask.CompletedTask;
    }
}
