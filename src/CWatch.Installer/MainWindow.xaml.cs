using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using Microsoft.Win32;

namespace CWatch.Installer;

public partial class MainWindow : Window
{
    private int _currentStep = 1;
    private string _installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "CWatch");

    public MainWindow()
    {
        InitializeComponent();
        TxtInstallPath.Text = _installDir;
        UpdateSpaceAvailable();
    }

    private void UpdateSpaceAvailable()
    {
        try
        {
            string root = Path.GetPathRoot(TxtInstallPath.Text) ?? "C:\\";
            var drive = new DriveInfo(root);
            if (drive.IsReady)
            {
                double freeGb = drive.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
                TxtSpaceAvailable.Text = $"{freeGb:F1} GB free on {drive.Name}";
            }
            else
            {
                TxtSpaceAvailable.Text = "Drive ready";
            }
        }
        catch
        {
            TxtSpaceAvailable.Text = "Unknown";
        }
    }

    private void TxtInstallPath_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _installDir = TxtInstallPath.Text;
        UpdateSpaceAvailable();
    }

    private void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Select C:Watch Installation Directory",
            InitialDirectory = _installDir
        };

        if (dialog.ShowDialog() == true)
        {
            TxtInstallPath.Text = Path.Combine(dialog.FolderName, "CWatch");
        }
    }

    private void BtnBack_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 2)
        {
            SetStep(1);
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 1)
        {
            if (string.IsNullOrWhiteSpace(TxtInstallPath.Text))
            {
                MessageBox.Show("Please specify a valid installation directory.", "Invalid Path", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            SetStep(2);
        }
        else if (_currentStep == 2)
        {
            SetStep(3);
            await RunInstallationAsync();
        }
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStep == 3)
        {
            if (MessageBox.Show("Installation is in progress. Are you sure you want to cancel?", "Confirm Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.No)
            {
                return;
            }
        }
        Close();
    }

    private void BtnFinish_Click(object sender, RoutedEventArgs e)
    {
        if (ChkLaunchApp.IsChecked == true)
        {
            string exePath = Path.Combine(_installDir, "CWatch.UI.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    WorkingDirectory = _installDir,
                    UseShellExecute = true
                });
            }
        }
        Close();
    }

    private void SetStep(int step)
    {
        _currentStep = step;

        Step1Welcome.Visibility = step == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2Options.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3Installing.Visibility = step == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4Finished.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;

        BtnBack.Visibility = step == 2 ? Visibility.Visible : Visibility.Collapsed;
        BtnNext.Visibility = (step == 1 || step == 2) ? Visibility.Visible : Visibility.Collapsed;
        BtnFinish.Visibility = step == 4 ? Visibility.Visible : Visibility.Collapsed;
        BtnCancel.IsEnabled = step != 3;

        BtnNext.Content = step == 2 ? "INSTALL" : "NEXT →";

        TxtHeaderSubtitle.Text = step switch
        {
            1 => "Select install destination folder",
            2 => "Configure shortcuts and startup options",
            3 => "Installing application binaries...",
            4 => "Setup completed successfully",
            _ => "C:Watch Setup"
        };
    }

    private async Task RunInstallationAsync()
    {
        InstallProgressBar.IsIndeterminate = true;
        TxtInstallStatus.Text = "Preparing installation directory...";

        await Task.Run(() =>
        {
            try
            {
                // 1. Terminate existing running instance if any
                try
                {
                    foreach (var p in Process.GetProcessesByName("CWatch.UI"))
                    {
                        p.Kill();
                        p.WaitForExit(2000);
                    }
                }
                catch { }

                // 2. Create destination folder
                if (!Directory.Exists(_installDir))
                {
                    Directory.CreateDirectory(_installDir);
                }

                // 3. Extract or copy payload binaries
                Dispatcher.Invoke(() => TxtInstallStatus.Text = "Extracting application binaries...");
                var assembly = Assembly.GetExecutingAssembly();
                Stream? stream = assembly.GetManifestResourceStream("CWatch.Installer.payload.zip");
                if (stream == null)
                {
                    var resName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase));
                    if (resName != null)
                    {
                        stream = assembly.GetManifestResourceStream(resName);
                    }
                }

                if (stream != null)
                {
                    using (stream)
                    using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        foreach (var entry in archive.Entries)
                        {
                            string destinationPath = Path.GetFullPath(Path.Combine(_installDir, entry.FullName));
                            if (string.IsNullOrEmpty(entry.Name))
                            {
                                // Directory entry
                                Directory.CreateDirectory(destinationPath);
                            }
                            else
                            {
                                string? dir = Path.GetDirectoryName(destinationPath);
                                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                                {
                                    Directory.CreateDirectory(dir);
                                }
                                entry.ExtractToFile(destinationPath, overwrite: true);
                            }
                        }
                    }
                }
                else
                {
                    // Fallback: Copy files from current directory / app subfolder
                    string sourceDir = AppDomain.CurrentDomain.BaseDirectory;
                    string appSub = Path.Combine(sourceDir, "app");
                    string copyFrom = Directory.Exists(appSub) ? appSub : sourceDir;

                    foreach (var file in Directory.GetFiles(copyFrom))
                    {
                        string fileName = Path.GetFileName(file);
                        if (fileName.Equals("Setup.exe", StringComparison.OrdinalIgnoreCase)) continue;
                        string destFile = Path.Combine(_installDir, fileName);
                        File.Copy(file, destFile, overwrite: true);
                    }
                }

                // 4. Create Uninstaller Scripts in target directory
                Dispatcher.Invoke(() => TxtInstallStatus.Text = "Creating uninstaller scripts...");
                WriteUninstallerScripts(_installDir);

                // 5. Create Shortcuts
                Dispatcher.Invoke(() => TxtInstallStatus.Text = "Creating program shortcuts...");
                string targetExe = Path.Combine(_installDir, "CWatch.UI.exe");

                bool createStartMenu = false;
                bool createDesktop = false;
                bool autoStartup = false;

                Dispatcher.Invoke(() =>
                {
                    createStartMenu = ChkStartMenuShortcut.IsChecked == true;
                    createDesktop = ChkDesktopShortcut.IsChecked == true;
                    autoStartup = ChkAutoStartup.IsChecked == true;
                });

                if (createStartMenu)
                {
                    string startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Microsoft", "Windows", "Start Menu", "Programs");
                    CreateShortcut(Path.Combine(startMenu, "C-Watch.lnk"), targetExe, _installDir, "C:Watch - Storage Intelligence & Safe Cleaner");
                }

                if (createDesktop)
                {
                    string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    CreateShortcut(Path.Combine(desktop, "C-Watch.lnk"), targetExe, _installDir, "C:Watch - Storage Intelligence & Safe Cleaner");
                }

                // 6. Register in Windows Registry (Installed Apps / Add-Remove Programs)
                Dispatcher.Invoke(() => TxtInstallStatus.Text = "Registering Windows uninstaller...");
                RegisterWindowsUninstall(_installDir, targetExe);

                // 7. Startup Registry if enabled
                if (autoStartup)
                {
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", writable: true);
                    runKey?.SetValue("CWatch", $"\"{targetExe}\" --minimized");
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Installation encountered an error:\n{ex.Message}", "Setup Error", MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        });

        InstallProgressBar.IsIndeterminate = false;
        InstallProgressBar.Value = 100;
        SetStep(4);
    }

    private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic? shell = Activator.CreateInstance(shellType);
                if (shell != null)
                {
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    shortcut.IconLocation = $"{targetPath},0";
                    shortcut.Save();
                }
            }
        }
        catch { }
    }

    private static void RegisterWindowsUninstall(string installDir, string exePath)
    {
        try
        {
            string uninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\CWatch";
            using var key = Registry.CurrentUser.CreateSubKey(uninstallKeyPath);
            if (key != null)
            {
                key.SetValue("DisplayName", "C:Watch Storage Intelligence");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "MR-1124");
                key.SetValue("DisplayIcon", Path.Combine(installDir, "Assets", "app.ico"));
                key.SetValue("UninstallString", $"\"{Path.Combine(installDir, "uninstall.cmd")}\"");
                key.SetValue("InstallLocation", installDir);
                key.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                key.SetValue("EstimatedSize", 75000, RegistryValueKind.DWord);
                key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("HelpLink", "https://github.com/MR-1124/CWatch");
            }
        }
        catch { }
    }

    private static void WriteUninstallerScripts(string installDir)
    {
        try
        {
            string uninstallPs1 = @"# C:Watch Automated Uninstaller
$ErrorActionPreference = 'SilentlyContinue'

Stop-Process -Name 'CWatch.UI' -Force -ErrorAction SilentlyContinue

# Remove shortcuts
$StartMenu = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\C-Watch.lnk'
if (Test-Path $StartMenu) { Remove-Item $StartMenu -Force }

$Desktop = Join-Path ([Environment]::GetFolderPath('Desktop')) 'C-Watch.lnk'
if (Test-Path $Desktop) { Remove-Item $Desktop -Force }

# Remove Registry entries
Remove-Item -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\CWatch' -Recurse -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name 'CWatch' -ErrorAction SilentlyContinue

# Remove files (schedule self-removal)
$InstallDir = $PSScriptRoot
Start-Process cmd.exe -ArgumentList ""/c timeout /t 2 & rmdir /s /q `""$InstallDir`"""" -WindowStyle Hidden
";
            File.WriteAllText(Path.Combine(installDir, "uninstall.ps1"), uninstallPs1);

            string uninstallCmd = @"@echo off
setlocal
cd /d ""%~dp0""
echo Uninstalling C:Watch...
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ""%~dp0uninstall.ps1""
echo C:Watch has been uninstalled successfully.
";
            File.WriteAllText(Path.Combine(installDir, "uninstall.cmd"), uninstallCmd);
        }
        catch { }
    }
}
