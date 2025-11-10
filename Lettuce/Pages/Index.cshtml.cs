using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Lettuce.Pages;

public class IndexModel(PgContext pg, VoteService voteSvc) : PageModel
{
    public VoteData[]? LastVote { get; set; }
    public bool VoteHeld { get; set; } = false;
    
    public Pawn? OwnPawn { get; set; }
    public bool IsPlayer => OwnPawn != null;
    
    public async Task<IActionResult> OnGetAsync()
    {
        var hasPawn = Guid.TryParse(User.GetClaim("pawnId"), out var pawnId); 
        if (hasPawn)
        {
            var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
            OwnPawn = pawn;
        }
        
        if (!await pg.Votes.AnyAsync()) return Page();
        VoteHeld = true;
        LastVote = await voteSvc.GetVotes();
        return Page();
    }


}