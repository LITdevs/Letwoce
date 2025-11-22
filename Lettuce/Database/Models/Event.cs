using System.ComponentModel.DataAnnotations;
using System.Drawing;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Database.Models;

[PrimaryKey(nameof(Id))]
public class Event
{
    public Guid Id { get; init; }
    public Guid ActionById { get; set; }
    public Guid ActionToId { get; set; }
    [MaxLength(1024)]
    public required string EventText { get; set; }
    public int NewX { get; set; } = 0;
    public int NewY { get; set; } = 0;
    public int OldX { get; set; } = 0;
    public int OldY { get; set; } = 0;
    public int LettuceCount { get; set; } = 0;
    public bool Died { get; set; } = false;
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public ActionType ActionType { get; set; }
    
    public Pawn ActionBy { get; set; } = null!;
    public Pawn ActionTo { get; set; } = null!;
    public Guid? ScolVoteId { get; set; } // If this event is related to a scol vote
}

public enum ActionType
{
    Move,
    Gift,
    Attack,
    LettuceDrop,
    Scol,
    Speak,
    WinnerWinnerChickenDinner,
    DiscordOnly
}