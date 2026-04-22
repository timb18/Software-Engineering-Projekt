namespace DataAccess.Models;

public class TaskDependency
{
    public Guid TaskId { get; set; }

    public Guid DependsOnTaskId { get; set; }

    public virtual UserTask DependsOnTask { get; set; } = null!;

    public virtual UserTask Task { get; set; } = null!;
}