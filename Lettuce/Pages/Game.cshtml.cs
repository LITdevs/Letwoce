using Lettuce.Database;
using Lettuce.Database.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Lettuce.Pages;

public class GameModel(PgContext pg) : PageModel
{
    public Pawn? OwnPawn { get; set; }
    public bool IsPlayer => OwnPawn != null;
    public required DateTimeOffset FirstEventTime { get; set; }
    
    public async Task OnGetAsync()
    {
        var hasPawn = Guid.TryParse(User.GetClaim("pawnId"), out var pawnId); 
        if (hasPawn)
        {
            var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
            OwnPawn = pawn;
        }

        var firstEvent = await pg.Events.OrderBy(e => e.Timestamp).FirstOrDefaultAsync();
        FirstEventTime = firstEvent?.Timestamp ?? DateTimeOffset.UtcNow;
    }
}