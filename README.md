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
    "https://your-project.supabase.co",
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
