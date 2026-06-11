using Microsoft.Win32;
using System.Security.Principal;

const string Clsid = "{F4E2C1A8-7B3D-4E5F-9A1C-2D8E6F0B4A3C}";
const string ProgId = "YSMViewer.ThumbnailProvider";
const string ThumbnailHandlerGuid = "{E357FCCD-A995-4576-B01F-234630154E96}";
var appDataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "YSMViewer",
    "ThumbnailProvider");

Console.OutputEncoding = System.Text.Encoding.UTF8;
PrintHeader();

if (args.Length > 0 && (args[0] == "--help" || args[0] == "-h"))
{
    PrintHelp();
    return 0;
}

if (!IsAdministrator())
{
    Console.WriteLine("Error / 错误: Administrator privileges required / 需要管理员权限。");
    Console.WriteLine("Right-click the program and select \"Run as administrator\" / 请右键点击程序，选择 [以管理员身份运行]。");
    Console.WriteLine();
    PauseAndExit(1);
}

var sourceDir = AppContext.BaseDirectory;
if (!File.Exists(Path.Combine(sourceDir, "YSMViewer.ThumbnailProvider.comhost.dll")))
{
    Console.WriteLine("Error / 错误: comhost.dll not found / 未找到 comhost.dll。");
    Console.WriteLine("Ensure comhost.dll is in the same directory / 请确保与本工具在同一目录。");
    Console.WriteLine($"Source / 来源: {sourceDir}");
    Console.WriteLine();
    PauseAndExit(1);
}

Console.WriteLine($"Source / 来源: {sourceDir}");
Console.WriteLine();

if (args.Length > 0)
{
    switch (args[0].ToLowerInvariant())
    {
        case "--register":
        case "-r":
            Register(sourceDir);
            return 0;
        case "--unregister":
        case "-u":
            Unregister();
            return 0;
        default:
            Console.WriteLine($"Unknown argument / 未知参数: {args[0]}");
            PrintHelp();
            return 1;
    }
}

while (true)
{
    PrintMenu();
    Console.Write("Select / 请选择 [1/2/3]: ");
    var input = Console.ReadLine()?.Trim();

    if (input is null)
        return 0;

    Console.WriteLine();

    switch (input)
    {
        case "1":
            Register(sourceDir);
            break;
        case "2":
            Unregister();
            break;
        case "3":
            Console.WriteLine("Goodbye / 再见！");
            return 0;
        default:
            Console.WriteLine("Invalid selection / 无效选择，请重新输入。");
            break;
    }

    Console.WriteLine();
}

void PrintHeader()
{
    Console.WriteLine("══════════════════════════════════════════════");
    Console.WriteLine("  YSMViewer Thumbnail Provider Registration Tool");
    Console.WriteLine("  YSMViewer 缩略图处理程序注册工具");
    Console.WriteLine("══════════════════════════════════════════════");
    Console.WriteLine();
}

void PrintMenu()
{
    Console.WriteLine("Select an operation / 请选择操作：");
    Console.WriteLine("  [1] Register thumbnail provider   / 注册缩略图处理程序");
    Console.WriteLine("  [2] Unregister thumbnail provider / 注销缩略图处理程序");
    Console.WriteLine("  [3] Exit                          / 退出");
}

bool IsAdministrator()
{
    using var identity = WindowsIdentity.GetCurrent();
    var principal = new WindowsPrincipal(identity);
    return principal.IsInRole(WindowsBuiltInRole.Administrator);
}

