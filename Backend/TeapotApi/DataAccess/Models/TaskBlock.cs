namespace DataAccess.Models;

public class TaskBlock
{
    public Guid TaskId { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public bool IsFixed { get; set; }

    public virtual UserTask Task { get; set; } = null!;
}