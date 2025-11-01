using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Lettuce.Database;
using Lettuce.Database.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

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
    public async Task<bool> MoveTo(PgContext pg, ILogger<LettuceHub> logger, int x, int y)
    {
        var pawnId = Guid.Parse(Context.User!.GetClaim("pawnId")!);
        logger.LogInformation("Pawn ID: {Pawn} is moving to {X}, {Y}", pawnId, x, y);
        var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
        if (pawn == null)
        {
            logger.LogInformation("Rejecting move due to pawn not found");
            return false;
        }
        var ΔX = Math.Abs(pawn.X - x);
        var ΔY = Math.Abs(pawn.Y - y);
        if (ΔY > 1 || ΔX > 1)
        {
            logger.LogInformation("Rejecting move due to high delta");
            return false;
        }

        if (x < 0 || x > Program.GridWidth || y < 0 || y > Program.GridHeight)
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

        await Clients.All.SendAsync("MoveTo", pawnId, x, y, pawn.Actions);
        return true;
    }
}