bool RunEnvironmentCheck()
{
    Console.WriteLine("--- Environment Check / 环境检测 ---");
    var dotnetRoot = @"C:\Program Files\dotnet\shared\Microsoft.NETCore.App";
    var errors = new List<string>();

    if (Environment.OSVersion.Version.Major < 10)
        errors.Add("Windows 10+ required / 需要 Windows 10 或更高版本。");

    if (!Directory.Exists(dotnetRoot))
        errors.Add(".NET Runtime not detected / 未检测到 .NET 运行时。");
    else if (!Directory.GetDirectories(dotnetRoot).Select(Path.GetFileName).Any(d => d is not null && d.StartsWith("10.")))
        errors.Add(".NET 10 Runtime not detected / 未检测到 .NET 10 运行时。");

    if (errors.Count > 0)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("  Environment check failed / 环境检测失败：");
        foreach (var e in errors)
            Console.WriteLine($"    [x] {e}");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine("  Fix the issues above and try again / 请修复以上问题后重试。");
        Console.WriteLine("  Download .NET 10 / 下载 .NET 10: https://dotnet.microsoft.com/download/dotnet/10.0");
        Console.WriteLine();
        return false;
    }

    var runtimeVer = Directory.GetDirectories(dotnetRoot)
        .Select(Path.GetFileName)
        .Where(d => d is not null && d.StartsWith("10."))
        .OrderDescending()
        .FirstOrDefault() ?? "unknown / 未知";

    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("  [OK] OS / 操作系统: Windows {0}", Environment.OSVersion.Version);
    Console.WriteLine("  [OK] .NET Runtime / .NET 运行时: {0}", runtimeVer);
    Console.ResetColor();
    Console.WriteLine();
    return true;
}

void Register(string sourceDir)
{
    try
    {
        Console.WriteLine("Installing YSM thumbnail provider / 正在安装 YSM 缩略图处理程序...");
        Console.WriteLine();

        if (!RunEnvironmentCheck())
            return;

        var appComHost = CopyToAppData(sourceDir);

        WriteClsid(appComHost);
        WriteProgId();
        WriteFileAssociation();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("══════════════════════════════════════");
        Console.WriteLine("  Registration successful!");
        Console.WriteLine("  注册成功！");
        Console.WriteLine("══════════════════════════════════════");
        Console.ResetColor();
        Console.WriteLine();
        Console.WriteLine($"Installed to / 文件已安装到: {appDataDir}");
        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Tip: The original files can be safely deleted after installation.");
        Console.WriteLine("提示: 安装完成后，原始文件可以安全删除。");
        Console.ResetColor();
        Console.WriteLine("     Restart File Explorer or log out to take effect / 可能需要重启资源管理器才能生效。");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Registration failed / 注册失败: {ex.Message}");
        Console.ResetColor();
    }
}

void Unregister()
{
    try
    {
        Console.WriteLine("Uninstalling YSM thumbnail provider / 正在卸载 YSM 缩略图处理程序...");
        Console.WriteLine();

        DeleteFileAssociation();
        DeleteProgId();
        DeleteClsid();
        DeleteAppData();

        Console.WriteLine();
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("══════════════════════════════════════");
        Console.WriteLine("  Unregistration successful!");
        Console.WriteLine("  注销成功！");
        Console.WriteLine("══════════════════════════════════════");
        Console.ResetColor();
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Unregistration failed / 注销失败: {ex.Message}");
        Console.ResetColor();
    }
}

void WriteClsid(string dllPath)
{
    using var clsidKey = Registry.ClassesRoot.CreateSubKey($@"CLSID\{Clsid}");
    clsidKey.SetValue(null, ProgId);

    using var serverKey = clsidKey.CreateSubKey("InprocServer32");
    serverKey.SetValue(null, dllPath);
    serverKey.SetValue("ThreadingModel", "Both");

    Console.WriteLine($"  [OK] CLSID registered / 已注册: {Clsid}");
}

void WriteProgId()
{
    using var progIdKey = Registry.ClassesRoot.CreateSubKey(ProgId);
    using var clsidSubKey = progIdKey.CreateSubKey("CLSID");
    clsidSubKey.SetValue(null, Clsid);

    Console.WriteLine($"  [OK] ProgID registered / 已注册: {ProgId}");
}

