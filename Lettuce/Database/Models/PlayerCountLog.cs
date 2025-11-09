using System.ComponentModel.DataAnnotations;
using System.Drawing;
using Microsoft.EntityFrameworkCore;

namespace Lettuce.Database.Models;

[PrimaryKey(nameof(Id))]
public class PlayerCountLog
{
    public Guid Id { get; init; }
    public int PlayersOnline { get; set; }
    public DateTimeOffset Timestamp { get; set; }
}