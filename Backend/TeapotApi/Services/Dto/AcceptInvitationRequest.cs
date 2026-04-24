namespace Api.Dto;

public class AcceptInvitationRequest
{
    public Guid InvitationId { get; set; }

    public string CurrentUserEmail { get; set; } = null!;
}
