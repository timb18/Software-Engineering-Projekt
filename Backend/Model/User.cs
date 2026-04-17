using System;

namespace Model;

public class User
{
    public Guid Id { get; set; }

    public string UserName { get; set; }

    public string Email { get; set; }

    public DateTimeOffset CreatedAt {get; set;}

    public DateTimeOffset UpdatedAt {get; set;}

}
