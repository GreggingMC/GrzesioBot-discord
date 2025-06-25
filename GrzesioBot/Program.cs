using GrzesioBot;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NetCord;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;
using NetCord.Hosting.Services.ApplicationCommands;
using NetCord.Hosting.Services.ComponentInteractions;
using NetCord.Rest;
using NetCord.Services.ApplicationCommands;
using NetCord.Services.ComponentInteractions;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services
    .AddSingleton<IWebhookProvider, WebhookProvider>()
    .AddSingleton<IMinecraftAvatarUrlProvider, CraftheadAvatarUrlProvider>()
    .AddDbContext<StorageContext>((services, builder) =>
    {
        var storageConfig = services.GetRequiredService<IOptions<Config>>().Value.Storage;

        var fullPath = Path.GetFullPath(storageConfig.DatabaseFilePath);

        builder.UseSqlite($"Data Source={fullPath}");
    })
    .AddHttpClient()
    .AddDiscordGateway(options =>
    {
        options.Intents = GatewayIntents.Guilds | GatewayIntents.GuildUsers | GatewayIntents.GuildMessages | GatewayIntents.MessageContent;
    })
    .AddApplicationCommands(o =>
    {
        o.LocalizationsProvider = new JsonLocalizationsProvider();
    })
    .AddComponentInteractions()
    .AddGatewayHandler(GatewayEvent.MessageCreate, async (IOptions<Config> config,
                                                          IServiceProvider services,
                                                          IHttpClientFactory httpClientFactory,
                                                          ILogger<IMessageCreateGatewayHandler> logger,
                                                          Message message) =>
    {
        var guildConfig = config.Value.Guild;
        if (message.ChannelId != guildConfig.ServerChannelId)
            return;

        var connectionConfig = config.Value.Connection;

        await using var scope = services.CreateAsyncScope();

        var storageContext = scope.ServiceProvider.GetRequiredService<StorageContext>();

        var dbAuthorId = (long)message.Author.Id;
        var savedUser = await storageContext.Users.FirstOrDefaultAsync(u => u.DiscordId == dbAuthorId);

        if (savedUser is null)
        {
            logger.LogInformation("Received a message from an unauthorized user: {}", dbAuthorId);
            return;
        }

        MessageSendPayload payload = new()
        {
            MinecraftAuthorUuid = savedUser.MinecraftUuid,
            Content = message.Content,
            Attachments = message.Attachments.Select(a => new MessageSendPayload.AttachmentInfo(a.FileName, a.ProxyUrl)),
        };

        using var httpClient = httpClientFactory.CreateClient();

        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsJsonAsync($"{connectionConfig.BaseUrl}/message", payload);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send a message to the server");
            return;
        }

        if (!response.IsSuccessStatusCode)
            logger.LogError("The server responded with an error: {}", response.StatusCode);
    })
    .AddOptions<Config>()
    .BindConfiguration("Config");

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var storageContext = scope.ServiceProvider.GetRequiredService<StorageContext>();
    await storageContext.Database.MigrateAsync();
}

app.AddSlashCommand("create-authorize-message", "Create the message with an authorize button", async (IOptions<Config> options, ApplicationCommandContext context) =>
{
    var authConfig = options.Value.Authorize;

    MessageProperties message = new()
    {
        Components = [
            new ComponentContainerProperties()
            {
                new TextDisplayProperties(authConfig.MessageContent),
                new ActionRowProperties()
                {
                    new ButtonProperties("authorize", authConfig.ButtonText, ButtonStyle.Primary),
                },
            }
            .WithAccentColor(new(authConfig.MessagePrimaryColor)),
        ],
        Flags = MessageFlags.IsComponentsV2,
    };

    await context.Channel.SendMessageAsync(message);

    return new InteractionMessageProperties()
        .WithContent(authConfig.MessageInteractionResponse)
        .WithFlags(MessageFlags.Ephemeral);
}, Permissions.Administrator);

app.AddComponentInteraction("authorize", (IOptions<Config> options, ComponentInteractionContext context) =>
{
    var authConfig = options.Value.Authorize;

    ModalProperties modal = new("authorize-submit", authConfig.ModalTitle)
    {
        new TextInputProperties("code", TextInputStyle.Short, authConfig.ModalCodeInputLabel)
        {
            MinLength = authConfig.ModalCodeInputMinLength,
            MaxLength = authConfig.ModalCodeInputMaxLength,
            Placeholder = authConfig.ModalCodeInputPlaceholder,
        },
    };

    return InteractionCallback.Modal(modal);
});

