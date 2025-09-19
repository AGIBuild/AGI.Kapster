using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AGI.Captor.Tests.TestHelpers;

/// <summary>
/// Mock HTTP message handler for testing HTTP clients
/// </summary>
public class MockHttpMessageHandler : HttpMessageHandler
{
    private string? _responseContent;
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private Exception? _exception;

    public void SetResponse(string content, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        _responseContent = content;
        _statusCode = statusCode;
        _exception = null;
    }

    public void SetException(Exception exception)
    {
        _exception = exception;
        _responseContent = null;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (_exception != null)
        {
            throw _exception;
        }

        var response = new HttpResponseMessage(_statusCode);

        if (_responseContent != null)
        {
            response.Content = new StringContent(_responseContent);
        }

        // Simulate async operation
        await Task.Delay(1, cancellationToken);

        return response;
    }
}