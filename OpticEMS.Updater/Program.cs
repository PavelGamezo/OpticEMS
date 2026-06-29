using System.Diagnostics;

// ── Лог в файл — единственный надёжный способ диагностики ──────────────────
string logPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
    "OpticEMS_Updater_Log.txt");

void Log(string message)
{
    string line = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
    Console.WriteLine(line);
    File.AppendAllText(logPath, line + Environment.NewLine);
}

File.WriteAllText(logPath, $"=== OpticEMS Updater started {DateTime.Now} ==={Environment.NewLine}");

Console.Title = "OpticEMS System Updater";
Console.WriteLine("=============================================");
Console.WriteLine("          OpticEMS UPDATE SERVICE            ");
Console.WriteLine("=============================================");

Log($"Arguments received: {args.Length}");
for (int i = 0; i < args.Length; i++)
    Log($"  args[{i}] = '{args[i]}'");

if (args.Length < 3)
{
    Log("[ERROR]: Not enough arguments. Expected: sourceDir targetDir exeName");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

string sourceDir = args[0];
string targetDir = args[1];
string exeToStart = args[2];

Log($"Source : {sourceDir}");
Log($"Target : {targetDir}");
Log($"Exe    : {exeToStart}");

if (!Directory.Exists(sourceDir))
{
    Log($"[ERROR]: Source directory does not exist: {sourceDir}");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
    return;
}

Log($"Source directory exists. Files inside:");
foreach (var f in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
    Log($"  {f}");

try
{
    Log("[1/3] Waiting for main app to shut down...");
    Thread.Sleep(2000);

    string processName = Path.GetFileNameWithoutExtension(exeToStart);
    Log($"Waiting for process: {processName}");

    int attempts = 0;
    Process[] running = Process.GetProcessesByName(processName);

    while (running.Length > 0 && attempts < 30)
    {
        Log($"  Still running ({attempts}/30)...");
        Thread.Sleep(1000);
        attempts++;
        running = Process.GetProcessesByName(processName);
    }

    if (running.Length > 0)
        Log("[WARN]: Process still alive after timeout. Continuing anyway.");
    else
        Log("Process has exited. Proceeding with file copy.");

    Log("[2/3] Copying files...");

    foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
    {
        string dest = dirPath.Replace(sourceDir, targetDir);
        if (!Directory.Exists(dest))
        {
            Directory.CreateDirectory(dest);
            Log($"  Created dir: {dest}");
        }
    }

    int copied = 0;
    int skipped = 0;

    foreach (string filePath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
    {
        string fileName = Path.GetFileName(filePath);

        if (fileName.Equals("OpticEMS.Updater.exe", StringComparison.OrdinalIgnoreCase))
        {
            Log($"  [SKIP self] {fileName}");
            skipped++;
            continue;
        }

        string destFile = filePath.Replace(sourceDir, targetDir);

        try
        {
            File.Copy(filePath, destFile, overwrite: true);
            Log($"  [OK] {fileName}");
            copied++;
        }
        catch (Exception copyEx)
        {
            Log($"  [FAIL] {fileName} — {copyEx.Message}");
            skipped++;
        }
    }

    Log($"[3/3] Done. Copied={copied}, Skipped={skipped}");

    string finalExe = Path.Combine(targetDir, exeToStart);
    Log($"Launching: {finalExe}");

    if (!File.Exists(finalExe))
    {
        Log($"[ERROR]: Exe not found at: {finalExe}");
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
        return;
    }

    Process.Start(new ProcessStartInfo
    {
        FileName = finalExe,
        UseShellExecute = true
    });

    Log("Application launched. Updater done.");

    Console.WriteLine("\nUpdate complete! Press any key to close...");
    Console.ReadKey(); // ← явная пауза — окно не закроется само
}
catch (Exception ex)
{
    Log($"[EXCEPTION] {ex.GetType().Name}: {ex.Message}");
    Log($"Stack: {ex.StackTrace}");

    Console.WriteLine("\nCRITICAL ERROR — see log on Desktop for details.");
    Console.WriteLine("Press any key to exit...");
    Console.ReadKey();
}