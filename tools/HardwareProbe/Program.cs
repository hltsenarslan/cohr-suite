// tools/HardwareProbe/Program.cs
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

static string TryRun(string fileName, string args)
{
    try {
        var p = new Process {
            StartInfo = new ProcessStartInfo {
                FileName = fileName, Arguments = args,
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true
            }
        };
        p.Start(); var outp = p.StandardOutput.ReadToEnd(); p.WaitForExit(1500);
        return outp.Trim();
    } catch { return ""; }
}

var parts = new List<string> {
    Environment.MachineName,
    RuntimeInformation.OSDescription,
    RuntimeInformation.OSArchitecture.ToString()
};

if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    parts.Add(TryRun("/usr/sbin/ioreg", "-rd1 -c IOPlatformExpertDevice"));
    parts.Add(TryRun("/usr/sbin/system_profiler", "SPHardwareDataType"));
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
{
    parts.Add(System.IO.File.Exists("/etc/machine-id") ? System.IO.File.ReadAllText("/etc/machine-id").Trim() : "");
    parts.Add(TryRun("/usr/sbin/dmidecode", "-s system-uuid")); // root gerektirebilir
}
else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
{
    parts.Add(TryRun("wmic", "baseboard get serialnumber"));
    parts.Add(TryRun("wmic", "cpu get processorid"));
    parts.Add(TryRun("wmic", "bios get serialnumber"));
}

string concat = string.Join("|", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(concat))).ToLowerInvariant();
Console.WriteLine(hash);