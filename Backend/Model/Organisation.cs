using System;

namespace Model;

public class Organisation
{
    public Guid Id {get; set;}

    public string Name {get; set;}

    public string Description {get; set;}

    public int InvitationQuota  {get; set;}

}
