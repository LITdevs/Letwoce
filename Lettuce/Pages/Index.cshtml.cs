using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Util;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Pages;

public class IndexModel(PgContext pg, ILogger<IndexModel> logger, VoteService voteSvc) : PageModel
{
    public VoteData[] LastVote { get; set; }
    public bool VoteHeld { get; set; } = false;
    
    public async Task<IActionResult> OnGetAsync()
    {
        if (!await pg.Votes.AnyAsync()) return Page();
        VoteHeld = true;
        LastVote = await voteSvc.GetVotes();
        return Page();
    }


}