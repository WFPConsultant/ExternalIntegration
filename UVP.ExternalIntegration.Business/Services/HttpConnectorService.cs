using Polly;
using Serilog;
using System.Text;
using UVP.ExternalIntegration.Business.Interfaces;

namespace UVP.ExternalIntegration.Business.Services
{
    public class HttpConnectorService : IHttpConnectorService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger = Log.ForContext<HttpConnectorService>();

        public HttpConnectorService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Convenience overload
        public async Task<HttpResponseDto> SendRequestAsync(
            string url,
            string method,
            string? payload,
            int timeoutSeconds,
            IAsyncPolicy<HttpResponseMessage>? retryPolicy = null)
        {
            var request = new HttpRequestDto
            {
                Url = url,
                Method = method,
                Payload = payload,
                TimeoutSeconds = timeoutSeconds
            };

            return await SendRequestAsync(request, retryPolicy);
        }

        // Main execution path (per-call retry policy is accepted here)
        public async Task<HttpResponseDto> SendRequestAsync(
            HttpRequestDto request,
            IAsyncPolicy<HttpResponseMessage>? retryPolicy = null)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // (kept) Mock base override via env var
                //var baseUrlOverride = Environment.GetEnvironmentVariable("CMTS_BASEURL");

                //if (!string.IsNullOrWhiteSpace(baseUrlOverride))
                //{
                //    if (Uri.TryCreate(request.Url, UriKind.Absolute, out var abs))
                //    {
                //        var mockBase = new Uri(baseUrlOverride, UriKind.Absolute);
                //        var final = new Uri(mockBase, abs.PathAndQuery + abs.Fragment);
                //        request.Url = final.ToString(); // e.g., http://localhost:9091/un/cmts/clearance/v1
                //    }
                //    else
                //    {
                //        request.Url = new Uri(new Uri(baseUrlOverride, UriKind.Absolute), request.Url).ToString();
                //    }
                //}
                // END kept
                _logger.Information("Sending {Method} request to {Url}", request.Method, request.Url);

                if (!string.IsNullOrEmpty(request.Payload))
                {
                    _logger.Debug("Request payload: {Payload}", request.Payload);
                }

                // Use the provided per-call policy, or a NoOp (i.e., no retry)
                var policy = retryPolicy ?? Policy.NoOpAsync<HttpResponseMessage>();

                var response = await policy.ExecuteAsync(async () =>
                {
                    using var perAttemptCts = request.TimeoutSeconds > 0
                        ? new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds))
                        : new CancellationTokenSource();

                    using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

                    if (!string.IsNullOrEmpty(request.Payload))
                    {
                        // Determine content type - default to JSON, but detect form-urlencoded
                        string contentType = "application/json";

                        // Check if payload is form-urlencoded format (contains grant_type= or starts with key=value pairs)
                        if (request.Payload.Contains("grant_type=") ||
                            (request.Payload.Contains("=") && request.Payload.Contains("&") && !request.Payload.TrimStart().StartsWith("{")))
                        {
                            contentType = "application/x-www-form-urlencoded";
                            _logger.Debug("Detected form-urlencoded payload format");
                        }

                        // Check if Content-Type is explicitly specified in headers
                        if (request.Headers != null && request.Headers.ContainsKey("Content-Type"))
                        {
                            contentType = request.Headers["Content-Type"];
                            _logger.Debug("Using explicit Content-Type from headers: {ContentType}", contentType);
                        }

                        httpRequest.Content = new StringContent(request.Payload, Encoding.UTF8, contentType);
                    }

                    if (request.Headers != null)
                    {
                        foreach (var header in request.Headers)
                        {
                            // Skip Content-Type header as it's already set on the content
                            if (header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                                continue;

                            // Add request headers (tolerant)
                            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    return await _httpClient.SendAsync(
                        httpRequest,
                        HttpCompletionOption.ResponseHeadersRead,
                        perAttemptCts.Token);
                });

                var responseBody = await response.Content.ReadAsStringAsync();
                stopwatch.Stop();

                _logger.Information("Received {StatusCode} response in {ElapsedMs}ms from {Url}",
                    response.StatusCode, stopwatch.ElapsedMilliseconds, request.Url);

                if (!string.IsNullOrEmpty(responseBody))
                {
                    _logger.Debug("Response body: {ResponseBody}", responseBody);
                }

                return new HttpResponseDto
                {
                    StatusCode = (int)response.StatusCode,
                    Body = responseBody,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    IsSuccess = response.IsSuccessStatusCode
                };
            }
            catch (TaskCanceledException tce)
            {
                stopwatch.Stop();
                _logger.Error(tce, "Request timeout/canceled after {TimeoutSeconds}s for {Url}",
                    request.TimeoutSeconds, request.Url);

                return new HttpResponseDto
                {
                    StatusCode = 408,
                    Body = null,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    IsSuccess = false,
                    ErrorMessage = $"Request canceled or timed out after {request.TimeoutSeconds} seconds"
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.Error(ex, "Error sending HTTP request to {Url}", request.Url);

                return new HttpResponseDto
                {
                    StatusCode = 0,
                    Body = null,
                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                    IsSuccess = false,
                    ErrorMessage = ex.Message
                };
            }
        }
    }
}

