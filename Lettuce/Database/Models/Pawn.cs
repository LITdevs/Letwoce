using System.ComponentModel.DataAnnotations;
using System.Drawing;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;

namespace Lettuce.Database.Models;

[PrimaryKey(nameof(Id))]
public class Pawn
{
    public Guid Id { get; init; }
    [MaxLength(32)]
    public required string DiscordId { get; set; }
    [MaxLength(1024)]
    public required string DisplayName { get; set; }
    public int X { get; set; } = 0;
    public int Y { get; set; } = 0;
    public bool Alive => Health > 0;
    public int Health { get; set; } = 3;
    public int Actions { get; set; } = 0;
    public Color Color { get; set; }
    public DateTimeOffset? KilledAt { get; set; }
    public Guid? KilledById { get; set; }
    
    [System.Text.Json.Serialization.JsonIgnore] [JsonIgnore]
    public Guid? Vote { get; set; }
    
    [MaxLength(1024)]
    public string? AvatarUri { get; set; }

    public bool IsAdmin { get; set; } = false;
    [System.Text.Json.Serialization.JsonIgnore] [JsonIgnore]
    public Pawn? KilledBy { get; set; } = null!;
}