# LicenseFlow.SDK

Official C#/.NET SDK for LicenseFlow.

## Installation

```bash
dotnet add package LicenseFlow.SDK
```

## Quick Start

```csharp
using LicenseFlow.SDK;

var client = new LicenseFlowClient(
    "https://api.licenseflow.dev/v1",
    "lf_live_xxxxxxxxxxxx", // Generated from the SaaS platform
    "your-jwt-secret"
);

try 
{
    // 1. Activate License
    var activation = await client.ActivateAsync("XXXX-YYYY-ZZZZ-AAAA", "Windows Server 2022");
    Console.WriteLine($"Activated: {activation.success}");

    // 2. Verify License (Uses internal cache)
    var verification = await client.VerifyAsync("XXXX-YYYY-ZZZZ-AAAA");
    Console.WriteLine($"Valid: {verification.valid}");
}
catch (RateLimitException)
{
    Console.WriteLine("Rate limit exceeded");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
}
```

## Features

- **HttpClient Factory**: Built-in support for standard .NET networking.
- **Hardware ID**: Automatic machine name identification.
- **Async/Await**: First-class asynchronous support.
- **Smart Caching**: In-memory verification caching.

## Phase 5: Entitlements

Check access to specific features:

```csharp
// Check boolean feature
if (client.HasFeature(verification, "ai_features"))
{
    EnableAI();
}

// Get numeric entitlement
var limit = client.GetEntitlement(verification, "max_projects");
Console.WriteLine($"Project limit: {limit}");
```

## Phase 5: Release Management

Check for updates and download artifacts:

```csharp
// Check for updates
var update = await client.CheckForUpdatesAsync("prod_123", "v1.0.0", "stable");

if (update != null)
{
    Console.WriteLine($"New version: {update.Version}");
    
    // Get download link
    var download = await client.DownloadArtifactAsync(
        "LF-KEY-123", 
        update.Id, 
        Platform.Windows, 
        Architecture.X64
    );
    
    Console.WriteLine($"Download URL: {download.Url}");
}
```

## Phase 5: Offline Licensing

Verify a license file without internet access:

```csharp
string licenseContent = File.ReadAllText("license.lic");
string publicKey = "YOUR_ORG_PUBLIC_KEY_HEX";

try
{
    var license = client.VerifyOfflineLicense(licenseContent, publicKey);
    Console.WriteLine("Offline license valid!");
}
catch (Exception ex)
{
    Console.WriteLine($"Invalid license: {ex.Message}");
}
```
