using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controller;

[Route("api/user/management")]
public class UserManagementController(IUserManagementService managementService) : ControllerBase
{
    [HttpPatch("change-password")]
    public async Task<Results<NoContent, NotFound<string>, InternalServerError<string>>> ChangePassword(
        [FromBody] ChangePasswordRequest changePasswordRequest,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await managementService.ChangePasswordAsync(changePasswordRequest, cancellationToken);
            return TypedResults.NoContent();
        }
        catch (KeyNotFoundException e)
        {
            return TypedResults.NotFound("User not found");
        }
        catch (Exception e)
        {
            return TypedResults.InternalServerError("There was an issue with changing the password");
        }
    }
}