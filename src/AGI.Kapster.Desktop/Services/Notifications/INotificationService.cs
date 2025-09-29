namespace AGI.Kapster.Desktop.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public interface INotificationService : IAsyncDisposable
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task ShowAsync(NotificationRequest request, CancellationToken cancellationToken = default);
}
