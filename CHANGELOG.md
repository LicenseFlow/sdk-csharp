# Changelog

All notable changes to the LicenseFlow .NET SDK will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [2.1.0] - 2026-02-17

### Added
- Environment scoping support: `environmentId` parameter in all license operations
- Cache isolation between environments
- Floating license lease methods: `CheckoutLicenseAsync()`, `CheckinLicenseAsync()`, `GetLeaseStatusAsync()`
- Credit system methods: `ConsumeCreditsAsync()`, `GetCreditsBalanceAsync()`
- Heartbeat support: `StartHeartbeat()`, `StopHeartbeat()`

### Changed
- Cache key format now includes environment context
- Defaults to `"default"` environment when `environmentId` is null

## [2.0.0] - 2026-01-19

### Added
- Entitlements system: `HasFeature()`, `GetEntitlement()`
- Release management: `CheckForUpdatesAsync()`, `DownloadArtifactAsync()`
- Offline licensing: `VerifyOfflineLicense()` with Ed25519 signature verification

### Changed
- Verification response now includes optional `Entitlements` property
- Full async/await support across all methods

## [1.0.0] - 2025-06-01

### Added
- Initial release with license activation, verification, and deactivation
- Hardware ID auto-detection via machine name
- In-memory caching with configurable TTL
- HttpClient-based networking
- Custom exception classes

[2.1.0]: https://github.com/licenseflow/csharp-sdk/compare/v2.0.0...v2.1.0
[2.0.0]: https://github.com/licenseflow/csharp-sdk/compare/v1.0.0...v2.0.0
[1.0.0]: https://github.com/licenseflow/csharp-sdk/releases/tag/v1.0.0
