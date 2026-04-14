using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace AssemblyManager.Services
{
    public static class KompasLauncher
    {
        public static void Open(string filePath)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("Файл модели не найден.", filePath);
            }

            var kompasExe = FindKompasExecutable();

            if (kompasExe != null)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = kompasExe,
                    Arguments = "\"" + filePath + "\"",
                    UseShellExecute = false
                });
                return;
            }

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "КОМПАС-3D не найден. Установите КОМПАС или сопоставьте расширения .m3d / .a3d с ним.",
                    ex);
            }
        }

        private static string? FindKompasExecutable()
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view);
                var path = ProbeRegistry(hklm);
                if (path != null) return path;
            }

            foreach (var root in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)
                     })
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                var ascon = Path.Combine(root, "ASCON");
                if (!Directory.Exists(ascon)) continue;

                foreach (var dir in Directory.EnumerateDirectories(ascon, "KOMPAS*"))
                {
                    foreach (var name in new[] { "kStudy.exe", "kompas.exe" })
                    {
                        var candidate = Path.Combine(dir, "Bin", name);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }

            return null;
        }

        private static string? ProbeRegistry(RegistryKey hklm)
        {
            foreach (var root in new[] { @"SOFTWARE\ASCON", @"SOFTWARE\Wow6432Node\ASCON" })
            {
                using var asconKey = hklm.OpenSubKey(root);
                if (asconKey == null) continue;

                foreach (var productName in asconKey.GetSubKeyNames())
                {
                    if (productName.IndexOf("KOMPAS", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    using var productKey = asconKey.OpenSubKey(productName);
                    if (productKey == null) continue;

                    foreach (var versionName in productKey.GetSubKeyNames())
                    {
                        using var versionKey = productKey.OpenSubKey(versionName);
                        var installDir = versionKey?.GetValue("InstallDir") as string
                                         ?? versionKey?.GetValue("InstallPath") as string;
                        if (string.IsNullOrWhiteSpace(installDir)) continue;

                        foreach (var name in new[] { "kStudy.exe", "kompas.exe" })
                        {
                            var candidate = Path.Combine(installDir, "Bin", name);
                            if (File.Exists(candidate)) return candidate;
                            candidate = Path.Combine(installDir, name);
                            if (File.Exists(candidate)) return candidate;
                        }
                    }
                }
            }

            return null;
        }
    }
}
