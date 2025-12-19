using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

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
