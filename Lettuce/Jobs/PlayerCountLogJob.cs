using Lettuce.Database;
using Lettuce.Database.Models;
using Lettuce.Hubs;
using Lettuce.Util;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Lettuce.Jobs;

public class PlayerCountLogJob(PgContext pg) : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        pg.Add(new PlayerCountLog
        {
            PlayersOnline = LettuceHub.ConnectionCount,
            Timestamp = DateTimeOffset.Now
        });
        
        await pg.SaveChangesAsync();
    }
}