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
        }

        public string GetHardwareId()
        {
            return Environment.MachineName;
        }

        public async Task<dynamic> ActivateAsync(string licenseKey, string deviceName)
        {
            var payload = new
            {
                license_key = licenseKey,
                device_id = GetHardwareId(),
                device_name = deviceName
            };
            return await PostAsync("functions/v1/activate-license", payload);
        }

        public async Task<dynamic> VerifyAsync(string licenseKey)
        {
            var deviceId = GetHardwareId();
            var cacheKey = $"verify:{licenseKey}:{deviceId}";

            if (_cache.ContainsKey(cacheKey)) return _cache[cacheKey];

            var payload = new
            {
                license_key = licenseKey,
                device_id = deviceId
            };

            var res = await PostAsync("functions/v1/verify-license", payload);
            if (res.valid == true) _cache[cacheKey] = res;
            return res;
        }

        public async Task<dynamic> DeactivateAsync(string licenseKey)
        {
            var payload = new
            {
                license_key = licenseKey,
                device_id = GetHardwareId()
            };
            var res = await PostAsync("functions/v1/deactivate-license", payload);
            _cache.Clear(); // Clear cache
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
    }
}
