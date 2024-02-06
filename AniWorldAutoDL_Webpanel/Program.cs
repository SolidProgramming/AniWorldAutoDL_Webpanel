global using AniWorldAutoDL_Webpanel.Models;
global using AniWorldAutoDL_Webpanel.Classes;
global using AniWorldAutoDL_Webpanel.Interfaces;
global using AniWorldAutoDL_Webpanel.Services;
global using AniWorldAutoDL_Webpanel.Enums;
global using AniWorldAutoDL_Webpanel.Misc;
using Quartz;
using Havit.Blazor.Components.Web;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

using ILoggerFactory factory = LoggerFactory.Create(_ => _.AddConsole());
ILogger logger = factory.CreateLogger<Program>();

builder.Services.AddHttpClient();

//builder.Services.AddQuartz(_ =>
//{
//    _.AddJobAndTrigger<CronJob>(15);
//});

//builder.Services.AddQuartzHostedService(_ =>
//{
//    _.WaitForJobsToComplete = true;
//    _.AwaitApplicationStarted = true;
//});

builder.Services.AddSingleton<IApiService, ApiService>();

builder.Services.AddHxServices();
builder.Services.AddHxMessenger();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
