namespace Api.Dto;

public class CreateInvitationRequest
{
    public Guid OrganizationId { get; set; }

    public string CreatedByEmail { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;
}
