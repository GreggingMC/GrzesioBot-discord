using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GrzesioBot;

public class ChannelWebhook
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required long ChannelId { get; set; }

    public required long WebhookId { get; set; }

    public required string WebhookToken { get; set; }
}
