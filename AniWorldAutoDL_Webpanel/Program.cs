global using AniWorldAutoDL_Webpanel.Models;
global using AniWorldAutoDL_Webpanel.Classes;
global using AniWorldAutoDL_Webpanel.Interfaces;
global using AniWorldAutoDL_Webpanel.Services;
global using AniWorldAutoDL_Webpanel.Enums;
global using AniWorldAutoDL_Webpanel.Misc;
using Quartz;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

using ILoggerFactory factory = LoggerFactory.Create(_ => _.AddConsole());
ILogger logger = factory.CreateLogger<Program>();

SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

if (settings is null)
{
    logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
    return;
}

bool binariesFound = Converter.FoundBinaries();

if (!binariesFound)
{
    string parentFolder = Directory.GetParent(Helper.GetFFProbePath())!.FullName;
    await Console.Out.WriteLineAsync($"{DateTime.Now} | {ErrorMessage.BinariesNotFound}\nPath: {parentFolder}");
    Console.ReadKey();
    return;
}

HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

bool hosterReachableSTO = await HosterHelper.HosterReachable(sto);
bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld);

if (!hosterReachableSTO)
{

    logger.LogError($"{DateTime.Now} | {sto.Hoster} {ErrorMessage.HosterUnavailable}");
    Console.ReadKey();
    return;
}

if (!hosterReachableAniworld)
{
    logger.LogError($"{DateTime.Now} | {aniworld.Hoster} {ErrorMessage.HosterUnavailable}");
    Console.ReadKey();
    return;
}

//Converter.ConvertStarted += Converter_ConvertStarted;
//Converter.ConvertProgressChanged += Converter_ConvertProgressChanged;

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
