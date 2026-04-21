namespace Api.Dto
{


    public class CreateInvitationRequest
    {
        public Guid OrganizationId { get; set; }
        public string Email { get; set; } = null!;
    }
}