using System.Net;
using Auth0.ManagementApi;
using Auth0.ManagementApi.Core;
using Auth0.ManagementApi.Users;
using IGroupsClient = Auth0.ManagementApi.Users.IGroupsClient;
using ILogsClient = Auth0.ManagementApi.Users.ILogsClient;
using IOrganizationsClient = Auth0.ManagementApi.Users.IOrganizationsClient;
using IRolesClient = Auth0.ManagementApi.Users.IRolesClient;
using ISessionsClient = Auth0.ManagementApi.Users.ISessionsClient;
using RawResponse = Auth0.ManagementApi.RawResponse;

namespace Services.Tests.Fakes;

public class FakeUsersClient(List<UserResponseSchema> users) : IUsersClient
{
    public async Task<Pager<UserResponseSchema>> ListAsync(ListUsersRequestParameters request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var pager = new FakePager<UserResponseSchema>(users);
        return pager;
    }

    public WithRawResponseTask<CreateUserResponseContent> CreateAsync(CreateUserRequestContent request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public WithRawResponseTask<IEnumerable<UserResponseSchema>> ListUsersByEmailAsync(
        ListUsersByEmailRequestParameters request, RequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var filteredUsers = users.Where(u =>
            string.Equals(u.Email, request.Email, StringComparison.InvariantCultureIgnoreCase));

        return new WithRawResponseTask<IEnumerable<UserResponseSchema>>(Task.FromResult(
            new WithRawResponse<IEnumerable<UserResponseSchema>>
            {
                Data = filteredUsers,
                RawResponse = new RawResponse
                    { Headers = new ResponseHeaders(), StatusCode = HttpStatusCode.OK, Url = new Uri("https://ahhhhh.com") }
            }));
    }

    public WithRawResponseTask<GetUserResponseContent> GetAsync(string id, GetUserRequestParameters request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public async Task DeleteAsync(string id, RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public WithRawResponseTask<UpdateUserResponseContent> UpdateAsync(string id, UpdateUserRequestContent request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        var userToUpdate = users.First(u => u.UserId == id);
        var updateResponse = new UpdateUserResponseContent
        {
            Email = request.Email.Value ?? userToUpdate.Email, UserId = userToUpdate.UserId,
            Username = request.Username.Value ?? userToUpdate.Username,
            Picture = request.Picture.Value ?? userToUpdate.Picture,
        };

        if (request.Email != null)
        {
            userToUpdate.Email = request.Email.Value;
        }

        if (request.Username != null)
        {
            userToUpdate.Username = request.Username.Value;
        }

        if (request.Picture != null)
        {
            userToUpdate.Picture = request.Picture.Value;
        }

        return new WithRawResponseTask<UpdateUserResponseContent>(Task.FromResult(
            new WithRawResponse<UpdateUserResponseContent>
            {
                Data = updateResponse,
                RawResponse = new RawResponse
                    { Headers = new ResponseHeaders(), StatusCode = HttpStatusCode.Accepted, Url = new Uri("https://ahhhhh.com") }
            }));
    }

    public WithRawResponseTask<RegenerateUsersRecoveryCodeResponseContent> RegenerateRecoveryCodeAsync(string id,
        RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public async Task RevokeAccessAsync(string id, RevokeUserAccessRequestContent request,
        RequestOptions? options = null,
        CancellationToken cancellationToken = new CancellationToken())
    {
        throw new NotImplementedException();
    }

    public IAuthenticationMethodsClient AuthenticationMethods { get; }
    public IAuthenticatorsClient Authenticators { get; }
    public IConnectedAccountsClient ConnectedAccounts { get; }
    public IEnrollmentsClient Enrollments { get; }
    public IFederatedConnectionsTokensetsClient FederatedConnectionsTokensets { get; }
    public IGroupsClient Groups { get; }
    public IIdentitiesClient Identities { get; }
    public ILogsClient Logs { get; }
    public IMultifactorClient Multifactor { get; }
    public IOrganizationsClient Organizations { get; }
    public IPermissionsClient Permissions { get; }
    public IRiskAssessmentsClient RiskAssessments { get; }
    public IRolesClient Roles { get; }
    public IRefreshTokenClient RefreshToken { get; }
    public ISessionsClient Sessions { get; }
}