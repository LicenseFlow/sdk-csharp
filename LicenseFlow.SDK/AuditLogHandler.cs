using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace LicenseFlow.SDK
{
    /// <summary>Opt-in non-fatal local activity logging; no payloads, headers or query strings.</summary>
    public sealed class AuditLogHandler : DelegatingHandler
    {
        private static readonly object Gate = new object();
        public AuditLogHandler() : base(new CanonicalEndpointsHandler()) { }
        public static string Mask(string key) => string.IsNullOrEmpty(key) ? "-" : key.Length <= 4 ? "****" : "****-" + key.Substring(key.Length - 4);
        private static string Safe(string value) => Regex.Replace(value ?? "-", "[^A-Za-z0-9._-]", "_");
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var flag = (Environment.GetEnvironmentVariable("LICENSEFLOW_AUDIT_LOG") ?? "").ToLowerInvariant();
            if (flag != "1" && flag != "true" && flag != "yes") return await base.SendAsync(request, ct).ConfigureAwait(false);
            var timer = Stopwatch.StartNew();
            var action = request.RequestUri?.AbsolutePath.Split('?')[0].Replace("/functions/v1/", "").Replace("/v1/", "").Trim('/').Split('/')[0] ?? "request";
            string key = null, rid = "-"; int status = 0;
            try
            {
                if (request.Content != null)
                {
                    var body = JObject.Parse(await request.Content.ReadAsStringAsync().ConfigureAwait(false));
                    key = (string)(body["license_key"] ?? body["licenseKey"]);
                }
            }
            catch { /* Never log the body. */ }
            try
            {
                var response = await base.SendAsync(request, ct).ConfigureAwait(false);
                status = (int)response.StatusCode;
                if (response.Headers.TryGetValues("x-request-id", out var ids)) rid = string.Join("", ids);
                return response;
            }
            finally
            {
                lock (Gate)
                {
                    try
                    {
                        var now = DateTime.UtcNow;
                        var dir = Environment.GetEnvironmentVariable("LICENSEFLOW_AUDIT_DIR") ??
                            (Environment.OSVersion.Platform == PlatformID.Win32NT
                                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LicenseFlow", "logs")
                                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".licenseflow", "logs"));
                        Directory.CreateDirectory(dir);
                        var stem = "licenseflow-audit-" + now.ToString("yyyy-MM-dd");
                        var file = Path.Combine(dir, stem + ".txt");
                        for (var i = 1; File.Exists(file) && new FileInfo(file).Length >= 10 * 1024 * 1024; i++) file = Path.Combine(dir, stem + "." + i + ".txt");
                        File.AppendAllText(file, now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") + " | actor=app | action=" + Safe(action) + " | license=" + Mask(key) + " | result=" + (status >= 200 && status < 400 ? "ok" : "error") + " | status=" + (status == 0 ? "-" : status.ToString()) + " | duration_ms=" + timer.ElapsedMilliseconds + " | request_id=" + Safe(rid) + "\n");
                        foreach (var old in Directory.GetFiles(dir, "licenseflow-audit-*.txt")) if (File.GetLastWriteTimeUtc(old) < now.AddDays(-14)) File.Delete(old);
                    }
                    catch { /* Audit failures must never change API behavior. */ }
                }
            }
        }
    }
}