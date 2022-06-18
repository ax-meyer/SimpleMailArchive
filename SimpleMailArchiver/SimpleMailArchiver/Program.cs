using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Console;
using SimpleMailArchiver.Areas.Identity;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver
{
    public class Program
    {
        public static ImportProgress ImportProgress { get; set; } = new();
        public static bool ImportRunning { get; set; } = false;

        public static AppConfig Config { get; } = AppConfig.Load();
        public static IDbContextFactory<ArchiveContext> ContextFactory { get; private set; }
        public static ILogger Logger { get; private set; }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            if (!Directory.Exists(Config.ArchiveBasePath))
                Directory.CreateDirectory(Config.ArchiveBasePath);
            if (!Directory.Exists(Config.DbPath))
                Directory.CreateDirectory(Config.DbPath);

            // Add services to the container.
            string connectionString = $"DataSource={Config.DbPath}/archive.db";
            builder.Services.AddDbContextFactory<ArchiveContext>(options => options.UseSqlite(connectionString));
            builder.Services.AddScoped<DatabaseService>();

            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            builder.Services.AddSingleton<DatabaseService>();

            builder.Services.AddLocalization();

            builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));
            builder.Logging.AddConsole();

            var app = builder.Build();

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.MapControllers();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            app.UseRequestLocalization(builder.Configuration.GetValue<string>("Localization"));

            DatabaseService.Initialize(app.Services);
            ContextFactory = app.Services.GetService<IDbContextFactory<ArchiveContext>>()!;
            Logger = app.Logger;

            Logger.LogInformation(
                "Using conifg:\n\t" +
                $"Account configs path: {Config.AccountConfigsPath}\n\t" +
                $"Import base path: {Config.ImportBasePath}\n\t" +
                $"Archive base path: {Config.ArchiveBasePath}\n\t" +
                $"Database path: {Config.DbPath}"
                );

            AppConfig.LoadAccounts(Config);

            app.Run();
        }
    }
}
