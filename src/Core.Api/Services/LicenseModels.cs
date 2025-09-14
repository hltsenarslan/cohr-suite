// src/Core.Api/Licensing/LicenseModels.cs

namespace Core.Api.Services;

public enum LicenseMode { Cloud, OnPrem }
public record FeatureGrant(string Key, int UserLimit);
public record EffectiveLicense(
    LicenseMode Mode,
    DateTime IssuedAtUtc,
    DateTime ExpiresAtUtc,
    string LicenseId,
    string? MachineFingerprint,
    IReadOnlyList<FeatureGrant> Features
);