﻿using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SimpleMailArchiver.Areas.Identity;
using SimpleMailArchiver.Data;

namespace SimpleMailArchiver
{
    public class Program
    {
        public static AppConfig Config { get; } = AppConfig.Load("config.json");
        public static IDbContextFactory<ArchiveContext> ContextFactory { get; private set; }

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            string? connectionString = $"DataSource={Config.DbPath}/archive.db";
            builder.Services.AddDbContextFactory<ArchiveContext>(options => options.UseSqlite(connectionString));
            builder.Services.AddScoped<DatabaseService>();

            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
            builder.Services.AddScoped<AuthenticationStateProvider, RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
            builder.Services.AddSingleton<DatabaseService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");

            DatabaseService.Initialize(app.Services);
            ContextFactory = app.Services.GetService<IDbContextFactory<ArchiveContext>>()!;

            app.Run();
        }
    }
}