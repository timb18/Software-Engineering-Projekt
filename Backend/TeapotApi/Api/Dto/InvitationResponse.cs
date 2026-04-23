namespace Api.Dto;

public class InvitationResponse
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public string OrgName { get; set; } = null!;

    public string FirstName { get; set; } = null!;

    public string LastName { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? ExpiryDate { get; set; }
}