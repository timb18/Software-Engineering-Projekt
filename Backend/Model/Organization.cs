using System;

namespace Model;

public class Organization
{
    public Guid Id {get; set;}

    public string Name {get; set;}

    public string Description {get; set;}

    public int InvitationQuota  {get; set;}

}
