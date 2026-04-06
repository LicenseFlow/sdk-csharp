# LicenseFlow .NET SDK

[![NuGet](https://img.shields.io/nuget/v/LicenseFlow.SDK)](https://www.nuget.org/packages/LicenseFlow.SDK)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)

**Stop Building Licensing Infrastructure. Start Shipping Software.**

The official C#/.NET SDK for [LicenseFlow](https://licenseflow.dev). Protect your intellectual property, enforce entitlements, and manage software distribution with enterprise-grade security.

## Installation

```bash
dotnet add package LicenseFlow.SDK
```

### Package Manager

```
Install-Package LicenseFlow.SDK
```

## Quick Start

```csharp
using LicenseFlow.SDK;

var client = new LicenseFlowClient(
    "https://api.licenseflow.dev",
    "lf_live_xxxxxxxxxxxx",
    "your-jwt-secret"
);

var activation = await client.ActivateAsync("XXXX-YYYY-ZZZZ-AAAA", "Windows Server");
Console.WriteLine($"Activated: {activation.success}");

var verification = await client.VerifyAsync("XXXX-YYYY-ZZZZ-AAAA");
Console.WriteLine($"Valid: {verification.valid}");
```

---

## API Reference

### Core Methods

| Method | Description |
|--------|-------------|
| `ActivateAsync(licenseKey, deviceName)` | Activate on a device |
| `VerifyAsync(licenseKey)` | Verify license (cached) |
| `DeactivateAsync(licenseKey)` | Deactivate from a device |
| `RecordUsageAsync(licenseKey, metricName, value)` | Track usage metrics |
| `GetHardwareId()` | Get machine name ID |

### Entitlements

```csharp
if (client.HasFeature(verification, "ai_features"))
{
    EnableAI();
}

var limit = client.GetEntitlement(verification, "max_projects");
Console.WriteLine($"Project limit: {limit}");
```

### Floating Licenses (Leases)

```csharp
var lease = await client.CheckoutLicenseAsync(
    "XXXX-XXXX", 3600, "ci-runner-1", "ci_runner"
);
Console.WriteLine($"Lease: {lease.LeaseKey}");

await client.CheckinLicenseAsync(lease.LeaseKey);
var status = await client.GetLeaseStatusAsync(lease.LeaseKey);
```

### Credits

```csharp
var result = await client.ConsumeCreditsAsync(100, "AI tokens");
Console.WriteLine($"Remaining: {result.Remaining}");

var balance = await client.GetCreditsBalanceAsync();
```

### Release Management

```csharp
var update = await client.CheckForUpdatesAsync("prod_123", "v1.0.0", "stable");

if (update != null)
{
    var download = await client.DownloadArtifactAsync(
        "XXXX-XXXX", update.Id, Platform.Windows, Architecture.X64
    );
    Console.WriteLine($"Download: {download.Url}");
}
```

### Offline Licensing

```csharp
string licenseContent = File.ReadAllText("license.lic");
var license = client.VerifyOfflineLicense(licenseContent, "ORG_PUBLIC_KEY_HEX");
Console.WriteLine($"Valid until: {license.ValidUntil}");
```

### Heartbeat

```csharp
client.StartHeartbeat("XXXX-XXXX", intervalSeconds: 60);
// ... later
client.StopHeartbeat();
```

---

## Error Handling

```csharp
try
{
    await client.ActivateAsync("XXXX", "Server");
}
catch (RateLimitException)
{
    Console.WriteLine("Rate limit exceeded");
}
catch (InvalidLicenseException)
{
    Console.WriteLine("Invalid license");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Features

- **HttpClient Factory** — Standard .NET networking with connection pooling
- **Async/Await** — First-class asynchronous support throughout
- **Thread-safe Caching** — In-memory verification cache
- **Ed25519** — Cryptographic offline license verification

## License

MIT

## Links

- 📖 [Documentation](https://docs.licenseflow.dev)
- 🐛 [Issues](https://github.com/licenseflow/csharp-sdk/issues)
- 🏠 [Homepage](https://licenseflow.dev)
