using Lettuce.Database;
using Lettuce.Database.Models;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;

namespace Lettuce.Pages;

public class AdminModel(PgContext pg) : PageModel
{
    public Pawn[] Pawns { get; set; }
    public Pawn? OwnPawn { get; set; }
    public bool IsPlayer => OwnPawn != null;
    
    public async Task OnGetAsync()
    {
        var hasPawn = Guid.TryParse(User.GetClaim("pawnId"), out var pawnId); 
        if (hasPawn)
        {
            var pawn = await pg.Pawns.FirstOrDefaultAsync(p => p.Id == pawnId);
            OwnPawn = pawn;
        }

        Pawns = await pg.Pawns.ToArrayAsync();
    }
}