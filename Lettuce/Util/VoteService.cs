using Lettuce.Database;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Util;

public class VoteService(PgContext pg)
{
    public async Task<VoteData[]> GetVotes()
    {
        var voteData = new List<VoteData>();
        var voteIds = await pg.Votes.OrderByDescending(v => v.VoteTime).Select(v => v.Id).Distinct().ToArrayAsync();
        var allVotes = await pg.Votes.Include(v => v.Votee).OrderByDescending(v => v.VoteTime).ToArrayAsync();
        foreach (var voteId in voteIds)
        {
            var votes = allVotes.Where(v => v.Id == voteId).ToArray();
            var cVotes = new Dictionary<string, object>();
            var groupedVotes = votes.GroupBy(v => v.VoteeId);
            foreach (var groupedVote in groupedVotes)
            {
                var color = groupedVote.First().Votee.Color;
                cVotes[groupedVote.First().Votee.DisplayName] = new
                {
                    votes = groupedVote.Count(),
                    color1 = $"rgba({color.R}, {color.G}, {color.B}, 0.2)",
                    color2 = $"rgb({color.R}, {color.G}, {color.B})"
                };
            }
            voteData.Add(new VoteData
            {
                Timestamp = votes.First().VoteTime,
                Votes = cVotes
            });
        }

        return voteData.ToArray();
    }
}

public record VoteData
{
    public required DateTimeOffset Timestamp { get; set; }
    public required Dictionary<string, object> Votes { get; set; }
}