//using Polly;
//using Serilog;
//using System.Text;
//using UVP.ExternalIntegration.Business.Interfaces;

//namespace UVP.ExternalIntegration.Business.Services
//{
//    public class HttpConnectorService : IHttpConnectorService
//    {
//        private readonly HttpClient _httpClient;
//        private readonly ILogger _logger = Log.ForContext<HttpConnectorService>();

//        public HttpConnectorService(HttpClient httpClient)
//        {
//            _httpClient = httpClient;
//        }

//        // Convenience overload
//        public async Task<HttpResponseDto> SendRequestAsync(
//            string url,
//            string method,
//            string? payload,
//            int timeoutSeconds,
//            IAsyncPolicy<HttpResponseMessage>? retryPolicy = null)
//        {
//            var request = new HttpRequestDto
//            {
//                Url = url,
//                Method = method,
//                Payload = payload,
//                TimeoutSeconds = timeoutSeconds
//            };

//            return await SendRequestAsync(request, retryPolicy);
//        }

//        // Main execution path (per-call retry policy is accepted here)
//        public async Task<HttpResponseDto> SendRequestAsync(
//            HttpRequestDto request,
//            IAsyncPolicy<HttpResponseMessage>? retryPolicy = null)
//        {
//            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

//            try
//            {
//                // (kept) Mock base override via env var
//                var baseUrlOverride = Environment.GetEnvironmentVariable("CMTS_BASEURL");

//                if (!string.IsNullOrWhiteSpace(baseUrlOverride))
//                {
//                    if (Uri.TryCreate(request.Url, UriKind.Absolute, out var abs))
//                    {
//                        var mockBase = new Uri(baseUrlOverride, UriKind.Absolute);
//                        var final = new Uri(mockBase, abs.PathAndQuery + abs.Fragment);
//                        request.Url = final.ToString(); // e.g., http://localhost:9091/un/cmts/clearance/v1
//                    }
//                    else
//                    {
//                        request.Url = new Uri(new Uri(baseUrlOverride, UriKind.Absolute), request.Url).ToString();
//                    }
//                }
//                // END kept
//                _logger.Information("Sending {Method} request to {Url}", request.Method, request.Url);

//                if (!string.IsNullOrEmpty(request.Payload))
//                {
//                    _logger.Debug("Request payload: {Payload}", request.Payload);
//                }

//                // Use the provided per-call policy, or a NoOp (i.e., no retry)
//                var policy = retryPolicy ?? Policy.NoOpAsync<HttpResponseMessage>();

//                var response = await policy.ExecuteAsync(async () =>
//                {
//                    using var perAttemptCts = request.TimeoutSeconds > 0
//                        ? new CancellationTokenSource(TimeSpan.FromSeconds(request.TimeoutSeconds))
//                        : new CancellationTokenSource();

//                    using var httpRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Url);

//                    if (!string.IsNullOrEmpty(request.Payload))
//                        httpRequest.Content = new StringContent(request.Payload, Encoding.UTF8, "application/json");

//                    if (request.Headers != null)
//                    {
//                        foreach (var header in request.Headers)
//                        {
//                            // add request headers (tolerant)
//                            httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
//                        }
//                    }

//                    return await _httpClient.SendAsync(
//                        httpRequest,
//                        HttpCompletionOption.ResponseHeadersRead,
//                        perAttemptCts.Token);
//                });

//                var responseBody = await response.Content.ReadAsStringAsync();
//                stopwatch.Stop();

//                _logger.Information("Received {StatusCode} response in {ElapsedMs}ms from {Url}",
//                    response.StatusCode, stopwatch.ElapsedMilliseconds, request.Url);

//                if (!string.IsNullOrEmpty(responseBody))
//                {
//                    _logger.Debug("Response body: {ResponseBody}", responseBody);
//                }

//                return new HttpResponseDto
//                {
//                    StatusCode = (int)response.StatusCode,
//                    Body = responseBody,
//                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
//                    IsSuccess = response.IsSuccessStatusCode
//                };
//            }
//            catch (TaskCanceledException tce)
//            {
//                stopwatch.Stop();
//                _logger.Error(tce, "Request timeout/canceled after {TimeoutSeconds}s for {Url}",
//                    request.TimeoutSeconds, request.Url);

//                return new HttpResponseDto
//                {
//                    StatusCode = 408,
//                    Body = null,
//                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
//                    IsSuccess = false,
//                    ErrorMessage = $"Request canceled or timed out after {request.TimeoutSeconds} seconds"
//                };
//            }
//            catch (Exception ex)
//            {
//                stopwatch.Stop();
//                _logger.Error(ex, "Error sending HTTP request to {Url}", request.Url);

//                return new HttpResponseDto
//                {
//                    StatusCode = 0,
//                    Body = null,
//                    ResponseTimeMs = stopwatch.ElapsedMilliseconds,
//                    IsSuccess = false,
//                    ErrorMessage = ex.Message
//                };
//            }
//        }
//    }
//}

