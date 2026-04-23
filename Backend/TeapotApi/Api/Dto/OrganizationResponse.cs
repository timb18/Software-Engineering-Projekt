namespace Api.Dto;

public class OrganizationResponse
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int RemainingInvitations { get; set; }

    public List<OrganizationMemberResponse> Users { get; set; } = new();

    public List<InvitationResponse> Invites { get; set; } = new();
}

public class OrganizationMemberResponse
{
    public Guid Id { get; set; }

    public string Username { get; set; } = "";

    public string Email { get; set; } = null!;

    public string Role { get; set; } = null!;
}