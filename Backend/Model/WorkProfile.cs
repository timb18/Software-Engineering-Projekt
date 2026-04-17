using System;

namespace Model;

public class WorkProfile
{
    public Guid Id {get; set;}

    public Guid UserId {get; set;}

    public TimeSpan MaxDailyLoad {get; set;}

}
