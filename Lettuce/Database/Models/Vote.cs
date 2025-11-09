using System.ComponentModel.DataAnnotations;
using System.Drawing;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Database.Models;

[PrimaryKey(nameof(Id), nameof(VoterId))]
public class Vote
{
    public Guid Id { get; init; } // PK id of the vote, same for all rows in the same vote
    public Guid VoterId { get; set; } // PK who voted
    public Guid DropId { get; set; } // ID of associated lettuce drop event
    public Guid VoteeId { get; set; }

    public Pawn Voter { get; set; } = null!;
    public Pawn Votee { get; set; } = null!;
    public Event Drop { get; set; } = null!;
}