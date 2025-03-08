using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using SimpleMailArchiver.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Radzen;
using SimpleMailArchiver.Data;
using SimpleMailArchiver.Services;
using SimpleMailArchiver.Services.MessageImportService;

namespace SimpleMailArchiver;

public class Startup
{
    public IConfiguration ConfigRoot { get; }
    private readonly ApplicationContext _appContext;

    public Startup(IConfiguration configuration)
    {
        var appConfig = new PathConfig();
        configuration.Bind("Paths", appConfig);
        ConfigRoot = configuration;
        _appContext = new ApplicationContext(appConfig);
    }

    public void ConfigureServices(IServiceCollection services)
    {
        
        services.AddRazorComponents().AddInteractiveServerComponents();
        // AppConfig
        services.AddSingleton(_appContext);
        services.AddScoped<FileDownloadHelperContext>();
        services.AddScoped<MailMessageHelperService>();

        // Set up directories
        if (!Directory.Exists(_appContext.PathConfig.ArchiveBasePath))
            Directory.CreateDirectory(_appContext.PathConfig.ArchiveBasePath);
        if (!Directory.Exists(_appContext.PathConfig.DbPath))
            Directory.CreateDirectory(_appContext.PathConfig.DbPath);

        // Database
        var connectionString = $"DataSource={_appContext.PathConfig.DbPath}/archive.db";
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
            "Using conifg:\n\t" +
            $"Account configs path: {_appContext.PathConfig.AccountConfigsPath}\n\t" +
            $"Import base path: {_appContext.PathConfig.ImportBasePath}\n\t" +
            $"Archive base path: {_appContext.PathConfig.ArchiveBasePath}\n\t" +
            $"Database path: {_appContext.PathConfig.DbPath}"
        );
    }
}