namespace Model;

public record TaskBlock
{
    public Guid Id { get; init; }

    public DateTimeOffset StartDate { get; set; }

    public DateTimeOffset EndDate { get; set; }

    public bool IsFixed { get; set; }

}
