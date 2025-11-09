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
            if (!pawn.Alive) continue;
            pawn.Actions++;
        }

        var dropEvent = new Event
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

        pg.Add(dropEvent);
        
        await hub.Clients.All.SendAsync("LettuceDrop", 1);
        en.HandleEvent(dropEvent);

        var votes = await pg.Pawns.Where(p => p.Vote != null).Select(p => new { p.Id, p.Vote }).ToArrayAsync();
        var voteId = Guid.NewGuid();
        foreach (var vote in votes)
        {
            var v = new Vote
            {
                Id = voteId,
                VoterId = vote.Id,
                DropId = dropEvent.Id,
                VoteeId = vote.Vote!.Value,
            };
            pg.Add(v);
        }

        var finalDrops = new Dictionary<Guid, (int votes, int lettuce)>();
        var groupedVotes = votes.GroupBy(v => v.Vote!.Value);
        foreach (var groupedVote in groupedVotes)
        {
            if (groupedVote.Key == Guid.AllBitsSet) continue; //how..
            var voteCount = groupedVote.Count();
            if (votes.Length < 3)
            {
                finalDrops[groupedVote.Key] = (voteCount, 1);
                continue;
            }

            var lettuceCount = (int)Math.Floor(voteCount / 3d);
            if (lettuceCount < 1) continue;
            finalDrops[groupedVote.Key] = (voteCount, lettuceCount);
        }
        
        foreach (var drop in finalDrops)
        {
            var pawn = pawns.FirstOrDefault(p => p.Id == drop.Key);
            if (pawn == null)
            {
                logger.LogWarning("Votee {Vote} not found in pawns", drop.Key);
                continue;
            }
            pawn.Actions += drop.Value.lettuce;
            var scolEvent = new Event
            {
                ActionById = Guid.AllBitsSet,
                ActionToId = pawn.Id,
                EventText = $"{pawn.DisplayName} received {drop.Value.votes} votes from the Supreme Court of Lettuce and has been awarded {drop.Value.lettuce} lettuce.",
                NewX = 0,
                NewY = 0,
                OldX = 0,
                OldY = 0,
                LettuceCount = drop.Value.lettuce,
                Died = false,
                ActionType = ActionType.Scol,
                ScolVoteId = voteId
            };
            await hub.Clients.All.SendAsync("Gift", Guid.AllBitsSet, pawn.Id, drop.Value.lettuce);
            pg.Add(scolEvent);
            en.HandleEvent(scolEvent);
        }
        
        await pg.SaveChangesAsync();
        
        logger.LogInformation("Gave 1 lettuce to {Count} pawns", pawns.Length);
    }
}