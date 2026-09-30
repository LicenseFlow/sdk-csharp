using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NSec.Cryptography;
using System.Linq;

namespace LicenseFlow.SDK
{
    public class LicenseFlowClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _jwtSecret;
        private readonly Dictionary<string, dynamic> _cache = new Dictionary<string, dynamic>();

        public LicenseFlowClient(string baseUrl, string apiKey, string jwtSecret = null)
        {
            _apiKey = apiKey;
            _jwtSecret = jwtSecret;
            _httpClient = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/") };
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }

        public string GetHardwareId()
        {
            try
            {
                var mac = System.Net.NetworkInformation.NetworkInterface
                    .GetAllNetworkInterfaces()
                    .Where(nic => nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up && nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                    .Select(nic => nic.GetPhysicalAddress().ToString())
                    .FirstOrDefault();

                return !string.IsNullOrEmpty(mac) 
                    ? $"{Environment.MachineName}-{mac}" 
                    : Environment.MachineName;
            }
            catch
            {
                return Environment.MachineName;
            }
        }

        public async Task<dynamic> ActivateAsync(string licenseKey, string deviceName, string environmentId = null)
        {
            var payload = new
            {
                license_key = licenseKey,
                device_id = GetHardwareId(),
                device_name = deviceName,
                environment_id = environmentId
            };
            return await PostAsync("functions/v1/activate-license", payload);
        }

        public async Task<dynamic> VerifyAsync(string licenseKey, string environmentId = null)
        {
            var deviceId = GetHardwareId();
            var cacheKey = $"verify:{licenseKey}:{deviceId}:{environmentId ?? "default"}";

            if (_cache.ContainsKey(cacheKey)) return _cache[cacheKey];

            var payload = new
            {
                licenseKey = licenseKey,
                deviceId = deviceId,
                environmentId = environmentId
            };

            var res = await PostAsync("functions/v1/verify-license", payload);
            if (res.valid == true) _cache[cacheKey] = res;
            return res;
        }

        public async Task<dynamic> DeactivateAsync(string licenseKey, string environmentId = null)
        {
            var payload = new
            {
                license_key = licenseKey,
                device_id = GetHardwareId(),
                environment_id = environmentId
            };
            var res = await PostAsync("functions/v1/deactivate-license", payload);
            _cache.Clear(); // Clear cache
            return res;
        }

        /// <summary>
        /// Identity-based (keyless) entitlement resolution.
        /// Resolves everything an authenticated person is entitled to from their
        /// email alone — licenses they own plus any seats assigned to them.
        /// </summary>
        public async Task<dynamic> ResolveForIdentityAsync(string email, string productId = null, string environmentId = null)
        {
            var cacheKey = $"identity:{email}:{productId ?? "all"}:{environmentId ?? "default"}";
            if (_cache.ContainsKey(cacheKey))
            {
                return _cache[cacheKey];
            }

            var payload = new
            {
                email,
                productId,
                environmentId
            };
            var res = await PostAsync("functions/v1/resolve-entitlements", payload);

            if (res?.resolved == true)
            {
                _cache[cacheKey] = res;
            }

            return res;
        }

        public bool HasFeature(dynamic verification, string featureCode)
        {
            if (verification?.valid != true || verification?.entitlements == null) return false;
            
            var entitlements = verification.entitlements;
            var ent = entitlements[featureCode];
            if (ent == null) return false;

            if (ent is bool b) return b;
            if (ent is Newtonsoft.Json.Linq.JObject obj)
            {
                return obj["enabled"]?.Value<bool>() == true || obj["value"]?.Value<bool>() == true;
            }
            if (ent is string s)
            {
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        public dynamic GetEntitlement(dynamic verification, string featureCode)
        {
            if (verification?.valid != true || verification?.entitlements == null) return null;
            return verification.entitlements[featureCode];
        }

        public async Task<dynamic> CheckForUpdatesAsync(string productId, string currentVersion, string channel = "stable")
        {
            var url = $"functions/v1/release-management/latest?product_id={productId}&channel={channel}";
            
            var response = await _httpClient.GetAsync(url);
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
            
            var responseString = await response.Content.ReadAsStringAsync();
            var data = JsonConvert.DeserializeObject<dynamic>(responseString);

            if (data == null || data.version == currentVersion) return null;
            return data;
        }

        public async Task<dynamic> DownloadArtifactAsync(string licenseKey, string releaseId = null, string artifactId = null, string platform = null, string architecture = null)
        {
            var payload = new
            {
                license_key = licenseKey,
                release_id = releaseId,
                artifact_id = artifactId,
                platform = platform,
                architecture = architecture
            };
            return await PostAsync("functions/v1/artifact-download", payload);
        }

        // ── Floating License Lease Methods ──

        public async Task<dynamic> CheckoutLicenseAsync(string licenseKey, int durationSeconds = 3600, string requesterId = null, string requesterType = "sdk", Dictionary<string, object> metadata = null)
        {
            var payload = new
            {
                license_key = licenseKey,
                duration_seconds = durationSeconds,
                requester_id = requesterId ?? GetHardwareId(),
                requester_type = requesterType,
                metadata
            };
            return await PostAsync("functions/v1/checkout-license", payload);
        }

        public async Task<dynamic> CheckinLicenseAsync(string leaseKey)
        {
            return await PostAsync("functions/v1/checkin-license", new { lease_key = leaseKey });
        }

        public async Task<dynamic> GetLeaseStatusAsync(string leaseKey)
        {
            return await PostAsync("functions/v1/lease-status", new { lease_key = leaseKey });
        }

        // ── Heartbeat ──

        private System.Threading.Timer _heartbeatTimer;

        public void StartHeartbeat(string licenseKey, int intervalMs = 60000)
        {
            StopHeartbeat();
            _heartbeatTimer = new System.Threading.Timer(async _ =>
            {
                try { await VerifyAsync(licenseKey); }
                catch (Exception ex) { Console.Error.WriteLine($"LicenseFlow heartbeat failed: {ex.Message}"); }
            }, null, intervalMs, intervalMs);
        }

        public void StopHeartbeat()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        // ── Usage Recording ──

        public async Task<dynamic> RecordUsageAsync(string licenseKey, string metricName, double value, bool increment = true, string environmentId = null)
        {
            var payload = new
            {
                license_key = licenseKey,
                metric_name = metricName,
                value = value,
                increment = increment,
                environment_id = environmentId
            };
            return await PostAsync("functions/v1/record-usage", payload);
        }

        // ── Credits / Usage-Based Billing ──

        public async Task<dynamic> ConsumeCreditsAsync(int amount, string description = null, string productId = null, string currency = "credits", string referenceId = null, string referenceType = null, IDictionary<string, object> metadata = null)
        {
            var payload = new Dictionary<string, object> { { "amount", amount } };
            if (description != null) payload["description"] = description;
            if (productId != null) payload["product_id"] = productId;
            if (currency != "credits") payload["currency"] = currency;
            if (referenceId != null) payload["reference_id"] = referenceId;
            if (referenceType != null) payload["reference_type"] = referenceType;
            if (metadata != null) payload["metadata"] = metadata;
            return await PostAsync("functions/v1/consume-credits", payload);
        }

        public async Task<dynamic> GetCreditsBalanceAsync(string productId = null, string currency = null)
        {
            var queryParams = new List<string>();
            if (productId != null) queryParams.Add($"product_id={productId}");
            if (currency != null) queryParams.Add($"currency={currency}");
            var query = queryParams.Count > 0 ? "?" + string.Join("&", queryParams) : "";

            var response = await _httpClient.GetAsync($"functions/v1/get-credit-balance{query}");
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<dynamic>(responseString);
        }

        // ── Entitlements Management ──

        public async Task<dynamic> ListEntitlementsAsync()
        {
            var response = await _httpClient.GetAsync("functions/v1/manage-entitlements");
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<dynamic>(responseString);
        }

        public async Task<dynamic> CreateEntitlementAsync(string code, string name, string dataType = "boolean", string description = null)
        {
            var payload = new Dictionary<string, object>
            {
                { "code", code },
                { "name", name },
                { "data_type", dataType }
            };
            if (description != null) payload["description"] = description;
            return await PostAsync("functions/v1/manage-entitlements", payload);
        }

        public async Task<dynamic> DeleteEntitlementAsync(string entitlementId)
        {
            var response = await _httpClient.DeleteAsync($"functions/v1/manage-entitlements/{entitlementId}");
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<dynamic>(responseString);
        }

        public async Task<dynamic> UpdateEntitlementAsync(string entitlementId, IDictionary<string, object> updates)
        {
            var json = JsonConvert.SerializeObject(updates ?? new Dictionary<string, object>());
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PutAsync($"functions/v1/manage-entitlements/{entitlementId}", content);
            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<dynamic>(responseString);
        }

        public async Task<dynamic> AssignEntitlementToLicenseAsync(string entitlementId, string licenseId, Dictionary<string, object> value)
        {
            return await PostAsync($"functions/v1/manage-entitlements/{entitlementId}/assign-to-license", new { license_id = licenseId, value });
        }

        public async Task<dynamic> AssignEntitlementToPolicyAsync(string entitlementId, string policyId, Dictionary<string, object> defaultValue)
        {
            return await PostAsync($"functions/v1/manage-entitlements/{entitlementId}/assign-to-policy", new { policy_id = policyId, default_value = defaultValue });
        }

        public dynamic VerifyOfflineLicense(string licenseContent, string publicKeyHex)
        {
            var data = JsonConvert.DeserializeObject<dynamic>(licenseContent);
            if (data?.license == null || data?.signature == null)
            {
                throw new Exception("Invalid offline license format");
            }

            string message = JsonConvert.SerializeObject(data.license);
            byte[] signature = Convert.FromBase64String(data.signature.ToString());
            byte[] publicKeyBytes = StringToByteArray(publicKeyHex);

            var algorithm = SignatureAlgorithm.Ed25519;
            var publicKey = PublicKey.Import(algorithm, publicKeyBytes, KeyBlobFormat.RawPublicKey);

            if (!algorithm.Verify(publicKey, Encoding.UTF8.GetBytes(message), signature))
            {
                throw new Exception("Invalid offline license signature");
            }

            var license = data.license;
            if (license.valid_until != null)
            {
                DateTime validUntil = DateTime.Parse(license.valid_until.ToString());
                if (DateTime.UtcNow > validUntil.ToUniversalTime())
                {
                    throw new Exception("Offline license has expired");
                }
            }

            return license;
        }

        /// <summary>
        /// Validate a signed JWT proof token offline using HS256.
        /// Returns an object with valid=true and payload on success.
        /// </summary>
        public dynamic ValidateProofOffline(string proof, string secret = null)
        {
            var key = secret ?? _jwtSecret;
            if (string.IsNullOrEmpty(key))
                throw new Exception("JWT secret is required for offline validation");

            var parts = proof.Split('.');
            if (parts.Length != 3)
                return new { valid = false, error = "invalid token format" };

            try
            {
                byte[] payloadBytes = Base64UrlDecode(parts[1]);
                byte[] expectedSig = Base64UrlDecode(parts[2]);
                var signingInput = parts[0] + "." + parts[1];
                using (var hmac = new System.Security.Cryptography.HMACSHA256(Encoding.UTF8.GetBytes(key)))
                {
                    var computedSig = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
                    if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(expectedSig, computedSig))
                        return new { valid = false, error = "signature verification failed" };
                }
                var payloadJson = Encoding.UTF8.GetString(payloadBytes);
                var payload = JsonConvert.DeserializeObject<dynamic>(payloadJson);
                return new { valid = true, payload };
            }
            catch (Exception ex)
            {
                return new { valid = false, error = ex.Message };
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            string padded = input.Replace('-', '+').Replace('_', '/');
            switch (padded.Length % 4)
            {
                case 2: padded += "=="; break;
                case 3: padded += "="; break;
            }
            return Convert.FromBase64String(padded);
        }

        private static byte[] StringToByteArray(string hex)
        {
            return Enumerable.Range(0, hex.Length)
                             .Where(x => x % 2 == 0)
                             .Select(x => Convert.ToByte(hex.Substring(x, 2), 16))
                             .ToArray();
        }

        private async Task<dynamic> PostAsync(string path, object payload)
        {
            var json = JsonConvert.SerializeObject(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(path, content);
            var responseString = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<dynamic>(responseString);

            if (!response.IsSuccessStatusCode)
            {
                var message = result?.message?.ToString() ?? result?.error?.ToString() ?? $"HTTP {response.StatusCode}";
                
                if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    throw new RateLimitException(message);
                
                if (response.StatusCode == System.Net.HttpStatusCode.BadRequest || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    throw new InvalidLicenseException(message, (int)response.StatusCode);

                throw new LicenseFlowException(message, "UNKNOWN_ERROR", (int)response.StatusCode);
            }

            return result;
        }

        /// <summary>
        /// Track high-throughput usage telemetry with idempotency, dimensions, and quota enforcement.
        /// </summary>
        public async Task<UsageTrackResponse> TrackUsageAsync(UsageTrackOptions options)
        {
            var raw = await PostAsync("/functions/v1/record-usage", options);
            return JsonConvert.DeserializeObject<UsageTrackResponse>(raw.ToString());
        }

        // ── Stage 5: LicenseFlow Plus Runtime Control Plane ──────────────────

        /// <summary>
        /// Runtime Authorization Control Plane (POST /v1/authorize)
        /// Evaluates whether a subject is entitled to perform an action on a protected resource.
        /// </summary>
        public async Task<AuthorizationDecision> AuthorizeAsync(AuthorizeOptions options)
        {
            var payload = new
            {
                subject = options.Subject,
                resource = options.Resource,
                action = options.Action ?? "*",
                environment = options.Environment,
                region = options.Region,
                requested_units = options.RequestedUnits,
                context = options.Context,
                dry_run = options.DryRun
            };

            var raw = await PostAsync("/functions/v1/authorize", payload);
            return JsonConvert.DeserializeObject<AuthorizationDecision>(raw.ToString());
        }

        /// <summary>
        /// Check if a subject has explicit entitlement to access a resource.
        /// </summary>
        public async Task<bool> CheckEntitlementAsync(string subject, string resource, string action = "*")
        {
            try
            {
                var decision = await AuthorizeAsync(new AuthorizeOptions
                {
                    Subject = subject,
                    Resource = resource,
                    Action = action
                });
                return decision.Allowed;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Universal Metering Event Ingestion (POST /v1/meter)
        /// </summary>
        public async Task<UsageEventResult> RecordUsageEventAsync(UsageEventOptions options)
        {
            var payload = new
            {
                operation = "record",
                meter_key = options.MeterKey,
                subject = options.Subject,
                resource = options.Resource,
                units = options.Units,
                dimensions = options.Dimensions ?? new Dictionary<string, object>(),
                metadata = options.Metadata ?? new Dictionary<string, object>(),
                idempotency_key = options.IdempotencyKey
            };

            var raw = await PostAsync("/functions/v1/meter", payload);
            return JsonConvert.DeserializeObject<UsageEventResult>(raw.ToString());
        }

        /// <summary>
        /// Emergency Revocation / Kill Switch Trigger
        /// </summary>
        public async Task<RevokeResult> RevokeAsync(string targetIdentifier, string reason, string level = "hard")
        {
            var payload = new
            {
                targetId = targetIdentifier,
                level = level,
                reason = reason
            };

            var raw = await PostAsync("/functions/v1/kill-switch", payload);
            return JsonConvert.DeserializeObject<RevokeResult>(raw.ToString());
        }
    }

    public class AuthorizeOptions
    {
        public string Subject { get; set; }
        public string Resource { get; set; }
        public string Action { get; set; } = "*";
        public string Environment { get; set; }
        public string Region { get; set; }
        public int? RequestedUnits { get; set; }
        public Dictionary<string, object> Context { get; set; }
        public bool DryRun { get; set; } = false;
    }

    public class Diagnostics
    {
        [JsonProperty("precedence_step")]
        public string PrecedenceStep { get; set; }

        [JsonProperty("matched_policy")]
        public string MatchedPolicy { get; set; }

        [JsonProperty("risk_level")]
        public string RiskLevel { get; set; }
    }

    public class AuthorizationDecision
    {
        [JsonProperty("allowed")]
        public bool Allowed { get; set; }

        [JsonProperty("decision")]
        public string Decision { get; set; } // ALLOW, DENY, THROTTLE, REQUIRE_APPROVAL

        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }

        [JsonProperty("approval_request_id")]
        public string ApprovalRequestId { get; set; }

        [JsonProperty("diagnostics")]
        public Diagnostics Diagnostics { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("dry_run")]
        public bool DryRun { get; set; }

        [JsonProperty("evaluated_at")]
        public string EvaluatedAt { get; set; }

        [JsonProperty("latency_ms")]
        public double LatencyMs { get; set; }
    }

    public class UsageEventOptions
    {
        public string MeterKey { get; set; }
        public string Subject { get; set; }
        public string Resource { get; set; }
        public int Units { get; set; }
        public Dictionary<string, object> Dimensions { get; set; }
        public Dictionary<string, object> Metadata { get; set; }
        public string IdempotencyKey { get; set; }
    }

    public class UsageEventResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("event_id")]
        public string EventId { get; set; }

        [JsonProperty("credits_deducted")]
        public decimal? CreditsDeducted { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    public class RevokeResult
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("reason")]
        public string Reason { get; set; }
    }

    public class UsageTrackOptions
    {
        [JsonProperty("license_key")]
        public string LicenseKey { get; set; }

        [JsonProperty("customer_id")]
        public string CustomerId { get; set; }

        [JsonProperty("event_name")]
        public string FeatureName { get; set; }

        [JsonProperty("quantity")]
        public decimal Quantity { get; set; } = 1;

        [JsonProperty("idempotency_key")]
        public string IdempotencyKey { get; set; }

        [JsonProperty("dimensions")]
        public Dictionary<string, object> Dimensions { get; set; } = new();

        [JsonProperty("metadata")]
        public Dictionary<string, object> Metadata { get; set; } = new();

        [JsonProperty("unit")]
        public string Unit { get; set; } = "units";
    }

    public class UsageTrackResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("current_usage")]
        public decimal? CurrentUsage { get; set; }

        [JsonProperty("quota_limit")]
        public decimal? QuotaLimit { get; set; }

        [JsonProperty("overage_units")]
        public decimal? OverageUnits { get; set; }

        [JsonProperty("enforcement_policy")]
        public string EnforcementPolicy { get; set; }

        [JsonProperty("is_duplicate")]
        public bool IsDuplicate { get; set; }
    }
}
