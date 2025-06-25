namespace GrzesioBot;

public class Config
{
    public AuthorizeConfig Authorize { get; set; } = new();

    public ConnectionConfig Connection { get; set; } = new();

    public StorageConfig Storage { get; set; } = new();

    public required GuildConfig Guild { get; set; }

    public class AuthorizeConfig
    {
        public string MessageContent { get; set; } = "# Authorize\nConnect your Minecraft account.";

        public string ButtonText { get; set; } = "Authorize";

        public int MessagePrimaryColor { get; set; } = 0xFF7777;

        public string MessageInteractionResponse { get; set; } = "The authorize message has been created.";

        public string ModalTitle { get; set; } = "Authorize";

        public string ModalCodeInputLabel { get; set; } = "Code";

        public int? ModalCodeInputMinLength { get; set; }

        public int? ModalCodeInputMaxLength { get; set; }

        public string ModalCodeInputPlaceholder { get; set; } = "Enter your code here";

        public string AuthorizationFailureMessage { get; set; } = "An error occurred while trying to authorize your account. Please try again later.";

        public string AuthorizationFailureInvalidCodeMessage { get; set; } = "The code you entered is invalid. Please try again.";

        public string AuthorizationSuccessMessage { get; set; } = "Your account has been successfully authorized.";
    }

    public class ConnectionConfig
    {
        public string BaseUrl { get; set; } = "http://localhost:8080";
    }

    public class StorageConfig
    {
        public string DatabaseFilePath { get; set; } = "GrzesioBot.db";
    }

    public class GuildConfig
    {
        public required ulong GuildId { get; set; }

        public required ulong ServerChannelId { get; set; }

        public required ulong PlayerRoleId { get; set; }
    }
}
