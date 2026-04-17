namespace Model;

public record UserTaskDependency
{
    public Guid TaskId { get; set; }

    public Guid DependsOnTaskId { get; set; }

}
