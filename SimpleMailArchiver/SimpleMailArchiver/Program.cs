using SimpleMailArchiver;


var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables(prefix: "SMA_");
var startup = new Startup(builder.Configuration);
startup.ConfigureServices(builder.Services);
startup.ConfigureLogging(builder.Logging);
var app = builder.Build();
startup.Configure(app, builder.Environment);
app.Run();