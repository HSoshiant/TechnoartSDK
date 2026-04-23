namespace TechnoartSDK.Models;

public enum AuthenticationRequiredReason
{
    /// <summary>
    /// The user's session has expired and they need to sign in again.
    /// </summary>
    SessionExpired,
    /// <summary>
    /// The user is not authenticated. This can occur if the user tries to access a protected resource without signing in.
    /// </summary>
    NotAuthenticated
}
/// <summary>
/// Thrown when the downstream API returns 401 or 403, indicating the caller must re-authenticate.
/// Consumers should catch this at the UI boundary and redirect to the sign-in flow.
/// </summary>
public class AuthenticationRequiredException(string message, AuthenticationRequiredReason reason) : Exception(message)
{
    public AuthenticationRequiredReason Reason { get; } = reason;
}