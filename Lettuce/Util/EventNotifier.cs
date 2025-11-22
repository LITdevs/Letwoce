using System.Net.Http.Headers;
using System.Text;
using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Hubs;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using PuppeteerSharp;

namespace Lettuce.Util;

public class EventNotifier(ILogger<EventNotifier> logger, IServiceProvider sp, IOptions<NotifierSettings> settings)
{
    private static readonly Dictionary<Guid, MoveTrackerData> MoveTracker = new();
    private static readonly Lock MoveTrackerLock = new();
    private static readonly HttpClient HttpClient = new();

    public async void HandleEvent(Event e)
    {
        using var scope = sp.CreateScope();
        if (e.ActionType != ActionType.DiscordOnly)
        {
            var hub = scope.ServiceProvider.GetRequiredService<IHubContext<LettuceHub>>();
            await hub.Clients.All.SendAsync("NewEvent", new EventDto
            {
                Timestamp = e.Timestamp,
                Id = e.Id,
                ActionById = e.ActionById,
                ActionToId = e.ActionToId,
                EventText = e.EventText,
                ActionType = e.ActionType
            });
        }
        var pg = scope.ServiceProvider.GetRequiredService<PgContext>();
        var actionBy = await pg.Pawns.FirstAsync(p => p.Id == e.ActionById);
        try
        {
            // Horrors ahead
            if (e.ActionType != ActionType.Move) goto Continue;
            lock (MoveTrackerLock)
            {
                var newTimer = new Timer(HandleTrackerExpiry, e.ActionById, TimeSpan.FromSeconds(15), Timeout.InfiniteTimeSpan);
                if (MoveTracker.TryGetValue(e.ActionById, out var tracker))
                {
                    tracker.ResetTimer.Dispose();
                    tracker.ResetTimer = newTimer;
                    tracker.EndX = e.NewX;
                    tracker.EndY = e.NewY;
                }
                else
                {
                    tracker = new MoveTrackerData()
                    {
                        StartX = e.OldX,
                        StartY = e.OldY,
                        DisplayName = actionBy.DisplayName, // I don't trust e.ActionBy to be set
                        EndX = e.NewX,
                        EndY = e.NewY,
                        Id = e.ActionById,
                        ResetTimer = newTimer
                    };
                    MoveTracker[e.ActionById] = tracker;
                }
            }

            Continue:

            // Horrors over, carry on
            
            await HttpClient.PostAsync(settings.Value.WebhookUri, new StringContent(JsonConvert.SerializeObject(new
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

    private async void HandleTrackerExpiry(object? state)
    {
        try
        {
            MoveTrackerData trackerData;
            lock (MoveTrackerLock)
            {
                trackerData = MoveTracker[(Guid)state!];
                MoveTracker.Remove((Guid)state);
            }
        
            await HttpClient.PostAsync(settings.Value.WebhookUri, 
                new StringContent(JsonConvert.SerializeObject(new
                {
                    embeds = new object[]
                    {
                        new
                        {
                            title = $"{trackerData.DisplayName} has moved from {trackerData.StartX}, {trackerData.StartY} to {trackerData.EndX}, {trackerData.EndY}",
                            image = new
                            {
                                url = $"{Program.BaseUrl}/Preview?arrowFrom={trackerData.StartX},{trackerData.StartY}&arrowTo={trackerData.EndX},{trackerData.EndY}"
                            }
                        }
                    }
                }), Encoding.UTF8, "application/json"));
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to send MoveTracker summary");
        }
    }
}

public record MoveTrackerData
{
    public DateTimeOffset LastMove;
    public Guid Id;
    public required string DisplayName;
    public int StartX;
    public int StartY;
    public int EndX;
    public int EndY;
    public required Timer ResetTimer;
}