using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TechnoartSDK.HTTP;
using TechnoartSDK.Models;

namespace TechnoartSDK.Extensions;

/// <summary>
/// Reusable authentication extension methods that support any OAuth provider.
/// Web side: cookie + external OAuth providers from config, with self-issued JWT generation.
/// API side: validates the self-issued JWT with a shared symmetric key.
/// </summary>
public static class AuthExtensions
{
    #region Static Fields

    private const string TokenIssuer = "Technoart";
    private const string TokenAudience = "Technoart.API";

    #endregion Static Fields

    #region Static Methods

    /// <summary>
    /// Configures cookie + external OAuth authentication for a Blazor Server / SSR web app.
    /// Binds <see cref="ExternalAuthOptions"/> from the <c>Authentication</c> config section
    /// and registers it as <c>IOptions&lt;ExternalAuthOptions&gt;</c>.
    /// After each OAuth callback, generates a self-issued JWT and stores it in the auth
    /// properties so <see cref="BearerTokenForwardingHandler"/> can forward it to the API.
    /// </summary>
    public static IServiceCollection AddExternalAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ExternalAuthOptions.SectionName);
        var authOptions = section.Get<ExternalAuthOptions>()
            ?? throw new InvalidOperationException($"{ExternalAuthOptions.SectionName} section is not configured.");

        if (string.IsNullOrWhiteSpace(authOptions.TokenSigningKey))
        {
            throw new InvalidOperationException(
                $"{ExternalAuthOptions.SectionName}:{nameof(ExternalAuthOptions.TokenSigningKey)} is not configured.");
        }

        if (authOptions.Providers.Count == 0)
        {
            throw new InvalidOperationException(
                $"No authentication providers configured. Add at least one provider to {ExternalAuthOptions.SectionName}:{nameof(ExternalAuthOptions.Providers)}.");
        }

        // Register as IOptions<ExternalAuthOptions> so components can inject it
        services.Configure<ExternalAuthOptions>(section);

        var authBuilder = services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            // Default challenge redirects to the sign-in page — per-provider challenge is explicit
            options.DefaultChallengeScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.LoginPath = AuthClaimsConstants.SignInPath;
        });

        foreach (var provider in authOptions.Providers)
        {
            RegisterProvider(authBuilder, provider, authOptions.TokenSigningKey);
        }

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Configures JWT Bearer authentication on the API side to validate self-issued tokens
    /// created by the Web host after OAuth sign-in. Binds the signing key from the
    /// <c>Authentication</c> config section. Provider-agnostic.
    /// </summary>
    public static IServiceCollection AddApiTokenValidation(
        this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ExternalAuthOptions.SectionName);
        var authOptions = section.Get<ExternalAuthOptions>()
            ?? throw new InvalidOperationException($"{ExternalAuthOptions.SectionName} section is not configured.");

        if (string.IsNullOrWhiteSpace(authOptions.TokenSigningKey))
        {
            throw new InvalidOperationException(
                $"{ExternalAuthOptions.SectionName}:{nameof(ExternalAuthOptions.TokenSigningKey)} is not configured.");
        }

        // Register as IOptions<ExternalAuthOptions> if not already registered by AddExternalAuthentication
        services.Configure<ExternalAuthOptions>(section);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = TokenIssuer,
                    ValidateAudience = true,
                    ValidAudience = TokenAudience,
                    ValidateLifetime = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(authOptions.TokenSigningKey)),
                };
            });

        services.AddAuthorization();
        return services;
    }

    /// <summary>
    /// Creates a self-issued JWT from the given claims, signed with the shared symmetric key.
    /// Used by the Web host to generate tokens that the API can validate.
    /// </summary>
    public static string GenerateApiToken(IEnumerable<Claim> claims, string signingKey, TimeSpan? expiry = null)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: TokenIssuer,
            audience: TokenAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(expiry ?? TimeSpan.FromHours(12)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>
    /// Registers <see cref="BearerTokenForwardingHandler"/> as a transient DelegatingHandler
    /// and attaches it to the given <see cref="IHttpClientBuilder"/>.
    /// </summary>
    public static IHttpClientBuilder AddBearerTokenForwarding(this IHttpClientBuilder builder)
    {
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<BearerTokenForwardingHandler>();
        return builder.AddHttpMessageHandler<BearerTokenForwardingHandler>();
    }

    /// <summary>
    /// Registers a known or custom OAuth provider on the authentication builder.
    /// After a successful sign-in, generates a self-issued JWT and stores it in the
    /// authentication properties for downstream API forwarding.
    /// </summary>
    private static void RegisterProvider(
        AuthenticationBuilder builder, AuthProviderConfig provider, string signingKey)
    {
        switch (provider.Name.ToLowerInvariant())
        {
            case AuthClaimsConstants.GoogleProvider:
                builder.AddGoogle(provider.Name, options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.SaveTokens = true;
                    options.CallbackPath = AuthClaimsConstants.GoogleCallbackPath;
                    options.ClaimActions.MapJsonKey(AuthClaimsConstants.Picture, AuthClaimsConstants.Picture);
                    AttachTokenGeneration(options.Events, provider.Name, signingKey);
                });
                break;

            case AuthClaimsConstants.MicrosoftProvider:
                builder.AddMicrosoftAccount(provider.Name, options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.SaveTokens = true;
                    options.CallbackPath = AuthClaimsConstants.MicrosoftCallbackPath;
                    AttachTokenGeneration(options.Events, provider.Name, signingKey);
                });
                break;

            case AuthClaimsConstants.GitHubProvider:
                builder.AddGitHub(provider.Name, options =>
                {
                    options.ClientId = provider.ClientId;
                    options.ClientSecret = provider.ClientSecret;
                    options.SaveTokens = true;
                    options.CallbackPath = AuthClaimsConstants.GitHubCallbackPath;
                    AttachTokenGeneration(options.Events, provider.Name, signingKey);
                });
                break;

            default:
                throw new NotSupportedException(
                    $"Authentication provider '{provider.Name}' is not supported. " +
                    $"Supported providers: Google, Microsoft, GitHub. " +
                    $"To add a new provider, register it in AuthExtensions.RegisterProvider().");
        }
    }

    /// <summary>
    /// Hooks into the OAuth <c>OnTicketReceived</c> event to generate a self-issued JWT
    /// containing the normalized user claims, then stores it in auth properties so
    /// <see cref="BearerTokenForwardingHandler"/> can read it.
    /// </summary>
    private static void AttachTokenGeneration(
        Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents events, string providerName, string signingKey)
    {
        events.OnTicketReceived = ctx =>
        {
            if (ctx.Principal?.Identity is ClaimsIdentity identity)
            {
                // Add provider metadata as claims so they flow into the cookie and JWT
                identity.AddClaim(new Claim(AuthClaimsConstants.Provider, providerName));

                var providerId = ctx.Principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                identity.AddClaim(new Claim(AuthClaimsConstants.ProviderId, providerId));
            }

            // Generate a self-issued JWT with the user's claims for API forwarding
            var apiToken = GenerateApiToken(ctx.Principal?.Claims ?? [], signingKey);

            // Store as an authentication token alongside the provider's own tokens
            var tokens = ctx.Properties?.GetTokens().ToList() ?? [];
            tokens.Add(new AuthenticationToken { Name = AuthClaimsConstants.ApiTokenName, Value = apiToken });
            ctx.Properties?.StoreTokens(tokens);

            return Task.CompletedTask;
        };
    }

    #endregion Static Methods
}
