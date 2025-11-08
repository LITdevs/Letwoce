using System.Net.Http.Headers;
using System.Text;
using Lettuce.Database.Models;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace Lettuce.Util;

public class EventNotifier(ILogger<EventNotifier> logger, HttpClient httpClient, IOptions<NotifierSettings> settings)
{
    public async void HandleEvent(Event e)
    {
        try
        {
            await httpClient.PostAsync(settings.Value.WebhookUri, new StringContent(JsonConvert.SerializeObject(new
            {
                content = e.EventText
            }), Encoding.UTF8, new MediaTypeHeaderValue("application/json")));
            logger.LogInformation("Event posted");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to handle event");
        }
    }
}