namespace GrzesioBot;

public class AuthorizeSendPayload
{
    public required ulong UserId { get; set; }

    public required string Code { get; set; }
}

public class AuthorizeReceivePayload
{
    public required ulong UserId { get; set; }

    public required string MinecraftUuid { get; set; }
}
