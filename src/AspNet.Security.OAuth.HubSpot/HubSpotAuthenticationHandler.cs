﻿/*
 * Licensed under the Apache License, Version 2.0 (http://www.apache.org/licenses/LICENSE-2.0)
 * See https://github.com/aspnet-contrib/AspNet.Security.OAuth.Providers
 * for more information concerning the license and the contributors participating to this project.
 */

using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AspNet.Security.OAuth.HubSpot
{
    public class HubSpotAuthenticationHandler : OAuthHandler<HubSpotAuthenticationOptions>
    {
        public HubSpotAuthenticationHandler(
            [NotNull] IOptionsMonitor<HubSpotAuthenticationOptions> options,
            [NotNull] ILoggerFactory logger,
            [NotNull] UrlEncoder encoder,
            [NotNull] ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override async Task<AuthenticationTicket> CreateTicketAsync(
            [NotNull] ClaimsIdentity identity,
            [NotNull] AuthenticationProperties properties,
            [NotNull] OAuthTokenResponse tokens)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Options.UserInformationEndpoint + "/" + tokens.AccessToken);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var response = await Backchannel.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, Context.RequestAborted);
            if (!response.IsSuccessStatusCode)
            {
                Logger.LogError("An error occurred while retrieving the user profile: the remote server " +
                                "returned a {Status} response with the following payload: {Headers} {Body}.",
                                /* Status: */ response.StatusCode,
                                /* Headers: */ response.Headers.ToString(),
#if NET5_0_OR_GREATER
                                /* Body: */ await response.Content.ReadAsStringAsync(Context.RequestAborted));
#else
                                /* Body: */ await response.Content.ReadAsStringAsync());
#endif

                throw new HttpRequestException("An error occurred while retrieving the user profile.");
            }

#if NET5_0_OR_GREATER
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Context.RequestAborted));
#else
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
#endif
            var principal = new ClaimsPrincipal(identity);
            var context = new OAuthCreatingTicketContext(principal, properties, Context, Scheme, Options, Backchannel, tokens, payload.RootElement);
            context.RunClaimActions();

            await Options.Events.CreatingTicket(context);
            return new AuthenticationTicket(context.Principal!, context.Properties, Scheme.Name);
        }
    }
}
