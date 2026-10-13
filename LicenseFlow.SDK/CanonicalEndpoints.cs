using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LicenseFlow.SDK
{
    /// <summary>
    /// Canonical REST paths (SDK v2.3+). The api.licenseflow.dev gateway serves both
    /// these and the legacy /functions/v1/&lt;name&gt; paths indefinitely.
    /// </summary>
    public sealed class CanonicalEndpointsHandler : DelegatingHandler
    {
        public static readonly IReadOnlyDictionary<string, string> Paths = new Dictionary<string, string>
        {
            ["verify-license"] = "/v1/licenses/verify",
            ["activate-license"] = "/v1/licenses/activate",
            ["deactivate-license"] = "/v1/licenses/deactivate",
            ["checkout-license"] = "/v1/licenses/checkout",
            ["lease-status"] = "/v1/licenses/lease-status",
            ["generate-offline-license"] = "/v1/licenses/offline",
            ["record-usage"] = "/v1/usage/record",
            ["resolve-entitlements"] = "/v1/entitlements/resolve",
            ["check-entitlement"] = "/v1/entitlements/check",
            ["authorize"] = "/v1/access/authorize",
        };

        private const string LegacyPrefix = "/functions/v1/";

        public CanonicalEndpointsHandler() : base(new HttpClientHandler()) { }

        internal static Uri CanonicalUri(Uri uri)
        {
            if (uri == null || uri.Host != "api.licenseflow.dev") return null;
            var path = uri.AbsolutePath;
            var i = path.IndexOf(LegacyPrefix, StringComparison.Ordinal);
            if (i < 0) return null;
            if (!Paths.TryGetValue(path.Substring(i + LegacyPrefix.Length), out var target)) return null;
            return new UriBuilder(uri) { Path = target }.Uri;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var canonical = CanonicalUri(request.RequestUri);
            if (canonical == null) return await base.SendAsync(request, ct).ConfigureAwait(false);

            var legacy = request.RequestUri;
            byte[] body = request.Content != null ? await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false) : null;
            var contentHeaders = request.Content?.Headers;

            request.RequestUri = canonical;
            var res = await base.SendAsync(request, ct).ConfigureAwait(false);
            if (res.StatusCode != HttpStatusCode.NotFound || res.Headers.Contains("X-LicenseFlow-Route")) return res;

            // The gateway itself did not recognise the path: fall back once.
            res.Dispose();
            var retry = new HttpRequestMessage(request.Method, legacy);
            foreach (var h in request.Headers) retry.Headers.TryAddWithoutValidation(h.Key, h.Value);
            if (body != null)
            {
                retry.Content = new ByteArrayContent(body);
                if (contentHeaders != null)
                    foreach (var h in contentHeaders) retry.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
            }
            return await base.SendAsync(retry, ct).ConfigureAwait(false);
        }
    }
}
