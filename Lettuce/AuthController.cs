using System.Drawing;
using System.Security.Claims;
using Lettuce.Database;
using Lettuce.Database.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Client.AspNetCore;
using OpenIddict.Client.WebIntegration;
using PuppeteerSharp;

namespace Lettuce;

public class AuthController(ILogger<AuthController> logger, PgContext pg) : Controller
{
    [HttpGet("~/login")]
    public IActionResult LogInWithDiscord(string returnUrl)
    {
        var properties = new AuthenticationProperties
        {
            // Only allow local return URLs to prevent open redirect attacks.
            RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/"
        };

        return Challenge(properties, OpenIddictClientWebIntegrationConstants.Providers.Discord);
    }
    
    [HttpGet("~/logout")]
    public async Task<IActionResult> LogOut()
    {
        await HttpContext.SignOutAsync();
        return Redirect("/");
    }

    [HttpGet("~/Preview")]
    public async Task<IActionResult> Preview([FromQuery] string arrowFrom, [FromQuery] string arrowTo)
    {
        await new BrowserFetcher().DownloadAsync();

        using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            DefaultViewport = new ViewPortOptions
            {
                Height = 800,
                Width = 800,
                IsMobile = true
            }
        });
        
        using var page = await browser.NewPageAsync();
        
        await page.GoToAsync($"{Program.BaseUrl}/game?arrowFrom={arrowFrom}&arrowTo={arrowTo}&disableUi=true");
        
        await page.WaitForFunctionAsync("() => {return window.lettuce.initialLoadDone === true}");

        return new FileContentResult(await page.ScreenshotDataAsync(), "image/png");
    }
    
    [HttpGet("~/discord-callback"), HttpPost("~/discord-callback"), IgnoreAntiforgeryToken]
    public async Task<ActionResult> LogInCallback()
    {
        // Retrieve the authorization data validated by OpenIddict as part of the callback handling.
        var result = await HttpContext.AuthenticateAsync(OpenIddictClientAspNetCoreDefaults.AuthenticationScheme);

        // Important: if the remote server doesn't support OpenID Connect and doesn't expose a userinfo endpoint,
        // result.Principal.Identity will represent an unauthenticated identity and won't contain any user claim.
        //
        // Such identities cannot be used as-is to build an authentication cookie in ASP.NET Core (as the
        // antiforgery stack requires at least a name claim to bind CSRF cookies to the user's identity) but
        // the access/refresh tokens can be retrieved using result.Properties.GetTokens() to make API calls.
        if (result.Principal is not ClaimsPrincipal { Identity.IsAuthenticated: true })
        {
            throw new InvalidOperationException("The external authorization data cannot be used for authentication.");
        }

        // Build an identity based on the external claims and that will be used to create the authentication cookie.
        var identity = new ClaimsIdentity(authenticationType: CookieAuthenticationDefaults.AuthenticationScheme);

        // By default, OpenIddict will automatically try to map the email/name and name identifier claims from
        // their standard OpenID Connect or provider-specific equivalent, if available. If needed, additional
        // claims can be resolved from the external identity and copied to the final authentication cookie.
        var claims = result.Principal.Claims.Select(c => c.Type).ToArray();
        logger.LogInformation("Claim types: {Claims}", string.Join(", ", claims));
        identity.SetClaim(ClaimTypes.Name, result.Principal.GetClaim("global_name"))
            .SetClaim("avatar", $"https://cdn.discordapp.com/avatars/{result.Principal.GetClaim(ClaimTypes.NameIdentifier)}/{result.Principal.GetClaim("avatar")}.webp")
            .SetClaim(ClaimTypes.NameIdentifier, result.Principal.GetClaim(ClaimTypes.NameIdentifier));
        // No email collectery
        // .SetClaim(ClaimTypes.Email, result.Principal.GetClaim(ClaimTypes.Email))

        // Preserve the registration identifier to be able to resolve it later.
        identity.SetClaim(OpenIddictConstants.Claims.Private.RegistrationId, result.Principal.GetClaim(OpenIddictConstants.Claims.Private.RegistrationId));

        // Build the authentication properties based on the properties that were added when the challenge was triggered.
        if (result.Properties?.RedirectUri == "/") result.Properties.RedirectUri = "/game";
        var properties = new AuthenticationProperties(result.Properties?.Items ?? new Dictionary<string, string?>())
        {
            RedirectUri = result.Properties?.RedirectUri ?? "/game",
            IsPersistent = true,
            AllowRefresh = true
        };

        // If needed, the tokens returned by the authorization server can be stored in the authentication cookie.
        //
        // To make cookies less heavy, tokens that are not used are filtered out before creating the cookie.
        // properties.StoreTokens(result.Properties.GetTokens().Where(token => token.Name is
        //     // Preserve the access, identity and refresh tokens returned in the token response, if available.
        //     OpenIddictClientAspNetCoreConstants.Tokens.BackchannelAccessToken or
        //     OpenIddictClientAspNetCoreConstants.Tokens.BackchannelIdentityToken or
        //     OpenIddictClientAspNetCoreConstants.Tokens.RefreshToken));
        // probably not needed to store things
        
        // Ask the default sign-in handler to return a new cookie and redirect the
        // user agent to the return URL stored in the authentication properties.
        //
        // For scenarios where the default sign-in handler configured in the ASP.NET Core
        // authentication options shouldn't be used, a specific scheme can be specified here.

        var pawn = pg.Pawns.FirstOrDefault(p => p.DiscordId == identity.GetClaim(ClaimTypes.NameIdentifier));
        if (pawn != null)
        {
            pawn.DisplayName = identity.GetClaim(ClaimTypes.Name) ?? "Unknown";
            pawn.AvatarUri = identity.GetClaim("avatar");
        }
        else
        {
            var pawnCount = await pg.Pawns.CountAsync();
            if (pawnCount > 1)
            {
                return Unauthorized("Only players may log in");
            }

            pawn = new Pawn
            {
                DiscordId = identity.GetClaim(ClaimTypes.NameIdentifier)!,
                DisplayName = identity.GetClaim(ClaimTypes.Name) ?? "Unknown",
                X = Random.Shared.Next(0, Program.GridWidth),
                Y = Random.Shared.Next(0, Program.GridHeight),
                Health = 3,
                Actions = 0,
                Color = Color.FromArgb(255, Random.Shared.Next(0, 255), Random.Shared.Next(0, 255),
                    Random.Shared.Next(0, 255)),
                KilledAt = null,
                KilledBy = null,
                Vote = null,
                AvatarUri = identity.GetClaim("avatar"),
                IsAdmin = true
            };
            pg.Add(pawn);
        }

        identity.SetClaim("pawnId", pawn.Id.ToString());
        await pg.SaveChangesAsync();

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity), properties);
        
        return LocalRedirect(properties.RedirectUri);
        // return SignIn(new ClaimsPrincipal(identity), properties);
    }
}