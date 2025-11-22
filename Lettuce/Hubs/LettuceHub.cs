using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Security.Claims;
using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Util;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Quartz;

namespace Lettuce.Hubs;

public class LettuceHub : Hub
{
    public static int ConnectionCount => ConnectedPlayers.Distinct().Count();
    public static List<string> ConnectedPlayers = [];
    public override async Task OnConnectedAsync()
    {
        if (Context.User?.GetClaim("pawnId") != null)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "Players");
            ConnectedPlayers.Add(Context.User.GetClaim(ClaimTypes.NameIdentifier)!);
            await Clients.All.SendAsync("UpdateLiveCount", ConnectionCount);
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Context.User?.GetClaim("pawnId") != null)
        {
            ConnectedPlayers.Remove(Context.User.GetClaim(ClaimTypes.NameIdentifier)!);
            await Clients.All.SendAsync("UpdateLiveCount", ConnectionCount);
        }
        await base.OnDisconnectedAsync(exception);
    }


    public async Task<Pawn[]> GetPawns(PgContext pg)
    {
        var pawns = await pg.Pawns.AsNoTracking().ToArrayAsync();
        return pawns;
    }

    public Task<int> GetLiveCount()
    {
        return Task.FromResult(ConnectionCount);
    }

    [Authorize]
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public async Task<bool> MoveTo(PgContext pg, ILogger<LettuceHub> logger, EventNotifier en, int x, int y)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        logger.LogInformation("Pawn ID: {Pawn} is moving to {X}, {Y}", pawnId, x, y);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null)
        {
            logger.LogInformation("Rejecting move due to pawn not found");
            return false;
        }

        if (!pawn.Alive)
        {
            logger.LogInformation("Rejecting move due to dead");
            return false;
        }
        
        var ΔX = Math.Abs(pawn.X - x);
        var ΔY = Math.Abs(pawn.Y - y);
        if (ΔY > 1 || ΔX > 1)
        {
            logger.LogInformation("Rejecting move due to high delta");
            return false;
        }

        if (x < 0 || x >= Program.GridWidth || y < 0 || y >= Program.GridHeight)
        {
            logger.LogInformation("Rejecting move due to out of bounds");
            return false;
        }

        if (pawn.Actions < 1)
        {
            logger.LogInformation("Rejecting move due to low action count");
            return false;
        }

        var otherPawn = await pg.Pawns.FirstOrDefaultAsync(p => p.X == x && p.Y == y);
        if (otherPawn != null)
        {
            logger.LogInformation("Rejecting move due to {PawnName} already occupying tile", otherPawn.DisplayName);
            return false;
        }

        var e = new Event
        {
            ActionById = pawn.Id,
            ActionToId = pawn.Id,
            EventText = $"{pawn.DisplayName} moved to {x}, {y}",
            NewX = x,
            NewY = y,
            OldX = pawn.X,
            OldY = pawn.Y,
            LettuceCount = 1,
            ActionType = ActionType.Move,
        };
        
        pg.Add(e);

        pawn.X = x;
        pawn.Y = y;
        pawn.Actions--;

        await pg.SaveChangesAsync();

        en.HandleEvent(e);

        await Clients.All.SendAsync("MoveTo", pawnId, x, y, pawn.Actions);
        return true;
    }

    [Authorize]
    public async Task<bool> Attack(PgContext pg, ILogger<LettuceHub> logger, EventNotifier en, Guid attackedPawnId)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        logger.LogInformation("Pawn {Pawn} is attacking {Attacked}", pawnId, attackedPawnId);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null)
        {
            logger.LogInformation("Rejecting attack due to attacker pawn not found");
            return false;
        }

        var attackedPawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == attackedPawnId);
        if (attackedPawn == null)
        {
            logger.LogInformation("Rejecting attack due to attacked pawn not found");
            return false;
        }
        
        
        var ΔX = Math.Abs(pawn.X - attackedPawn.X);
        var ΔY = Math.Abs(pawn.Y - attackedPawn.Y);
        var attackDistance = double.Hypot(ΔX, ΔY); // funny method to do sqrt(ΔX² + ΔY²) at 33% of the speed
        if (attackDistance > 4.25d)
        {
            logger.LogInformation("Rejecting attack due to high distance ({Distance})", attackDistance);
            return false;
        }

        // Stupidity checks
        if (attackedPawn.Id == pawn.Id) return false;
        if (!attackedPawn.Alive) return false;
        if (!pawn.Alive) return false;

        if (pawn.Actions < 1)
        {
            logger.LogInformation("Rejecting attack due to not enough actions");
            return false;
        }

        pawn.Actions--;
        attackedPawn.Health--;
        var e = new Event
        {
            ActionById = pawn.Id,
            ActionToId = attackedPawn.Id,
            EventText = $"{pawn.DisplayName} {(attackedPawn.Alive ? "attacked" : "killed")} {attackedPawn.DisplayName}",
            LettuceCount = 1,
            Died = false,
            ActionType = ActionType.Attack,
            ActionBy = pawn,
            ActionTo = attackedPawn,
        };
        pg.Add(e);
        en.HandleEvent(e);
        await Clients.All.SendAsync("Attack", pawn.Id, attackedPawn.Id);
        if (!attackedPawn.Alive)
        {
            var e3 = new Event
            {
                ActionById = pawn.Id,
                ActionToId = attackedPawn.Id,
                EventText = $"<@{attackedPawn.DiscordId}> You have been killed. You may now vote in the Supreme Court of Lettuce to support a living fighter.",
                LettuceCount = 0,
                Died = false,
                ActionType = ActionType.DiscordOnly
            };
            en.HandleEvent(e3);
            var voters = await pg.Pawns.Where(p => p.Vote == attackedPawn.Id).ToArrayAsync();
            var pings = string.Join(", ", voters.Select(v => $"<@{v.DiscordId}>"));
            if (voters.Length > 0)
            {
                
                var e2 = new Event
                {
                    ActionById = pawn.Id,
                    ActionToId = attackedPawn.Id,
                    EventText = $"{pings} the fighter you voted for has been killed by {pawn.DisplayName}. Please choose a new fighter to support.",
                    LettuceCount = 0,
                    Died = false,
                    ActionType = ActionType.DiscordOnly
                };
                foreach (var voter in voters)
                {
                    voter.Vote = null;
                }
                en.HandleEvent(e2);
            }

            attackedPawn.KilledById = pawn.Id;
            attackedPawn.KilledAt = DateTimeOffset.UtcNow;

            var alivePawns = await pg.Pawns.CountAsync(p => p.Health > 0 && p.Id != Guid.AllBitsSet);
            logger.LogInformation("Alive pawns: {Alive}", alivePawns);
            if (alivePawns == 2) // The pawn just killed does not yet show up as dead
            {
                logger.LogInformation("winner winner chicken dinner");
                // damn they won
                var e4 = new Event
                {
                    ActionById = pawn.Id,
                    ActionToId = pawn.Id,
                    EventText = $"{pawn.DisplayName} wins the game.",
                    LettuceCount = 0,
                    Died = false,
                    ActionType = ActionType.WinnerWinnerChickenDinner
                };
                en.HandleEvent(e4);
                await Clients.All.SendAsync("Winner", pawn.Id);
            }
        }
        await pg.SaveChangesAsync();

        return true;
    }

    [Authorize]
    public async Task<bool> Gift(PgContext pg, ILogger<LettuceHub> logger, EventNotifier en, Guid giftedPawnId)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        logger.LogInformation("Pawn {Pawn} is gifting to {Gifted}", pawnId, giftedPawnId);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null)
        {
            logger.LogInformation("Rejecting gift due to gifter pawn not found");
            return false;
        }

        var giftedPawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == giftedPawnId);
        if (giftedPawn == null)
        {
            logger.LogInformation("Rejecting gift due to giftee pawn not found");
            return false;
        }
        
        
        var ΔX = Math.Abs(pawn.X - giftedPawn.X);
        var ΔY = Math.Abs(pawn.Y - giftedPawn.Y);
        var attackDistance = double.Hypot(ΔX, ΔY); // funny method to do sqrt(ΔX² + ΔY²) at 33% of the speed
        if (attackDistance > 7.5d)
        {
            logger.LogInformation("Rejecting gift due to high distance ({Distance})", attackDistance);
            return false;
        }

        // Stupidity checks
        if (giftedPawn.Id == pawn.Id) return false;
        if (!giftedPawn.Alive) return false;
        if (!pawn.Alive) return false;

        if (pawn.Actions < 1)
        {
            logger.LogInformation("Rejecting gift due to not enough actions");
            return false;
        }

        pawn.Actions--;
        giftedPawn.Actions++;
        var e = new Event
        {
            ActionById = pawn.Id,
            ActionToId = giftedPawn.Id,
            EventText = $"{pawn.DisplayName} gave lettuce to {giftedPawn.DisplayName}",
            LettuceCount = 1,
            Died = false,
            ActionType = ActionType.Gift,
            ActionBy = pawn,
            ActionTo = giftedPawn,
        };
        pg.Add(e);
        await pg.SaveChangesAsync();
        en.HandleEvent(e);
        
        await Clients.All.SendAsync("Gift", pawn.Id, giftedPawn.Id, 1);

        return true;
    }

    [Authorize]
    public async Task<bool> Speak(PgContext pg, EventNotifier en, string message)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.Alive) return false;
        message = message.Replace("@everyone", "im a loser");
        message = message.Replace("@here", "i like licking trees");
        message = message.Replace("@", "at");
        message = message.Replace("<", "&lt;");
        message = message.Replace(">", "&gt;");
        var e = new Event
        {
            ActionById = pawn.Id,
            ActionToId = pawn.Id,
            EventText = $"{pawn.DisplayName} says \"{message}\"",
            LettuceCount = 0,
            Died = false,
            ActionType = ActionType.Speak,
            ActionBy = pawn,
            ActionTo = pawn,
        };
        pg.Add(e);
        await pg.SaveChangesAsync();
        en.HandleEvent(e);
        await Clients.All.SendAsync("Speak", pawn.Id, message);
        return true;
    }

    [Authorize]
    public async Task<bool> Vote(PgContext pg, Guid vote)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        var votee = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == vote);
        if (votee == null) return false;
        if (pawn.Alive) return false; // Must be dead to vote
        if (!votee.Alive) return false; // Can't vote for corpses
        pawn.Vote = votee.Id;
        await pg.SaveChangesAsync();
        return true;
    }

    [Authorize]
    public async Task<bool> LDrop(PgContext pg, ISchedulerFactory factory)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;
        
        var jobKey = new JobKey("LettuceDrop");
        var allSchedulers = await factory.GetAllSchedulers();
        var scheduler = allSchedulers[0];
        await scheduler.TriggerJob(jobKey);
        return true;
    }

    [Authorize]
    public async Task<bool> RemovePawn(PgContext pg, Guid pawnToRemove)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;

        await pg.Pawns.Where(p => p.Id == pawnToRemove).ExecuteDeleteAsync();
        return true;
    }
    
    [Authorize]
    public async Task<bool> AddPawn(PgContext pg, string discordId, string initialName)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;

        pg.Add(new Pawn
        {
            DiscordId = discordId,
            DisplayName = initialName,
            X = Random.Shared.Next(0, Program.GridWidth),
            Y = Random.Shared.Next(0, Program.GridHeight),
            Health = 3,
            Actions = 0,
            Color = Color.FromArgb(255, Random.Shared.Next(0, 255), Random.Shared.Next(0, 255),
                Random.Shared.Next(0, 255)),
            KilledAt = null,
            KilledBy = null,
            Vote = null,
            AvatarUri = null,
            IsAdmin = false
        });
        await pg.SaveChangesAsync();
        return true;
    }
    
    [Authorize]
    public async Task<bool> Reset(PgContext pg)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;

        var pawns = await pg.Pawns.ToArrayAsync();
        
        foreach (var p in pawns)
        {
            if (p.Id != Guid.AllBitsSet)
            {
                p.X = Random.Shared.Next(0, Program.GridWidth);
                p.Y = Random.Shared.Next(0, Program.GridHeight);
                p.Actions = 0;
                p.Health = 3;
                p.Vote = null;
                p.KilledAt = null;
                p.KilledById = null;
                continue;
            }
            
            p.X = -8;
            p.Y = (int)Math.Floor(Program.GridHeight / 2d);
            p.Actions = int.MaxValue;
            p.Health = int.MaxValue;
            p.Vote = null;
            p.KilledAt = null;
            p.KilledById = null;
        }

        await pg.Events.ExecuteDeleteAsync();
        await pg.Votes.ExecuteDeleteAsync();

        await pg.SaveChangesAsync();
        return true;
    }

    [Authorize]
    public async Task<bool> ForceMove(PgContext pg, Guid pawnToMoveId, int x, int y)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;

        var pawnToMove = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnToMoveId);
        if (pawnToMove == null) return false;
        pawnToMove.X = x;
        pawnToMove.Y = y;
        await pg.SaveChangesAsync();
        return true;
    }

    
    [Authorize]
    public async Task<bool> SetLettuce(PgContext pg, Guid pawnToMoveId, int lettuce)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null) return false;
        if (!pawn.IsAdmin) return false;

        var pawnToMove = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnToMoveId);
        if (pawnToMove == null) return false;
        pawnToMove.Actions = lettuce;
        await pg.SaveChangesAsync();
        return true;
    }

    public async Task<VoteData[]> GetVoteData(VoteService voteSvc)
    {
        return await voteSvc.GetVotes();
    }

    public async Task<EventDto[]> GetEvents(PgContext pg)
    {
        return await pg.Events.AsNoTracking().OrderByDescending(e => e.Timestamp).Select(e =>
            new EventDto
            {
                Timestamp = e.Timestamp,
                Id = e.Id,
                ActionById = e.ActionById,
                ActionToId = e.ActionToId,
                EventText = e.EventText,
                ActionType = e.ActionType
            }).Take(50).ToArrayAsync();
    }
}

public record EventDto
{
    public DateTimeOffset Timestamp { get; set; }
    public Guid Id { get; set; }
    public required string EventText { get; set; }
    public required Guid ActionById { get; set; }
    public required Guid ActionToId { get; set; }
    public ActionType ActionType { get; set; }
}