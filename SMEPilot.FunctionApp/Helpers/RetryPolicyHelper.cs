using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Graph.Models.ODataErrors;
using Polly;
using Polly.Retry;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Helpers
{
    /// <summary>
    /// Helper for creating retry policies for Graph API calls
    /// Handles transient failures (429, 503, network errors)
    /// </summary>
    public static class RetryPolicyHelper
    {
        /// <summary>
        /// Creates a retry policy for Graph API operations
        /// Retries on transient errors: 429 (Too Many Requests), 503 (Service Unavailable), network errors
        /// </summary>
        public static AsyncRetryPolicy CreateGraphRetryPolicy(Config config, ILogger? logger = null)
        {
            return Policy
                .Handle<ODataError>(ex => IsTransientError(ex))
                .Or<HttpRequestException>()
                .Or<TaskCanceledException>() // Timeout
                .WaitAndRetryAsync(
                    retryCount: config.MaxRetryAttempts,
                    sleepDurationProvider: retryAttempt =>
                    {
                        // Exponential backoff with jitter
                        var baseDelay = TimeSpan.FromSeconds(config.RetryDelaySeconds * Math.Pow(2, retryAttempt - 1));
                        var maxDelay = TimeSpan.FromSeconds(config.MaxRetryDelaySeconds);
                        var delay = baseDelay > maxDelay ? maxDelay : baseDelay;
                        
                        // Add jitter (±20%)
                        var jitter = TimeSpan.FromMilliseconds(new Random().Next(0, (int)(delay.TotalMilliseconds * 0.2)));
                        var finalDelay = delay + jitter;
                        
                        logger?.LogWarning("⚠️ [RETRY] Transient error detected. Retrying in {Delay}ms (attempt {Attempt}/{MaxAttempts})", 
                            finalDelay.TotalMilliseconds, retryAttempt, config.MaxRetryAttempts);
                        
                        return finalDelay;
                    },
                    onRetry: (exception, timeSpan, retryCount, context) =>
                    {
                        if (exception is ODataError odataError)
                        {
                            logger?.LogWarning(odataError, 
                                "⚠️ [RETRY] Graph API error (attempt {RetryCount}/{MaxRetries}): Code={Code}, Message={Message}. Retrying in {Delay}ms",
                                retryCount, config.MaxRetryAttempts, odataError.Error?.Code, odataError.Error?.Message, timeSpan.TotalMilliseconds);
                        }
                        else
                        {
                            logger?.LogWarning(exception, 
                                "⚠️ [RETRY] Transient error (attempt {RetryCount}/{MaxRetries}): {ErrorType}. Retrying in {Delay}ms",
                                retryCount, config.MaxRetryAttempts, exception.GetType().Name, timeSpan.TotalMilliseconds);
                        }
                    });
        }

        /// <summary>
        /// Determines if an ODataError is a transient error that should be retried
        /// </summary>
        private static bool IsTransientError(ODataError error)
        {
            if (error?.Error == null) return false;

            var code = error.Error.Code ?? "";
            var message = error.Error.Message ?? "";

            // Rate limiting (429)
            if (code.Contains("429") || code.Contains("TooManyRequests") || message.Contains("429"))
            {
                return true;
            }

            // Service unavailable (503)
            if (code.Contains("503") || code.Contains("ServiceUnavailable") || message.Contains("503"))
            {
                return true;
            }

            // Timeout errors
            if (code.Contains("Timeout") || message.Contains("timeout", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Throttling
            if (code.Contains("Throttled") || message.Contains("throttl", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // General server errors (5xx) - might be transient
            if (code.Contains("5") && (code.Contains("00") || code.Contains("02") || code.Contains("04")))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Executes an async operation with retry policy
        /// </summary>
        public static async Task<T> ExecuteWithRetryAsync<T>(
            AsyncRetryPolicy policy,
            Func<Task<T>> operation,
            string operationName,
            ILogger? logger = null)
        {
            try
            {
                return await policy.ExecuteAsync(async () =>
                {
                    logger?.LogDebug("🔄 [RETRY] Executing {OperationName}", operationName);
                    return await operation();
                });
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ [RETRY] {OperationName} failed after all retry attempts", operationName);
                throw;
            }
        }
    }
}

