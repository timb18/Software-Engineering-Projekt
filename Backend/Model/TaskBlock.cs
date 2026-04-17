using System;

namespace Model;

public class TaskBlock
{
    public Guid Id {get; set;}

    public DateTimeOffset StartDate {get; set;}

    public DateTimeOffset EndDate {get; set;}

    public bool IsFixed {get; set;}

}
