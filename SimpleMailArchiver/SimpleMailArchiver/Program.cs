using SimpleMailArchiver;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables("SMA_");
var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);
startup.ConfigureLogging(builder.Logging);

var app = builder.Build();
startup.Configure(app, builder.Environment);

/*
// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
*/
app.Run();