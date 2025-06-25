using System.Diagnostics.CodeAnalysis;

namespace GrzesioBot;

public class MessageReceivePayload
{
    public required string MinecraftAuthorUuid { get; set; }

    public required string MinecraftAuthorName { get; set; }

    public required string Content { get; set; }
}

public class MessageSendPayload
{
    public required string MinecraftAuthorUuid { get; set; }

    public required string Content { get; set; }

    public required IEnumerable<AttachmentInfo> Attachments { get; set; }

    [method: SetsRequiredMembers]
    public class AttachmentInfo(string name, string url)
    {
        public required string Name { get; set; } = name;

        public required string Url { get; set; } = url;
    }
}
