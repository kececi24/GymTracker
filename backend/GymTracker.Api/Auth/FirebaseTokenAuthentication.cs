using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace GymTracker.Api.Auth;

public static class FirebaseAuthConstants
{
    public const string SchemeName = "FirebaseBearer";
    public const string UidClaimType = "firebase_uid";
    public const string EmailClaimType = ClaimTypes.Email;
    public const string ProviderClaimType = "sign_in_provider";
    public const string FailureReasonItemKey = "FirebaseAuthFailureReason";
}

public sealed class FirebaseAuthOptions : AuthenticationSchemeOptions
{
    public const string SectionName = "FirebaseAuth";

    public string ProjectId { get; set; } = "gymtracker-pilot";
    public bool AllowDevelopmentTokens { get; set; } = true;
}

public enum TokenVerificationStatus
{
    Valid = 0,
    Missing = 1,
    Invalid = 2,
    Expired = 3
}

public sealed record VerifiedFirebaseToken(
    string Uid,
    string? Email,
    string SignInProvider,
    DateTimeOffset ExpiresAtUtc);

public sealed record TokenVerificationResult(
    TokenVerificationStatus Status,
    VerifiedFirebaseToken? Token,
    string? ErrorMessage)
{
    public static TokenVerificationResult Valid(VerifiedFirebaseToken token) =>
        new(TokenVerificationStatus.Valid, token, null);

    public static TokenVerificationResult Expired(string message = "Firebase identity token has expired.") =>
        new(TokenVerificationStatus.Expired, null, message);

    public static TokenVerificationResult Invalid(string message = "Invalid Firebase identity token.") =>
        new(TokenVerificationStatus.Invalid, null, message);
}

public interface IFirebaseTokenVerifier
{
    Task<TokenVerificationResult> VerifyIdTokenAsync(string rawToken, CancellationToken cancellationToken = default);
}

public sealed class FirebaseTokenVerifier(
    IOptions<FirebaseAuthOptions> optionsAccessor,
    TimeProvider timeProvider) : IFirebaseTokenVerifier
{
    public Task<TokenVerificationResult> VerifyIdTokenAsync(string rawToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return Task.FromResult(TokenVerificationResult.Invalid("Bearer token is empty."));
        }

        var now = timeProvider.GetUtcNow();
        var options = optionsAccessor.Value;

        if (options.AllowDevelopmentTokens)
        {
            if (rawToken.StartsWith("expired:", StringComparison.Ordinal))
            {
                return Task.FromResult(TokenVerificationResult.Expired());
            }

            if (rawToken.StartsWith("dev:", StringComparison.Ordinal))
            {
                // Format: dev:<uid>[:<email>[:<provider>]]
                var segments = rawToken.Split(':', StringSplitOptions.TrimEntries);
                if (segments.Length < 2 || string.IsNullOrWhiteSpace(segments[1]))
                {
                    return Task.FromResult(TokenVerificationResult.Invalid("Malformed development token."));
                }

                var uid = segments[1];
                var email = segments.Length >= 3 && !string.IsNullOrWhiteSpace(segments[2])
                    ? segments[2]
                    : $"{uid}@example.com";
                var provider = segments.Length >= 4 && !string.IsNullOrWhiteSpace(segments[3])
                    ? segments[3]
                    : "password";

                return Task.FromResult(TokenVerificationResult.Valid(
                    new VerifiedFirebaseToken(uid, email, provider, now.AddHours(1))));
            }
        }

        // Parse JWT format: header.payload.signature
        var parts = rawToken.Split('.');
        if (parts.Length != 3 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]) || string.IsNullOrWhiteSpace(parts[2]))
        {
            return Task.FromResult(TokenVerificationResult.Invalid("Malformed JWT structure."));
        }

        try
        {
            var payloadBytes = DecodeBase64Url(parts[1]);
            using var jsonDoc = JsonDocument.Parse(payloadBytes);
            var root = jsonDoc.RootElement;

            if (!root.TryGetProperty("exp", out var expElement) || !expElement.TryGetInt64(out var expSeconds))
            {
                return Task.FromResult(TokenVerificationResult.Invalid("Token is missing expiration claim."));
            }

            var expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);
            if (expiresAt <= now)
            {
                return Task.FromResult(TokenVerificationResult.Expired());
            }

            var uid = TryGetString(root, "user_id") ?? TryGetString(root, "sub");
            if (string.IsNullOrWhiteSpace(uid))
            {
                return Task.FromResult(TokenVerificationResult.Invalid("Token is missing subject/user_id claim."));
            }

            if (!string.IsNullOrWhiteSpace(options.ProjectId))
            {
                var aud = TryGetString(root, "aud");
                if (!string.IsNullOrWhiteSpace(aud) && !string.Equals(aud, options.ProjectId, StringComparison.Ordinal))
                {
                    return Task.FromResult(TokenVerificationResult.Invalid("Token audience does not match Firebase project."));
                }

                var expectedIssuer = $"https://securetoken.google.com/{options.ProjectId}";
                var iss = TryGetString(root, "iss");
                if (!string.IsNullOrWhiteSpace(iss) && !string.Equals(iss, expectedIssuer, StringComparison.Ordinal))
                {
                    return Task.FromResult(TokenVerificationResult.Invalid("Token issuer does not match Firebase project."));
                }
            }

            var email = TryGetString(root, "email");
            var signInProvider = "password";
            if (root.TryGetProperty("firebase", out var firebaseElement) &&
                firebaseElement.ValueKind == JsonValueKind.Object &&
                firebaseElement.TryGetProperty("sign_in_provider", out var providerElement) &&
                providerElement.ValueKind == JsonValueKind.String)
            {
                signInProvider = providerElement.GetString() ?? "password";
            }

            return Task.FromResult(TokenVerificationResult.Valid(
                new VerifiedFirebaseToken(uid, email, signInProvider, expiresAt)));
        }
        catch (Exception)
        {
            return Task.FromResult(TokenVerificationResult.Invalid("Token payload could not be decoded."));
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;

    private static byte[] DecodeBase64Url(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2:
                padded += "==";
                break;
            case 3:
                padded += "=";
                break;
        }

        return Convert.FromBase64String(padded);
    }
}