void WriteFileAssociation()
{
    using var ysmKey = Registry.ClassesRoot.CreateSubKey(@".ysm");
    using var shellexKey = ysmKey.CreateSubKey(@"ShellEx");
    using var handlerKey = shellexKey.CreateSubKey(ThumbnailHandlerGuid);
    handlerKey.SetValue(null, Clsid);

    Console.WriteLine("  [OK] .ysm file association / .ysm 文件关联已设置");
}

void DeleteClsid()
{
    try
    {
        Registry.ClassesRoot.DeleteSubKeyTree($@"CLSID\{Clsid}", throwOnMissingSubKey: false);
        Console.WriteLine($"  [OK] CLSID removed / 已删除: {Clsid}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [!!] Failed to remove CLSID / 删除 CLSID 失败: {ex.Message}");
    }
}

void DeleteProgId()
{
    try
    {
        Registry.ClassesRoot.DeleteSubKeyTree(ProgId, throwOnMissingSubKey: false);
        Console.WriteLine($"  [OK] ProgID removed / 已删除: {ProgId}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [!!] Failed to remove ProgID / 删除 ProgID 失败: {ex.Message}");
    }
}

void DeleteFileAssociation()
{
    try
    {
        var handlerPath = $@".ysm\ShellEx\{ThumbnailHandlerGuid}";
        Registry.ClassesRoot.DeleteSubKeyTree(handlerPath, throwOnMissingSubKey: false);
        Console.WriteLine("  [OK] .ysm file association removed / .ysm 文件关联已删除");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [!!] Failed to remove .ysm association / 删除 .ysm 关联失败: {ex.Message}");
    }
}

string CopyToAppData(string sourceDir)
{
    if (Directory.Exists(appDataDir))
    {
        Console.WriteLine($"  [i] Clearing previous install / 清理旧安装: {appDataDir}");
        try
        {
            Directory.Delete(appDataDir, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!!] Cleanup failed / 清理失败: {ex.Message}");
        }
    }

    Directory.CreateDirectory(appDataDir);
    Console.WriteLine($"  [OK] Created directory / 创建目录: {appDataDir}");

    var files = Directory.GetFiles(sourceDir);
    foreach (var file in files)
    {
        var dest = Path.Combine(appDataDir, Path.GetFileName(file));
        File.Copy(file, dest, overwrite: true);
    }
    Console.WriteLine($"  [OK] Copied {files.Length} file(s) / 已复制 {files.Length} 个文件");

    return Path.Combine(appDataDir, "YSMViewer.ThumbnailProvider.comhost.dll");
}

void DeleteAppData()
{
    try
    {
        if (Directory.Exists(appDataDir))
        {
            Directory.Delete(appDataDir, recursive: true);
            Console.WriteLine($"  [OK] Removed / 已删除: {appDataDir}");
        }
        else
        {
            Console.WriteLine($"  [i] Directory not found, nothing to clean / 目录不存在，无需清理: {appDataDir}");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  [!!] Failed to remove install directory / 删除安装目录失败: {ex.Message}");
    }
}

void PauseAndExit(int code)
{
    Console.WriteLine("Press Enter to exit / 按 Enter 退出...");
    Console.ReadLine();
    Environment.Exit(code);
}

void PrintHelp()
{
    Console.WriteLine("Usage / 用法: YSMViewer.ThumbnailProvider.Tools.exe [options / 选项]");
    Console.WriteLine();
    Console.WriteLine("Options / 选项:");
    Console.WriteLine("  --register, -r    Register thumbnail provider   / 注册缩略图处理程序");
    Console.WriteLine("  --unregister, -u  Unregister thumbnail provider / 注销缩略图处理程序");
    Console.WriteLine("  --help, -h        Show help / 显示帮助信息");
    Console.WriteLine();
    Console.WriteLine("Run without arguments for interactive menu / 不带参数运行时将进入交互式菜单。");
}
