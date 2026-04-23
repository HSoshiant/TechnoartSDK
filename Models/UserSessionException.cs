namespace TechnoartSDK.Models;

/// <summary>
/// Thrown when the authenticated JWT is valid but no matching user record exists in the database.
/// Indicates the session is stale and the user must sign in again to trigger the token exchange.
/// </summary>
public class UserSessionException(string message) : Exception(message);