public sealed class FirebaseAuthenticationHandler(
    IOptionsMonitor<FirebaseAuthOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IFirebaseTokenVerifier tokenVerifier)
    : AuthenticationHandler<FirebaseAuthOptions>(options, logger, encoder)
{
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            Context.Items[FirebaseAuthConstants.FailureReasonItemKey] = "Authentication header is missing.";
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            Context.Items[FirebaseAuthConstants.FailureReasonItemKey] = "Authorization header must use Bearer scheme.";
            return AuthenticateResult.Fail("Authorization header must use Bearer scheme.");
        }

        var rawToken = header["Bearer ".Length..].Trim();
        var verification = await tokenVerifier.VerifyIdTokenAsync(rawToken, Context.RequestAborted);
        if (verification.Status != TokenVerificationStatus.Valid || verification.Token is null)
        {
            var reason = verification.ErrorMessage ?? "Invalid identity token.";
            Context.Items[FirebaseAuthConstants.FailureReasonItemKey] = reason;
            return AuthenticateResult.Fail(reason);
        }

        var token = verification.Token;
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, token.Uid),
            new(FirebaseAuthConstants.UidClaimType, token.Uid),
            new(FirebaseAuthConstants.ProviderClaimType, token.SignInProvider)
        };

        if (!string.IsNullOrWhiteSpace(token.Email))
        {
            claims.Add(new Claim(FirebaseAuthConstants.EmailClaimType, token.Email));
        }

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return AuthenticateResult.Success(ticket);
    }

    protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        var reason = Context.Items.TryGetValue(FirebaseAuthConstants.FailureReasonItemKey, out var value) && value is string text
            ? text
            : "Unauthenticated request.";

        await Response.WriteAsJsonAsync(new ApiErrorResponse(reason));
    }

    protected override async Task HandleForbiddenAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        Response.ContentType = "application/json";
        await Response.WriteAsJsonAsync(new ApiErrorResponse("You do not have permission to access another user's resource."));
    }
}

public sealed record ApiErrorResponse(string Error);

public static class ClaimsPrincipalOwnershipExtensions
{
    public static string? GetFirebaseUid(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseAuthConstants.UidClaimType)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

    public static string? GetEmail(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseAuthConstants.EmailClaimType);

    public static string GetSignInProvider(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(FirebaseAuthConstants.ProviderClaimType) ?? "password";

    public static bool IsOwner(this ClaimsPrincipal principal, string resourceOwnerUid)
    {
        var authenticatedUid = principal.GetFirebaseUid();
        return !string.IsNullOrWhiteSpace(authenticatedUid) &&
               string.Equals(authenticatedUid, resourceOwnerUid, StringComparison.Ordinal);
    }
}