app.AddComponentInteraction("authorize-submit", async (IOptions<Config> options,
                                                       IHttpClientFactory httpClientFactory,
                                                       ILogger<IComponentInteractionService> logger,
                                                       StorageContext storageContext,
                                                       RestClient client,
                                                       ComponentInteractionContext context) =>
{
    var connectionConfig = options.Value.Connection;

    var authConfig = options.Value.Authorize;

    var guildConfig = options.Value.Guild;

    var modalInteraction = (ModalInteraction)context.Interaction;

    var codeInput = (TextInput)modalInteraction.Data.Components[0];

    using var httpClient = httpClientFactory.CreateClient();

    AuthorizeSendPayload payload = new()
    {
        UserId = context.User.Id,
        Code = codeInput.Value,
    };

    await modalInteraction.SendResponseAsync(InteractionCallback.DeferredMessage(MessageFlags.Ephemeral));

    HttpResponseMessage responseMessage;
    try
    {
        responseMessage = await httpClient.PostAsJsonAsync($"{connectionConfig.BaseUrl}/authorize", payload);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send authorization request");

        await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationFailureMessage);
        return;
    }

    if (!responseMessage.IsSuccessStatusCode)
    {
        logger.LogError("Authorization request failed with status code {}", responseMessage.StatusCode);

        await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationFailureMessage);
        return;
    }

    AuthorizeReceivePayload receivePayload;
    try
    {
        receivePayload = (await responseMessage.Content.ReadFromJsonAsync<AuthorizeReceivePayload>())!;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to read authorization response");

        await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationFailureMessage);
        return;
    }

    try
    {
        var dbUserId = (long)receivePayload.UserId;
        var minecraftUuid = receivePayload.MinecraftUuid;

        var savedUser = await storageContext.Users.FirstOrDefaultAsync(u => u.DiscordId == dbUserId);
        if (savedUser is null)
        {
            await storageContext.Users.AddAsync(new AuthorizedUser
            {
                DiscordId = dbUserId,
                MinecraftUuid = minecraftUuid,
            });

            await storageContext.SaveChangesAsync();
        }
        else if (savedUser.MinecraftUuid != minecraftUuid)
        {
            savedUser.MinecraftUuid = minecraftUuid;

            await storageContext.SaveChangesAsync();
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to save an authorized user to database");

        await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationFailureMessage);
        return;
    }

    try
    {
        await client.AddGuildUserRoleAsync(guildConfig.GuildId, receivePayload.UserId, guildConfig.PlayerRoleId);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to assign a player user role to {}", receivePayload.UserId);

        await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationFailureMessage);
        return;
    }

    await modalInteraction.SendFollowupMessageAsync(authConfig.AuthorizationSuccessMessage);
});

app.UseGatewayHandlers();

app.MapPost("/message", async (StorageContext storageContext,
                               IWebhookProvider webhookProvider,
                               GatewayClient client,
                               IOptions<Config> config,
                               IMinecraftAvatarUrlProvider avatarUrlProvider,
                               HttpContext context) =>
{
    var guildConfig = config.Value.Guild;

    var message = (await context.Request.ReadFromJsonAsync<MessageReceivePayload>())!;

    var uuid = message.MinecraftAuthorUuid;

    var webhook = await webhookProvider.GetAsync();

    var (displayName, avatarUrl) = await GetUserPropertiesAsync();

    await webhook.ExecuteAsync(new()
    {
        Content = message.Content,
        Username = displayName,
        AvatarUrl = avatarUrl,
    });

    async Task<(string DisplayName, string AvatarUrl)> GetUserPropertiesAsync()
    {
        var savedUser = await storageContext.Users.FirstOrDefaultAsync(u => u.MinecraftUuid == uuid);

        string displayName;
        string avatarUrl;
        if (savedUser is null)
        {
            displayName = message.MinecraftAuthorName;
            avatarUrl = avatarUrlProvider.GetAvatarUrl(uuid);
        }
        else
        {
            var userId = (ulong)savedUser.DiscordId;
            var guildId = guildConfig.GuildId;

            if (!client.Cache.Guilds.TryGetValue(guildId, out var guild) || !guild.Users.TryGetValue(userId, out var guildUser))
            {
                try
                {
                    guildUser = await client.Rest.GetGuildUserAsync(guildId, userId);
                }
                catch (RestException ex) when (ex.StatusCode is System.Net.HttpStatusCode.NotFound)
                {
                    storageContext.Users.Remove(savedUser);

                    await storageContext.SaveChangesAsync();

                    try
                    {
                        var user = await client.Rest.GetUserAsync(userId);
                        displayName = user.GlobalName ?? user.Username;
                        avatarUrl = (user.GetAvatarUrl() ?? user.DefaultAvatarUrl).ToString();
                    }
                    catch (RestException ex2) when (ex2.StatusCode is System.Net.HttpStatusCode.NotFound)
                    {
                        displayName = message.MinecraftAuthorName;
                        avatarUrl = avatarUrlProvider.GetAvatarUrl(uuid);
                    }

                    return (displayName, avatarUrl);
                }
            }

            displayName = guildUser.Nickname ?? guildUser.GlobalName ?? guildUser.Username;
            avatarUrl = (guildUser.GetGuildAvatarUrl() ?? guildUser.GetAvatarUrl() ?? guildUser.DefaultAvatarUrl).ToString();
        }

        return (displayName, avatarUrl);
    }
});

await app.RunAsync();
