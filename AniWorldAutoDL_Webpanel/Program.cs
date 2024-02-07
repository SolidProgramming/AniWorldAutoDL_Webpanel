global using AniWorldAutoDL_Webpanel.Models;
global using AniWorldAutoDL_Webpanel.Classes;
global using AniWorldAutoDL_Webpanel.Interfaces;
global using AniWorldAutoDL_Webpanel.Services;
global using AniWorldAutoDL_Webpanel.Enums;
global using AniWorldAutoDL_Webpanel.Misc;
using Quartz;
using Havit.Blazor.Components.Web;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

builder.Services.AddHsts(_ =>
{
    _.Preload = true;
    _.IncludeSubDomains = true;
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

if (settings is null)
{
    Console.WriteLine("Couldn't read or find Settings file!. Shutting down!");
    return;
}

builder.Services.AddSingleton(_ =>
{
    return new HttpClient() { BaseAddress = new Uri(settings.ApiUrl) };
});

builder.Services.AddQuartz(_ =>
{
    _.AddJobAndTrigger<CronJob>(15);
});

builder.Services.AddQuartzHostedService(_ =>
{
    _.WaitForJobsToComplete = true;
    _.AwaitApplicationStarted = true;
});

builder.Services.AddSingleton<IApiService, ApiService>();

builder.Services.AddHxServices();
builder.Services.AddHxMessenger();

WebApplication? app = builder.Build();

app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
