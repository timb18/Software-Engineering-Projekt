namespace Api.Dto;

public class DeclineInvitationRequest
{
    public Guid InvitationId { get; set; }

    public string CurrentUserEmail { get; set; } = null!;
}
