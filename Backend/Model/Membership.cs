using System;

namespace Model;

public class Membership
{
    public Guid UserId {get; set;}

    public Guid OrganisationId {get; set;}

    public Role Role {get; set;}

}
