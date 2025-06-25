using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

using Microsoft.EntityFrameworkCore;

namespace GrzesioBot;

[Index(nameof(DiscordId), IsUnique = true)]
[Index(nameof(MinecraftUuid), IsUnique = true)]
public class AuthorizedUser
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required long DiscordId { get; set; }

    public required string MinecraftUuid { get; set; }
}
