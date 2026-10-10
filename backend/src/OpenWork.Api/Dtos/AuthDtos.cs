namespace OpenWork.Api.Dtos;

/// <summary>Whether this installation has a PIN set up yet, and whether a recovery passkey exists.</summary>
public record AuthStatusResponse(bool Configured, bool HasRecovery);

public record PinRequest(string Pin);

public record AuthTokenResponse(string Token, DateTimeOffset ExpiresAt);
