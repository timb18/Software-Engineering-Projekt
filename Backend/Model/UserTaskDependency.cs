using System;

namespace Model;

public class UserTaskDependency
{
    public Guid TaskId {get; set;}

    public Guid DependsOnTaskId {get; set;}

}
