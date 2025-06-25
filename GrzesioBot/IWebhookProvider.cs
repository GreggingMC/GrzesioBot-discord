using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using NetCord.Rest;

namespace GrzesioBot;

public interface IWebhookProvider
{
    public ValueTask<WebhookClient> GetAsync();
}

public class WebhookProvider(RestClient client, IOptions<Config> config, IServiceProvider services) : IWebhookProvider
{
    private WebhookClient? _cached;

    public async ValueTask<WebhookClient> GetAsync() => _cached ??= await CreateClientAsync();

    private async ValueTask<WebhookClient> CreateClientAsync()
    {
        var guildConfig = config.Value.Guild;

        await using var scope = services.CreateAsyncScope();

        var storageContext = scope.ServiceProvider.GetRequiredService<StorageContext>();
        var channelId = guildConfig.ServerChannelId;
        var dbChannelId = (long)guildConfig.ServerChannelId;

        var savedWebhook = await storageContext.Webhooks.FirstOrDefaultAsync(w => w.ChannelId == dbChannelId);

        if (savedWebhook is null)
            savedWebhook = await CreateAndSaveWebhookAsync();
        else
        {
            try
            {
                await client.GetWebhookWithTokenAsync(channelId, savedWebhook.WebhookToken);
            }
            catch
            {
                storageContext.Webhooks.Remove(savedWebhook);
                savedWebhook = await CreateAndSaveWebhookAsync();
            }
        }

        return new((ulong)savedWebhook.WebhookId, savedWebhook.WebhookToken, new()
        {
            Client = client,
        });

        async ValueTask<ChannelWebhook> CreateAndSaveWebhookAsync()
        {
            var createdWebhook = await client.CreateWebhookAsync(channelId, new("Wzium"));

            ChannelWebhook newWebhook = new()
            {
                ChannelId = (long)channelId,
                WebhookId = (long)createdWebhook.Id,
                WebhookToken = createdWebhook.Token,
            };

            await storageContext.Webhooks.AddAsync(newWebhook);

            await storageContext.SaveChangesAsync();

            return newWebhook;
        }
    }
}
