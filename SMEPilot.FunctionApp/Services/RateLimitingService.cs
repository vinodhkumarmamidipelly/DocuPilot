using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace SMEPilot.FunctionApp.Services
{
    /// <summary>
    /// Service for rate limiting to prevent abuse
    /// </summary>
    public class RateLimitingService
    {
        private readonly ConcurrentDictionary<string, RateLimitInfo> _rateLimitCache = new ConcurrentDictionary<string, RateLimitInfo>();
        private readonly int _maxRequestsPerMinute;
        private readonly int _maxRequestsPerHour;
        private readonly ILogger<RateLimitingService>? _logger;

        public RateLimitingService(int maxRequestsPerMinute = 60, int maxRequestsPerHour = 1000, ILogger<RateLimitingService>? logger = null)
        {
            _maxRequestsPerMinute = maxRequestsPerMinute;
            _maxRequestsPerHour = maxRequestsPerHour;
            _logger = logger;
        }

        /// <summary>
        /// Check if request should be rate limited
        /// </summary>
        public bool IsRateLimited(string identifier, out string? reason)
        {
            reason = null;
            var now = DateTimeOffset.UtcNow;
            
            var rateLimitInfo = _rateLimitCache.GetOrAdd(identifier, _ => new RateLimitInfo
            {
                Identifier = identifier,
                FirstRequestTime = now,
                RequestCount = 0,
                LastRequestTime = now
            });

            lock (rateLimitInfo)
            {
                // Check per-minute limit
                if (now - rateLimitInfo.LastRequestTime < TimeSpan.FromMinutes(1))
                {
                    if (rateLimitInfo.RequestCount >= _maxRequestsPerMinute)
                    {
                        reason = $"Rate limit exceeded: {rateLimitInfo.RequestCount} requests in the last minute (max: {_maxRequestsPerMinute})";
                        _logger?.LogWarning("🚫 [RateLimit] {Identifier}: {Reason}", identifier, reason);
                        return true;
                    }
                }
                else
                {
                    // Reset per-minute counter
                    rateLimitInfo.RequestCount = 0;
                    rateLimitInfo.LastRequestTime = now;
                }

                // Check per-hour limit
                if (now - rateLimitInfo.FirstRequestTime < TimeSpan.FromHours(1))
                {
                    var hourCount = rateLimitInfo.TotalRequestsInHour;
                    if (hourCount >= _maxRequestsPerHour)
                    {
                        reason = $"Rate limit exceeded: {hourCount} requests in the last hour (max: {_maxRequestsPerHour})";
                        _logger?.LogWarning("🚫 [RateLimit] {Identifier}: {Reason}", identifier, reason);
                        return true;
                    }
                }
                else
                {
                    // Reset per-hour counter
                    rateLimitInfo.FirstRequestTime = now;
                    rateLimitInfo.TotalRequestsInHour = 0;
                }

                // Increment counters
                rateLimitInfo.RequestCount++;
                rateLimitInfo.TotalRequestsInHour++;
                rateLimitInfo.LastRequestTime = now;

                return false;
            }
        }

        /// <summary>
        /// Clean up old rate limit entries (call periodically)
        /// </summary>
        public void Cleanup()
        {
            var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromHours(2);
            var keysToRemove = new List<string>();

            foreach (var kvp in _rateLimitCache)
            {
                if (kvp.Value.LastRequestTime < cutoff)
                {
                    keysToRemove.Add(kvp.Key);
                }
            }

            foreach (var key in keysToRemove)
            {
                _rateLimitCache.TryRemove(key, out _);
            }

            if (keysToRemove.Count > 0)
            {
                _logger?.LogDebug("🧹 [RateLimit] Cleaned up {Count} old rate limit entries", keysToRemove.Count);
            }
        }

        private class RateLimitInfo
        {
            public string Identifier { get; set; } = "";
            public DateTimeOffset FirstRequestTime { get; set; }
            public DateTimeOffset LastRequestTime { get; set; }
            public int RequestCount { get; set; }
            public int TotalRequestsInHour { get; set; }
        }
    }
}

