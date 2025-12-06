using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Radzen;
using SimpleMailArchiver.Components;
using SimpleMailArchiver.Data;
using SimpleMailArchiver.Services;
using SimpleMailArchiver.Services.MessageImportService;

namespace SimpleMailArchiver;

public class Startup
{
    private IConfiguration ConfigRoot { get; }
    private readonly PathConfig _appConfig;

    public Startup(IConfiguration configuration)
    {
        _appConfig = new PathConfig();
        configuration.Bind("Paths", _appConfig);
        if (configuration["Paths"] is null)
        {
            var configFile = "config.json";
            if (!File.Exists(configFile)) throw new Exception();
            _appConfig = JsonSerializer.Deserialize<PathConfig>(File.ReadAllText(configFile)) ?? throw new Exception();
        }

        ConfigRoot = configuration;
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddRazorComponents().AddInteractiveServerComponents();

        // AppConfig
        services.AddSingleton(_appConfig);
        services.AddSingleton<ApplicationContext>();
        services.AddScoped<FileDownloadHelperContext>();
        services.AddScoped<MailMessageHelperService>();

        // Set up directories
        if (!Directory.Exists(_appConfig.ArchiveBasePath))
            Directory.CreateDirectory(_appConfig.ArchiveBasePath);
        if (!Directory.Exists(_appConfig.DbPath))
            Directory.CreateDirectory(_appConfig.DbPath);

        // Database
        var connectionString = $"DataSource={_appConfig.DbPath}/archive.db";
        services.AddDbContextFactory<ArchiveContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<MessageImportService>();

        // Logging
        services.AddLogging();

        // Other
        services.AddRazorPages();
        services.AddServerSideBlazor();
        services.AddLocalization();

        services.AddRadzenComponents();
        services.AddRadzenQueryStringThemeService();
    }

    public void ConfigureLogging(ILoggingBuilder loggingBuilder)
    {
        loggingBuilder.AddConfiguration(ConfigRoot.GetSection("Logging"));
        loggingBuilder.AddConsole();
    }

    public void Configure(WebApplication app, IWebHostEnvironment env)
    {
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
            app.UseHsts();
        }


        DatabaseService.Initialize(app.Services);


        app.UseHttpsRedirection();
        app.UseStaticFiles();
        app.UseRouting();
        app.UseAuthorization();
        app.MapRazorPages();
        app.UseAntiforgery();
        app.MapRazorComponents<App>().AddInteractiveServerRenderMode();

        app.Logger.LogInformation(
            """
                        Using conifg:
                        Account configs path: {AccountConfigsPath}
                        Import base path: {ImportBasePath}
                        Archive base path: {ArchiveBasePath}
                        Database path: {DbPath}
            """,
            _appConfig.AccountConfigsPath,
            _appConfig.ImportBasePath,
            _appConfig.ArchiveBasePath,
            _appConfig.DbPath
        );
    }
}