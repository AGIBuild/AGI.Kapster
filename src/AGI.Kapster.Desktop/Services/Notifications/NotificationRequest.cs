namespace AGI.Kapster.Desktop.Services;

using System;
using System.Collections.Generic;

public sealed record NotificationRequest(
    string Title,
    string Message,
    string? IconPath = null,
    IReadOnlyList<NotificationButton>? Buttons = null,
    bool IsAutoDismissEnabled = false,
    TimeSpan? DisplayDuration = null
);
