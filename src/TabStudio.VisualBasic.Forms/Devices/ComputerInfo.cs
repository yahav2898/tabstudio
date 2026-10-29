using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.VisualBasic.Devices;

/// <summary>
/// The device's operating system, memory and installed culture (<c>My.Computer.Info</c>). Memory comes from the
/// operating system (<c>/proc/meminfo</c> on Android and Linux); virtual memory is the memory plus the swap space.
/// </summary>
public class ComputerInfo
{
    /// <summary>Creates the information object.</summary>
    public ComputerInfo()
    {
    }

    /// <summary>The free physical memory, in bytes.</summary>
    public ulong AvailablePhysicalMemory => MemoryStatus.Read().AvailablePhysical;

    /// <summary>The free virtual memory, in bytes: the free memory plus the free swap space.</summary>
    public ulong AvailableVirtualMemory => MemoryStatus.Read().AvailableVirtual;

    /// <summary>The culture the operating system was installed with.</summary>
    public CultureInfo InstalledUICulture => CultureInfo.InstalledUICulture;

    /// <summary>The operating system's full name, as .NET describes it.</summary>
    public string OSFullName => RuntimeInformation.OSDescription;

    /// <summary>The operating system's platform: <c>Unix</c> on Android and Linux.</summary>
    public string OSPlatform => Environment.OSVersion.Platform.ToString();

    /// <summary>The operating system's version.</summary>
    public string OSVersion => Environment.OSVersion.Version.ToString();

    /// <summary>The device's physical memory, in bytes.</summary>
    public ulong TotalPhysicalMemory => MemoryStatus.Read().TotalPhysical;

    /// <summary>The device's virtual memory, in bytes: the memory plus the swap space.</summary>
    public ulong TotalVirtualMemory => MemoryStatus.Read().TotalVirtual;

    /// <summary>The device's memory, from <c>/proc/meminfo</c> where there is one, or from what the garbage collector knows.</summary>
    private readonly record struct MemoryStatus(ulong TotalPhysical, ulong AvailablePhysical, ulong TotalVirtual, ulong AvailableVirtual)
    {
        private const string MemInfo = "/proc/meminfo";
        private const ulong KiB = 1024;

        public static MemoryStatus Read() => FromMemInfo() ?? FromGarbageCollector();

        private static MemoryStatus? FromMemInfo()
        {
            try
            {
                if (!File.Exists(MemInfo))
                {
                    return null;
                }

                var values = new Dictionary<string, ulong>(StringComparer.Ordinal);
                foreach (var line in File.ReadLines(MemInfo))
                {
                    // "MemTotal:        3891236 kB"
                    var colon = line.IndexOf(':', StringComparison.Ordinal);
                    if (colon <= 0)
                    {
                        continue;
                    }

                    var number = line[(colon + 1)..].Trim();
                    var space = number.IndexOf(' ', StringComparison.Ordinal);
                    if (space > 0)
                    {
                        number = number[..space];
                    }

                    if (ulong.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
                    {
                        values[line[..colon]] = value * KiB;
                    }
                }

                if (!values.TryGetValue("MemTotal", out var total) || total == 0)
                {
                    return null;
                }

                var available = values.TryGetValue("MemAvailable", out var memAvailable)
                    ? memAvailable
                    : values.GetValueOrDefault("MemFree") + values.GetValueOrDefault("Cached") + values.GetValueOrDefault("Buffers");
                var swapTotal = values.GetValueOrDefault("SwapTotal");
                var swapFree = values.GetValueOrDefault("SwapFree");
                return new MemoryStatus(total, Math.Min(available, total), total + swapTotal, Math.Min(available, total) + swapFree);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static MemoryStatus FromGarbageCollector()
        {
            var info = GC.GetGCMemoryInfo();
            var total = (ulong)Math.Max(0, info.TotalAvailableMemoryBytes);
            var available = (ulong)Math.Max(0, info.TotalAvailableMemoryBytes - info.MemoryLoadBytes);
            return new MemoryStatus(total, available, total, available);
        }
    }
}
