namespace GrzesioBot;

public interface IMinecraftAvatarUrlProvider
{
    public string GetAvatarUrl(string uuid);
}

public class CraftheadAvatarUrlProvider : IMinecraftAvatarUrlProvider
{
    public string GetAvatarUrl(string uuid)
    {
        return $"https://crafthead.net/avatar/{uuid}";
    }
}
