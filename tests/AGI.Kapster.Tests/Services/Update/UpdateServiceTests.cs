using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using FluentAssertions;
using NSubstitute;
using AGI.Kapster.Desktop.Services.Settings;
using AGI.Kapster.Desktop.Services.Update;
using AGI.Kapster.Desktop.Models.Update;
using AGI.Kapster.Tests.TestHelpers;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace AGI.Kapster.Tests.Services.Update;

public class UpdateServiceTests : IDisposable
{
    private readonly ISettingsService _mockSettingsService;
    private readonly TestHttpMessageHandler _httpHandler;
    private readonly UpdateService _updateService;

    public UpdateServiceTests()
    {
        _mockSettingsService = Substitute.For<ISettingsService>();
        var settings = TestDataFactory.CreateDefaultAppSettings();
        _mockSettingsService.Settings.Returns(settings);

        _httpHandler = new TestHttpMessageHandler();
        _httpHandler.SetResponse(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(TestDataFactory.CreateGitHubReleasesResponse())
        });

        _updateService = CreateService();
    }

    private UpdateService CreateService(TimeSpan? retryInterval = null)
    {
        var httpClient = new HttpClient(_httpHandler, disposeHandler: false);
        return new UpdateService(_mockSettingsService, retryInterval, httpClient);
    }

    private static UpdateService CreateService(ISettingsService settingsService, TimeSpan? retryInterval = null, HttpClient? httpClient = null, IFileSystemService? fileSystemService = null)
    {
        return new UpdateService(settingsService, retryInterval ?? TimeSpan.FromMilliseconds(10), httpClient, fileSystemService);
    }

    [Fact]
    public void Constructor_ShouldInitializeCorrectly()
    {
        var service = CreateService();
        service.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckForUpdatesAsync_ShouldCompleteWithoutError()
    {
        Func<Task> act = async () => await _updateService.CheckForUpdatesAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void StartBackgroundChecking_ShouldNotThrow()
    {
        Action act = () => _updateService.StartBackgroundChecking();
        act.Should().NotThrow();
    }

    [Fact]
    public void StopBackgroundChecking_ShouldNotThrow()
    {
        _updateService.StartBackgroundChecking();
        Action act = () => _updateService.StopBackgroundChecking();
        act.Should().NotThrow();
    }

    [Fact]
    public async Task UpdateSettingsAsync_ShouldUpdateCoreFlags()
    {
        var newSettings = new UpdateSettings
        {
            Enabled = false,
            NotifyBeforeInstall = true,
            UsePreReleases = true,
            RepositoryOwner = "Custom",
            RepositoryName = "Repo"
        };

        await _updateService.UpdateSettingsAsync(newSettings);

        var currentSettings = _updateService.GetSettings();
        currentSettings.Enabled.Should().BeFalse();
        currentSettings.NotifyBeforeInstall.Should().BeTrue();
        currentSettings.UsePreReleases.Should().BeTrue();
        currentSettings.RepositoryOwner.Should().Be("Custom");
        currentSettings.RepositoryName.Should().Be("Repo");
    }

    [Fact]
    public void IsAutoUpdateEnabled_ShouldRespectSettings()
    {
        var settings = TestDataFactory.CreateDefaultAppSettings();
        settings.AutoUpdate!.Enabled = false;
        _mockSettingsService.Settings.Returns(settings);

        var service = CreateService();
        service.IsAutoUpdateEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadUpdateAsync_OnFailure_ShouldScheduleRetry()
    {
        var retryInterval = TimeSpan.FromMilliseconds(50);
        using var service = CreateService(retryInterval);

        var updateInfo = TestDataFactory.CreateUpdateInfo();

        _httpHandler.SetResponse(new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        var result = await service.DownloadUpdateAsync(updateInfo);
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DownloadRetryPolicy_ShouldInvokeOnSubsequentAttempt()
    {
        var retryInterval = TimeSpan.FromMilliseconds(10);

        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(TestDataFactory.CreateDefaultAppSettings());

        var fileSystemService = new MemoryFileSystemService();
        var httpHandler = new TestHttpMessageHandler();
        
        var attempts = 0;
        httpHandler.SetResponseFactory(() =>
        {
            attempts++;
            if (attempts == 1)
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
            
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
            content.Headers.ContentLength = 4;
            response.Content = content;
            return response;
        });

        using var httpClient = new HttpClient(httpHandler);
        using var service = CreateService(settingsService, retryInterval, httpClient, fileSystemService);

        var updateInfo = TestDataFactory.CreateUpdateInfo(fileSize: 4);

        var stopwatch = Stopwatch.StartNew();
        var result = await service.DownloadUpdateAsync(updateInfo);
        stopwatch.Stop();

        result.Should().BeTrue("Expected retry to succeed after first failure");
        attempts.Should().Be(2);
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(retryInterval);

        service.ClearPendingInstaller();
    }

    [Fact]
    public async Task DownloadUpdateAsync_OnSuccess_ShouldPersistInstaller()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(TestDataFactory.CreateDefaultAppSettings());

        var fileSystemService = new MemoryFileSystemService();
        var httpHandler = new TestHttpMessageHandler();
        var bytes = new byte[] { 0x5A, 0x5A, 0x5A, 0x5A };
        
        httpHandler.SetResponseFactory(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentLength = bytes.Length;
            response.Content = content;
            return response;
        });

        using var httpClient = new HttpClient(httpHandler);
        using var service = CreateService(settingsService, httpClient: httpClient, fileSystemService: fileSystemService);
        var updateInfo = TestDataFactory.CreateUpdateInfo(fileSize: 4);

        var result = await service.DownloadUpdateAsync(updateInfo);

        result.Should().BeTrue();
        var path = service.PendingInstallerPath;
        path.Should().NotBeNull();

        service.ClearPendingInstaller();
    }


    [Fact]
    public async Task DownloadUpdateAsync_WithSizeMismatch_ShouldDeleteFile()
    {
        var settingsService = Substitute.For<ISettingsService>();
        settingsService.Settings.Returns(TestDataFactory.CreateDefaultAppSettings());

        var fileSystemService = new MemoryFileSystemService();
        var httpHandler = new TestHttpMessageHandler();
        var bytes = new byte[] { 0x42, 0x42, 0x42, 0x42 };
        
        httpHandler.SetResponseFactory(() =>
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            };
        });

        using var httpClient = new HttpClient(httpHandler);
        using var service = CreateService(settingsService, httpClient: httpClient, fileSystemService: fileSystemService);
        var updateInfo = TestDataFactory.CreateUpdateInfo(fileSize: bytes.Length + 10);

        var result = await service.DownloadUpdateAsync(updateInfo);

        result.Should().BeFalse();
        service.PendingInstallerPath.Should().BeNull();
    }

    public void Dispose()
    {
        var pending = _updateService.PendingInstallerPath;
        if (pending != null && File.Exists(pending))
        {
            File.Delete(pending);
        }

        _updateService.Dispose();
    }
}