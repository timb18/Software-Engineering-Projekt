namespace Model;

public record Invitation
{
    public Guid Id { get; init; }

    public string Email { get; set; }

    public string InviteCode { get; set; }
}
