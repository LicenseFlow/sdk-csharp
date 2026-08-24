using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace LicenseFlow.SDK
{
    /// <summary>
    /// Cached entitlement decision with TTL and offline grace support.
    /// </summary>
    public class CachedEntry
    {
        public Dictionary<string, object> Data { get; set; } = new();
        public DateTimeOffset CachedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
        /// <summary>"network", "cache", or "offline"</summary>
        public string Source { get; set; } = "network";
    }

    /// <summary>
    /// Thread-safe, TTL-based local entitlement cache for zero-network-request validation.
    /// 
    /// <para>Strategies:</para>
    /// <list type="bullet">
    ///   <item><c>cache-first</c> — Return cache if valid, else network.</item>
    ///   <item><c>stale-while-revalidate</c> — Return cache immediately, revalidate in background.</item>
    ///   <item><c>network-first</c> — Always hit network, cache as fallback.</item>
    /// </list>
    /// </summary>
    public class EntitlementCache
    {
        private readonly ConcurrentDictionary<string, CachedEntry> _entries = new();
        private readonly TimeSpan _ttl;
        private readonly TimeSpan _gracePeriod;
        private readonly string _strategy;

        public EntitlementCache(
            int ttlSeconds = 300,
            double offlineGraceHours = 72.0,
            string strategy = "stale-while-revalidate")
        {
            _ttl = TimeSpan.FromSeconds(ttlSeconds);
            _gracePeriod = TimeSpan.FromHours(offlineGraceHours);
            _strategy = strategy;
        }

        /// <summary>Get cached entitlements. Returns null on miss/expiry.</summary>
        public CachedEntry? Get(string key)
        {
            if (!_entries.TryGetValue(key, out var entry))
                return null;

            var now = DateTimeOffset.UtcNow;

            // Within normal TTL
            if (now < entry.ExpiresAt)
                return new CachedEntry
                {
                    Data = entry.Data,
                    CachedAt = entry.CachedAt,
                    ExpiresAt = entry.ExpiresAt,
                    Source = "cache"
                };

            // Within offline grace period
            if (now < entry.CachedAt.Add(_gracePeriod))
                return new CachedEntry
                {
                    Data = entry.Data,
                    CachedAt = entry.CachedAt,
                    ExpiresAt = entry.ExpiresAt,
                    Source = "offline"
                };

            // Fully expired
            _entries.TryRemove(key, out _);
            return null;
        }

        /// <summary>Store entitlement decision in cache.</summary>
        public void Set(string key, Dictionary<string, object> data)
        {
            var now = DateTimeOffset.UtcNow;
            _entries[key] = new CachedEntry
            {
                Data = data,
                CachedAt = now,
                ExpiresAt = now.Add(_ttl),
                Source = "network"
            };
        }

        /// <summary>Remove a specific cached entry.</summary>
        public void Invalidate(string key)
        {
            _entries.TryRemove(key, out _);
        }

        /// <summary>Clear all cached entries.</summary>
        public void Flush()
        {
            _entries.Clear();
        }

        /// <summary>
        /// Determine cache action: "use_cache", "use_cache_revalidate", or "use_network".
        /// </summary>
        public string GetStrategy(string key)
        {
            var entry = Get(key);
            if (entry == null) return "use_network";

            return _strategy switch
            {
                "cache-first" => entry.Source == "offline" ? "use_cache_revalidate" : "use_cache",
                "stale-while-revalidate" => entry.Source == "cache" ? "use_cache" : "use_cache_revalidate",
                _ => "use_network"
            };
        }

        /// <summary>Number of cached entries.</summary>
        public int Size => _entries.Count;
    }
}
