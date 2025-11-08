using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Hubs;
using Lettuce.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Lettuce.Jobs;

public class LettuceDropJob(PgContext pg, ILogger<LettuceDropJob> logger, IHubContext<LettuceHub> hub, EventNotifier en) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        var pawns = await pg.Pawns.Where(p => p.Id != Guid.AllBitsSet).ToArrayAsync();
        foreach (var pawn in pawns)
        {
            pawn.Actions++;
        }

        var e = new Event
        {
            ActionById = Guid.AllBitsSet,
            ActionToId = Guid.AllBitsSet,
            EventText = "Lettuce drop",
            NewX = 0,
            NewY = 0,
            OldX = 0,
            OldY = 0,
            LettuceCount = 1,
            Died = false,
            ActionType = ActionType.LettuceDrop
        };

        pg.Add(e);
        
        await hub.Clients.All.SendAsync("LettuceDrop", 1);
        await pg.SaveChangesAsync();
        
        en.HandleEvent(e);
        
        logger.LogInformation("Gave 1 lettuce to {Count} pawns", pawns.Length);
    }
}