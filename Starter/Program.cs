string DownloadsPath = Path.Combine(Directory.GetCurrentDirectory(), "Updates");
string DestPath = Directory.GetCurrentDirectory();

Console.WriteLine("===================\n\nInstalliere Updates...");
Console.WriteLine($"Von: {DownloadsPath}\nNach: {DestPath}");
await Task.Delay(5000);

DirectoryInfo? folder = new(DownloadsPath);

if (folder.Exists)
{
    FileInfo[]? files = folder.GetFiles("*");
    files.ToList().ForEach(f =>
    {
        if (f.Name != "Starter.exe")
        {
            try
            {
                File.Move(Path.Combine(DownloadsPath, f.Name), Path.Combine(DestPath, f.Name), true);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.ReadKey();
            }            
            Console.WriteLine($"{f.Name} kopiert!");
        }
    });

    Directory.Delete(DownloadsPath, true);
}
else
{
    return;
}

Console.WriteLine("\n\nUpdates installiert...Neustart...\n\n===================");
await Task.Delay(3000);

System.Diagnostics.Process.Start("AniWorldAutoDL_Webpanel.exe");