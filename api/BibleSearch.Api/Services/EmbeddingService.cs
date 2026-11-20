
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BibleSearch.Api.Services
{
    public class EmbeddingService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;

        private const int MaxAttempts = 5;
        private const int ExpectedDimensions = 1536;

        private static readonly TimeSpan RequestInterval =
            TimeSpan.FromMilliseconds(250);

        private static readonly SemaphoreSlim RequestLock =
            new SemaphoreSlim(1, 1);

        private static DateTime _lastRequestTime =
            DateTime.MinValue;

        public EmbeddingService(
            HttpClient httpClient,
            IConfiguration configuration)
        {
            _httpClient = httpClient;
            _configuration = configuration;

            _httpClient.BaseAddress = new Uri(
                _configuration["OpenRouter:BaseUrl"]!);

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    _configuration["OpenRouter:ApiKey"]);
        }

        private async Task WaitForRequestSlotAsync()
        {
            await RequestLock.WaitAsync();

            try
            {
                var elapsed =
                    DateTime.UtcNow - _lastRequestTime;

                var remainingDelay =
                    RequestInterval - elapsed;

                if (remainingDelay > TimeSpan.Zero)
                {
                    await Task.Delay(remainingDelay);
                }

                _lastRequestTime = DateTime.UtcNow;
            }
            finally
            {
                RequestLock.Release();
            }
        }

        public async Task<List<float>> GenerateEmbeddingAsync(
            string text)
        {
            var request = new
            {
                model = _configuration["OpenRouter:Model"],
                input = text
            };

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                try
                {
                    await WaitForRequestSlotAsync();

                    using var response =
                        await _httpClient.PostAsJsonAsync(
                            "embeddings",
                            request);

                    if (response.IsSuccessStatusCode)
                    {
                        var responseContent =
                            await response.Content.ReadAsStringAsync();

                        using var jsonDocument =
                            JsonDocument.Parse(responseContent);

                        var embeddingArray =
                            jsonDocument.RootElement
                                .GetProperty("data")[0]
                                .GetProperty("embedding");

                        var embedding = embeddingArray
                            .EnumerateArray()
                            .Select(value => value.GetSingle())
                            .ToList();

                        if (embedding.Count != ExpectedDimensions)
                        {
                            throw new Exception(
                                $"Invalid embedding dimensions. " +
                                $"Expected {ExpectedDimensions}, " +
                                $"received {embedding.Count}.");
                        }

                        return embedding;
                    }

                    bool shouldRetry =
                        response.StatusCode ==
                            HttpStatusCode.TooManyRequests ||
                        response.StatusCode ==
                            HttpStatusCode.InternalServerError ||
                        response.StatusCode ==
                            HttpStatusCode.BadGateway ||
                        response.StatusCode ==
                            HttpStatusCode.ServiceUnavailable ||
                        response.StatusCode ==
                            HttpStatusCode.GatewayTimeout;

                    var errorContent =
                        await response.Content.ReadAsStringAsync();

                    if (!shouldRetry || attempt == MaxAttempts)
                    {
                        throw new Exception(
                            $"OpenRouter API error: " +
                            $"{(int)response.StatusCode}. " +
                            errorContent);
                    }

                    var retryAfter =
                        response.Headers.RetryAfter;

                    TimeSpan delay;

                    if (retryAfter?.Delta != null)
                    {
                        delay = retryAfter.Delta.Value;
                    }
                    else if (retryAfter?.Date != null)
                    {
                        delay = retryAfter.Date.Value -
                                DateTimeOffset.UtcNow;
                    }
                    else
                    {
                        delay = TimeSpan.FromSeconds(
                            Math.Pow(2, attempt));
                    }

                    if (delay < TimeSpan.FromSeconds(1))
                    {
                        delay = TimeSpan.FromSeconds(1);
                    }

                    Console.WriteLine(
                        $"OpenRouter returned " +
                        $"{(int)response.StatusCode}. " +
                        $"Attempt {attempt}/{MaxAttempts}. " +
                        $"Retrying in {delay.TotalSeconds:F1} seconds.");

                    await Task.Delay(delay);
                }
                catch (HttpRequestException ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        throw new Exception(
                            "OpenRouter connection failed after " +
                            $"{MaxAttempts} attempts.",
                            ex);
                    }

                    var delay = TimeSpan.FromSeconds(
                        Math.Pow(2, attempt));

                    Console.WriteLine(
                        $"Network error. Attempt {attempt}/" +
                        $"{MaxAttempts}. Retrying in " +
                        $"{delay.TotalSeconds:F1} seconds.");

                    await Task.Delay(delay);
                }
                catch (TaskCanceledException ex)
                {
                    if (attempt == MaxAttempts)
                    {
                        throw new Exception(
                            "OpenRouter request timed out after " +
                            $"{MaxAttempts} attempts.",
                            ex);
                    }

                    var delay = TimeSpan.FromSeconds(
                        Math.Pow(2, attempt));

                    Console.WriteLine(
                        $"Request timed out. Attempt {attempt}/" +
                        $"{MaxAttempts}. Retrying in " +
                        $"{delay.TotalSeconds:F1} seconds.");

                    await Task.Delay(delay);
                }
            }

            throw new Exception(
                "Could not generate embedding.");
        }
    }
}
