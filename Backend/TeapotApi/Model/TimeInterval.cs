namespace Model;

public record TimeInterval
{
    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }
}
