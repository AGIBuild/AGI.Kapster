using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace AGI.Kapster.Tests.TestHelpers;

public class TestHttpMessageHandler : HttpMessageHandler
{
    private HttpResponseMessage _response = new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent("{}")
    };
    private Func<HttpResponseMessage>? _responseFactory;
    private int _requestCount;
    private readonly List<HttpStatusCode> _statusHistory = new();

    public int RequestCount => Volatile.Read(ref _requestCount);
    public IReadOnlyList<HttpStatusCode> StatusHistory => _statusHistory;

    public void ResetRequestCount()
    {
        Interlocked.Exchange(ref _requestCount, 0);
        lock (_statusHistory)
        {
            _statusHistory.Clear();
        }
    }

    public void SetResponse(HttpResponseMessage response)
    {
        _response = response;
        _responseFactory = null;
    }

    public void SetResponseFactory(Func<HttpResponseMessage> factory)
    {
        _responseFactory = factory;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);

        var response = _responseFactory != null ? _responseFactory() : _response;
        
        // Ensure Content-Length is set for successful responses
        if (response.IsSuccessStatusCode && response.Content != null)
        {
            if (response.Content.Headers.ContentLength == null)
            {
                // For ByteArrayContent, set the length based on the actual content
                if (response.Content is ByteArrayContent)
                {
                    var contentTask = response.Content.ReadAsByteArrayAsync();
                    contentTask.Wait();
                    var contentLength = contentTask.Result.Length;
                    response.Content.Headers.ContentLength = contentLength;
                }
            }
        }
        
        lock (_statusHistory)
        {
            _statusHistory.Add(response.StatusCode);
        }

        return Task.FromResult(response);
    }
}
