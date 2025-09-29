namespace AGI.Kapster.Desktop.Services;

using System;
using System.Threading;
using System.Threading.Tasks;

public sealed record NotificationButton(
    string Id,
    string Title,
    Func<CancellationToken, Task>? Callback = null
);
