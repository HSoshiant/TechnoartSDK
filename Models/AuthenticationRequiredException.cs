namespace TechnoartSDK.Models;

/// <summary>
/// Thrown when the downstream API returns 401 or 403, indicating the caller must re-authenticate.
/// Consumers should catch this at the UI boundary and redirect to the sign-in flow.
/// </summary>
public class AuthenticationRequiredException(string message) : Exception(message);
