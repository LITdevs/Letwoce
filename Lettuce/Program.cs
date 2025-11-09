using Lettuce.Database;
using Lettuce.Hubs;
using Lettuce.Jobs;
using Lettuce.Util;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Lettuce;

public class Program
{
    public static int GridWidth = 1;
    public static int GridHeight = 1;
    
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        builder.Services.AddRazorPages();

        builder.Services.AddDbContext<PgContext>(opt =>
        {
            opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres"));
            opt.UseOpenIddict();
        });
        
        builder.Services.AddDataProtection()
            .PersistKeysToDbContext<PgContext>();
        
        builder.Services.AddSignalR();
        GridWidth = builder.Configuration.GetValue<int>("Lettuce:GridWidth", 35); 
        GridHeight = builder.Configuration.GetValue<int>("Lettuce:GridHeight", 25); 
        builder.Services.AddDistributedPostgresCache(options =>
        {
            options.ConnectionString = builder.Configuration.GetConnectionString("Postgres");
            options.SchemaName = builder.Configuration.GetValue<string>("PostgresCache:SchemaName", "public");
            options.TableName = builder.Configuration.GetValue<string>("PostgresCache:TableName", "DistributedCache");
            options.CreateIfNotExists = builder.Configuration.GetValue<bool>("PostgresCache:CreateIfNotExists", true);
            options.UseWAL = builder.Configuration.GetValue<bool>("PostgresCache:UseWAL", false);
            
            var expirationInterval = builder.Configuration.GetValue<string>("PostgresCache:ExpiredItemsDeletionInterval");
            if (!string.IsNullOrEmpty(expirationInterval) && TimeSpan.TryParse(expirationInterval, out var interval)) {
                options.ExpiredItemsDeletionInterval = interval;
            }

            var slidingExpiration = builder.Configuration.GetValue<string>("PostgresCache:DefaultSlidingExpiration");
            if (!string.IsNullOrEmpty(slidingExpiration) && TimeSpan.TryParse(slidingExpiration, out var sliding)) {
                options.DefaultSlidingExpiration = sliding;
            }
        });

        builder.Services.AddSession(options =>
        {
            options.IdleTimeout = TimeSpan.FromHours(8);
            options.Cookie.MaxAge = TimeSpan.FromDays(30);
            options.Cookie.HttpOnly = true;
            options.Cookie.IsEssential = true;
        });
        
        builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/login";
                options.LogoutPath = "/logout";
                options.ExpireTimeSpan = TimeSpan.FromHours(48);
                options.Cookie.MaxAge = TimeSpan.FromDays(30);
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Strict;
                options.SlidingExpiration = true;
            });

        builder.Services.AddOpenIddict()
            .AddCore(opt =>
            {
                opt.UseEntityFrameworkCore()
                    .UseDbContext<PgContext>();
            })
            .AddClient(opt =>
            {
                opt.AllowAuthorizationCodeFlow();

                // ???
                var cert = CertificateLoader.GetOrCreateCertificate();
                opt.AddEncryptionCertificate(cert);
                opt.AddSigningCertificate(cert);

                opt.UseAspNetCore()
                    .EnableRedirectionEndpointPassthrough()
                    .DisableTransportSecurityRequirement(); // Behind reverse proxy

                // ???
                opt.UseSystemNetHttp();

                opt.UseWebProviders()
                    .AddDiscord(options =>
                    {
                        var discordSettings = builder.Configuration.GetSection("Discord").Get<DiscordSettings>();
                        options.SetClientId(discordSettings!.ClientId);
                        options.SetClientSecret(discordSettings!.ClientSecret);
                        options.SetRedirectUri("discord-callback");
                    });
                opt.UseDataProtection();
            });

        builder.Services.AddSingleton<HttpClient>();
        builder.Services.Configure<NotifierSettings>(builder.Configuration.GetSection("Notifier"));
        builder.Services.AddScoped<EventNotifier>();

        builder.Services.AddQuartz(q =>
        {
            q.UsePersistentStore(c =>
            {
                c.UsePostgres(p =>
                {
                    p.UseDriverDelegate<PostgreSQLDelegate>();
                    p.ConnectionStringName = "Postgres";
                    p.TablePrefix = "quartz.qrtz_";
                });
                c.UseNewtonsoftJsonSerializer();
            });

            var jobKey = new JobKey("LettuceDrop");
            q.AddJob<LettuceDropJob>(jobKey, j => 
                j.WithDescription("Daily lettuce drop"));

            q.AddTrigger(t => t
                .WithIdentity("Daily")
                .ForJob(jobKey)
                .WithCronSchedule("0 0 14 * * ?")
                .WithDescription("Daily cron trigger"));
            
            var logKey = new JobKey("logKey");
            q.AddJob<PlayerCountLogJob>(logKey, j => 
                j.WithDescription("Player count statistics collection"));

            q.AddTrigger(t => t
                .WithIdentity("5Min")
                .ForJob(logKey)
                .WithCronSchedule("0 */5 * * * ?")
                .WithDescription("5min cron trigger"));
        });

        builder.Services.AddQuartzHostedService(q =>
        {
            q.AwaitApplicationStarted = true;
            q.WaitForJobsToComplete = true;
        });

        var app = builder.Build();

        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PgContext>();
            await db.Database.MigrateAsync();
        }

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
        }

        app.UseSession();
        app.UseForwardedHeaders();

        app.UseRouting();

        app.UseAuthentication();
        app.UseAuthorization();


        app.MapStaticAssets();
        app.MapRazorPages()
            .WithStaticAssets();
        app.UseStaticFiles();
        app.MapControllers();
        app.MapHub<LettuceHub>("/lettuceHub");

        await app.RunAsync();
    }
}