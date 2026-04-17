using System;

namespace Model;

public class WorkProfile
{
    public Guid Id {get; init;}

    public Guid UserId {get; set;}

    public TimeSpan MaxDailyLoad {get; set;}

}
