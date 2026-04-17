namespace Model;

public record User
{
    public Guid Id { get; init; }

    public string UserName { get; set; }

    public string Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

}
