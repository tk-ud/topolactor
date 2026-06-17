namespace Topolactor.Runtime;

public sealed class HttpExternalPortHttpClient : IExternalPortHttpClient
{
    private readonly HttpClient _httpClient;

    public HttpExternalPortHttpClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<ExternalPortHttpResponse> SendAsync(ExternalPortHttpRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        using var httpRequest = new HttpRequestMessage(request.Method, request.Endpoint);
        foreach (var (key, value) in request.Headers)
            httpRequest.Headers.TryAddWithoutValidation(key, value);
        if (request.Body is not null)
            httpRequest.Content = new StringContent(request.Body);

        var response = await _httpClient.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        return new ExternalPortHttpResponse((int)response.StatusCode, body);
    }
}
