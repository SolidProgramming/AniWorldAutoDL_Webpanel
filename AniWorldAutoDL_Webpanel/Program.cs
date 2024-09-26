global using AniWorldAutoDL_Webpanel.Models;
global using AniWorldAutoDL_Webpanel.Classes;
global using AniWorldAutoDL_Webpanel.Interfaces;
global using AniWorldAutoDL_Webpanel.Services;
global using AniWorldAutoDL_Webpanel.Enums;
global using AniWorldAutoDL_Webpanel.Misc;
global using AniWorldAutoDL_Webpanel.Factories;
using Quartz;
using Havit.Blazor.Components.Web;
using System.Diagnostics;
using System.Runtime.InteropServices;
using PuppeteerSharp;
using System.Net;
using System.Net.Sockets;
using Toolbelt.Blazor.Extensions.DependencyInjection;
using Toolbelt.Blazor.I18nText;

WebApplicationBuilder? builder = WebApplication.CreateBuilder(args);

string? hostUrl = default;
if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    hostUrl = GetHostAdress();

    if (AnotherInstanceExists())
    {
        OpenBrowser(hostUrl);
        return;
    }

    builder.WebHost.UseUrls(hostUrl, "http://localhost:5080");
}

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

builder.Services.AddI18nText(_ =>
{
    _.PersistenceLevel = PersistanceLevel.Cookie;
});

WebApplication? app = builder.Build();

SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

if (settings is null || string.IsNullOrEmpty(settings.ApiUrl))
{
    app.Logger.LogError($"{DateTime.Now} | Could not find Settings.json file or settings not complete.");
    return;
}

app.Logger.LogInformation($"{DateTime.Now} | Downloading and installing chrome.");
BrowserFetcher? browserFetcher = new();
await browserFetcher.DownloadAsync();

app.UseHsts();
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

IConverterService converterService = app.Services.GetRequiredService<IConverterService>();
bool converterInitSuccess = converterService.Init();

if (!converterInitSuccess)
{
    app.Logger.LogError($"{DateTime.Now} | Converter couldn't be initialized!");
    return;
}

IApiService apiService = app.Services.GetRequiredService<IApiService>();
bool apiInitSuccess = apiService.Init();

if (!apiInitSuccess)
{
    app.Logger.LogError($"{DateTime.Now} | API service couldn't be initialized!");
    return;
}

HosterModel? sto = HosterHelper.GetHosterByEnum(Hoster.STO);
HosterModel? aniworld = HosterHelper.GetHosterByEnum(Hoster.AniWorld);

DownloaderPreferencesModel? downloaderPreferences = await apiService.GetAsync<DownloaderPreferencesModel?>("getDownloaderPreferences");

WebProxy? proxy = default;

if (downloaderPreferences is not null && downloaderPreferences.UseProxy)
{
    app.Logger.LogInformation($"{DateTime.Now} | Proxy configured: {downloaderPreferences.ProxyUri}");

    proxy = ProxyFactory.CreateProxy(new ProxyAccountModel()
    {
        Uri = downloaderPreferences.ProxyUri,
        Username = downloaderPreferences.ProxyUsername,
        Password = downloaderPreferences.ProxyPassword
    });
}

app.Logger.LogInformation($"{DateTime.Now} | Checking if Hosters are reachable...");

(bool success, string? ipv4) = await new HttpClient().GetIPv4();
if (!success)
{
    app.Logger.LogError($"{DateTime.Now} | HttpClient could not retrieve WAN IP Address.");
    return;
}

app.Logger.LogInformation($"{DateTime.Now} | Your WAN IP is: {ipv4}");

bool hosterReachableSTO = await HosterHelper.HosterReachable(sto, proxy);

if (!hosterReachableSTO)
{
    app.Logger.LogError($"{DateTime.Now} | Hoster: {sto.Host} not reachable. Maybe there is a captcha you need to solve.");

    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    {
        OpenBrowser(sto.BrowserUrl);
    }

    return;
}

bool hosterReachableAniworld = await HosterHelper.HosterReachable(aniworld, proxy);

if (!hosterReachableAniworld)
{
    app.Logger.LogError($"{DateTime.Now} | Hoster: {aniworld.Host} not reachable. Maybe there is a captcha you need to solve.");

    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    {
        OpenBrowser(aniworld.BrowserUrl);
    }
    return;
}

app.Logger.LogInformation($"{DateTime.Now} | Initializing Cronjob and HttpClients...");
await CronJob.InitAsync(proxy);

IQuartzService quartz = app.Services.GetRequiredService<IQuartzService>();
await quartz.Init();

if (downloaderPreferences is null)
{
    await quartz.CreateJob(15);
}
else
{
    if (downloaderPreferences.AutoStart)
    {
        await quartz.CreateJob(downloaderPreferences.Interval);
    }
}

if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux) || Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    OpenBrowser(hostUrl);
}

app.Run();

static void OpenBrowser(string url)
{
    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
    else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
    {
        if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
        {
            Process.Start("xdg-open", url);
        }
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
    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
        return false;

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

    string? ipAdress;

    if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true")
    {
        ipAdress = addresslist.FirstOrDefault(_ => _.AddressFamily == AddressFamily.InterNetwork)?.MapToIPv4().ToString();
    }
    else
    {
        ipAdress = addresslist.FirstOrDefault(_ => _.AddressFamily == AddressFamily.InterNetwork && _.MapToIPv4().ToString().StartsWith("192.168"))?.MapToIPv4().ToString();
    }

    string hostAdress = $"http://{ipAdress}:5080";

    return hostAdress;
}