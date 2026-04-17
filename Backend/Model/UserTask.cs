using System;

namespace Model;

public class UserTask
{
    public Guid Id {get; set;}

    public string Name {get; set;}

    public string Description {get; set;}

    public TimeSpan TimeEstimnate {get; set;}

    public DateTime Deadline {get; set;}

    public Priority Priority {get; set;}

    public Status Status {get; set;}
}
