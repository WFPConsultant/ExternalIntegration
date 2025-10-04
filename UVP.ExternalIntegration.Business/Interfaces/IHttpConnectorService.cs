using Polly;

namespace UVP.ExternalIntegration.Business.Interfaces
{
    public interface IHttpConnectorService
    {
        Task<HttpResponseDto> SendRequestAsync(
        string url,
        string method,
        string? payload,
        int timeoutSeconds,
        IAsyncPolicy<HttpResponseMessage>? retryPolicy = null);

        Task<HttpResponseDto> SendRequestAsync(
            HttpRequestDto request,
            IAsyncPolicy<HttpResponseMessage>? retryPolicy = null);
    }

    public class HttpResponseDto
    {
        public int StatusCode { get; set; }
        public string? Body { get; set; }
        public long ResponseTimeMs { get; set; }
        public bool IsSuccess { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class HttpRequestDto
    {
        public string Url { get; set; } = string.Empty;
        public string Method { get; set; } = "GET";
        public string? Payload { get; set; }
        public int TimeoutSeconds { get; set; } = 30;
        public Dictionary<string, string>? Headers { get; set; }
    }
}
