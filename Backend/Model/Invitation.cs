using System;

namespace Model;

public class Invitation
{
    public Guid Id {get; set;}

    public string Email {get; set;}

    public string InviteCode {get; set;}
}
