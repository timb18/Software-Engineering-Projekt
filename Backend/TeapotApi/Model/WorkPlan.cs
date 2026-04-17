namespace Model;

public record WorkPlan

{
    public Guid Id { get; init; }

    public Guid UserId { get; set; }

}
