using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Options;
using Services;

namespace DataAccessTests;

[TestFixture]
public class Auth0InvitationServiceTests
{
    [Test]
    public void InviteUserAsync_Throws_When_Email_Is_Missing()
    {
        var service = CreateInvitationService();

        var act = async () => await service.InviteUserAsync(string.Empty, "org_123");

        Assert.ThrowsAsync<ArgumentException>(async () => await act());
    }

    [Test]
    public async Task InviteUserAsync_Returns_Invitation_Data_And_Uses_Auth0_Endpoints()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://tenant.eu.auth0.com/oauth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"mgmt-token\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (request.RequestUri?.AbsoluteUri == "https://tenant.eu.auth0.com/api/v2/organizations/org_123/invitations")
            {
                Assert.That(request.Headers.Authorization, Is.EqualTo(new AuthenticationHeaderValue("Bearer", "mgmt-token")));

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"id\":\"inv_123\",\"invitation_url\":\"https://invite.example.com/token\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var httpClient = new HttpClient(handler);
        var options = CreateOptions();
        var tokenService = new Auth0TokenService(httpClient, options);
        var service = new Auth0InvitationService(httpClient, tokenService, options);

        var result = await service.InviteUserAsync("member@example.com", "org_123");

        Assert.Multiple(() =>
        {
            Assert.That(result.InvitationId, Is.EqualTo("inv_123"));
            Assert.That(result.InvitationUrl, Is.EqualTo("https://invite.example.com/token"));
        });
    }

    [Test]
    public void GetTokenAsync_Throws_When_Auth0_Config_Is_Incomplete()
    {
        var options = Options.Create(new Auth0Options());
        var tokenService = new Auth0TokenService(new HttpClient(new FakeHttpMessageHandler(_ => new HttpResponseMessage())), options);

        var act = async () => await tokenService.GetTokenAsync();

        Assert.ThrowsAsync<InvalidOperationException>(async () => await act());
    }

    private static Auth0InvitationService CreateInvitationService()
    {
        var options = CreateOptions();
        var httpClient = new HttpClient(new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri?.AbsoluteUri == "https://tenant.eu.auth0.com/oauth/token")
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        "{\"access_token\":\"mgmt-token\"}",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"id\":\"inv_123\",\"invitation_url\":\"https://invite.example.com/token\"}",
                    Encoding.UTF8,
                    "application/json")
            };
        }));

        var tokenService = new Auth0TokenService(httpClient, options);
        return new Auth0InvitationService(httpClient, tokenService, options);
    }

    private static IOptions<Auth0Options> CreateOptions() =>
        Options.Create(new Auth0Options
        {
            Domain = "tenant.eu.auth0.com",
            ClientId = "client-id",
            ClientSecret = "client-secret",
            ConnectionId = "connection-id",
            ManagementAudience = "https://tenant.eu.auth0.com/api/v2/"
        });

    private sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }
}
