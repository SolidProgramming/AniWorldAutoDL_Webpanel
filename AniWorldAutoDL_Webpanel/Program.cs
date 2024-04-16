global using AniWorldAutoDL_Webpanel.Models;
global using AniWorldAutoDL_Webpanel.Classes;
global using AniWorldAutoDL_Webpanel.Interfaces;
global using AniWorldAutoDL_Webpanel.Services;
global using AniWorldAutoDL_Webpanel.Enums;
global using AniWorldAutoDL_Webpanel.Misc;
using Quartz;
using Havit.Blazor.Components.Web;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PuppeteerSharp;
using System.Net;
using System.Net.Sockets;

SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

if (settings is null || string.IsNullOrEmpty(settings.ApiUrl) || string.IsNullOrEmpty(settings.DownloadPath))
{
    Console.WriteLine("Settings.json Datei nicht gefunden oder nicht vollständig!\nProgramm wird beendet.");
    Console.ReadKey();
    return;
}

string? hostUrl = GetHostAdress();

if (AnotherInstanceExists())
{
    OpenBrowser(hostUrl);
    return;
}

HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

bool hosterReachableSTO = await HosterHelper.HosterReachable(sto);

if (!hosterReachableSTO)
{
    OpenBrowser(sto.BrowserUrl);
    return;
}

bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld);

if (!hosterReachableAniworld)
{
    OpenBrowser(aniworld.BrowserUrl);
    return;
}

Console.WriteLine("Downloading Chrome");
using var browserFetcher = new BrowserFetcher();
await browserFetcher.DownloadAsync();

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls(hostUrl, "http://localhost:5080");

builder.Services.AddHsts(_ =>
{
    _.Preload = true;
    _.IncludeSubDomains = true;
});

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHttpClient<IApiService, ApiService>();

builder.Services.AddQuartz();

builder.Services.AddQuartzHostedService(_ =>
{
    _.WaitForJobsToComplete = true;
    _.AwaitApplicationStarted = true;
});

builder.Services.AddSingleton<IApiService, ApiService>();
builder.Services.AddSingleton<IConverterService, ConverterService>();
builder.Services.AddSingleton<Updater.Interfaces.IUpdateService, Updater.Services.UpdateService>();
builder.Services.AddSingleton<IQuartzService, QuartzService>();

builder.Services.AddHxServices();
builder.Services.AddHxMessenger();

WebApplication? app = builder.Build();

app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

IConverterService converterService = app.Services.GetRequiredService<IConverterService>();
converterService.Init();

IApiService apiService = app.Services.GetRequiredService<IApiService>();
apiService.Init();

DownloaderPreferencesModel? downloaderPreferences = await apiService.GetAsync<DownloaderPreferencesModel?>("getDownloaderPreferences");

IQuartzService quartz = app.Services.GetRequiredService<IQuartzService>();
await quartz.Init();

if (downloaderPreferences is null)
{
    await quartz.CreateJob(15);
}
else
{
    await quartz.CreateJob(downloaderPreferences.Interval);
}


OpenBrowser(hostUrl);

app.Run();

static void OpenBrowser(string url)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        Process.Start("xdg-open", url);
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
    {
        Process.Start("open", url);
    }
    else
    {
        // throw 
    }
}

static bool AnotherInstanceExists()
{
    Process currentRunningProcess = Process.GetCurrentProcess();

    Process[] listOfProcs = Process.GetProcessesByName(currentRunningProcess.ProcessName);

    foreach (Process proc in listOfProcs)
    {

        if (( proc.MainModule.FileName == currentRunningProcess.MainModule.FileName ) && ( proc.Id != currentRunningProcess.Id ))

            return true;
    }
    return false;
}

static string? GetHostAdress()
{
    IPAddress[] addresslist = Dns.GetHostAddresses(Dns.GetHostName());

    string ipAdress = addresslist.FirstOrDefault(_ => _.AddressFamily == AddressFamily.InterNetwork && _.MapToIPv4().ToString().StartsWith("192.168"))?.MapToIPv4().ToString();

    string hostAdress = $"http://{ipAdress}:5080";

    return hostAdress;
}