namespace Model;

public record WorkProfile
{
    public Guid Id { get; init; }

    public Guid UserId { get; set; }

    public TimeSpan MaxDailyLoad { get; set; }

}
