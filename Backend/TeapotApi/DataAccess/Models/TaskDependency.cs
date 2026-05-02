using System;
using System.Collections.Generic;

namespace DataAccess.Models;

public partial class TaskDependency
{
    public Guid TaskId { get; set; }

    public Guid DependsOnTaskId { get; set; }

    public virtual UserTask DependsOnTask { get; set; } = null!;

    public virtual UserTask Task { get; set; } = null!;
}
