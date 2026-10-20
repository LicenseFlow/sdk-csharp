using System;
using System.Diagnostics;
using System.Threading.Tasks;
using LicenseFlow.SDK;

namespace DesktopCadApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("[LicenseFlow C#] Starting Desktop CAD Suite...");

            var client = new LicenseFlowClient("https://api.licenseflow.dev/v1", "lf_live_demo_cad_token");

            // 1. Hardware Fingerprinting
            var hwId = client.GetHardwareId();
            Console.WriteLine($"[Hardware] Device ID: {hwId}");

            // 2. Online Verification
            Console.WriteLine("[License] Verifying license key LF-CAD-PRO-2026...");
            var res = await client.VerifyAsync("LF-CAD-PRO-2026");
            Console.WriteLine($"[License] Valid: {res?.valid ?? true}");

            // 3. Start real-time background revocation stream
            Console.WriteLine("[Stream] Subscribing to real-time revocation events...");
            client.StartRevocationStream(TimeSpan.FromSeconds(2));

            // 4. Ultra-low latency render-loop check (<100ns)
            var sw = Stopwatch.StartNew();
            bool isRevoked = client.IsRevoked("designer@cadstudio.com", "LF-CAD-PRO-2026", null, out var reason);
            sw.Stop();
            Console.WriteLine($"[Performance] Access check elapsed: {sw.ElapsedTicks * (1000000000.0 / Stopwatch.Frequency):F1} ns");
            Console.WriteLine($"[Access] Is Revoked: {isRevoked}");

            // 5. Emergency kill-switch trigger in local memory
            Console.WriteLine("[Kill-Switch] Tripping emergency kill-switch locally...");
            client.TripKillSwitchLocal("license", "LF-CAD-PRO-2026", "Compliance audit flag: unlicensed workstation clone");

            bool isNowRevoked = client.IsRevoked("designer@cadstudio.com", "LF-CAD-PRO-2026", null, out var killReason);
            Console.WriteLine($"[Kill-Switch] Is Revoked Now: {isNowRevoked} (Reason: {killReason})");

            client.StopRevocationStream();
            Console.WriteLine("[LicenseFlow C#] Native desktop parity verification complete.");
        }
    }
}
