namespace TabStudio.Toolchains.Embedded.Storage;

/// <summary>A permission-scoped logical block device; no kernel disk path or operating-system mount is required.</summary>
public interface IBlockDevice
{
    /// <summary>Logical sector size in bytes.</summary>
    int SectorSize { get; }
    /// <summary>Total number of sectors from the device's actual capacity response.</summary>
    ulong SectorCount { get; }
    /// <summary>Reads complete sectors, failing on short transport data.</summary>
    Task<byte[]> ReadBlocksAsync(uint sector, int count, CancellationToken cancellationToken);
    /// <summary>Writes a whole number of sectors; an exception never implies that no payload reached the target.</summary>
    Task WriteBlocksAsync(uint sector, ReadOnlyMemory<byte> data, CancellationToken cancellationToken);
}

/// <summary>A transport lost its connection while a block write was in progress.</summary>
public sealed class BlockWriteDisconnectedException : IOException
{
    /// <summary>Constructs an explicitly ambiguous write result.</summary>
    /// <param name="message">Transport details.</param>
    /// <param name="dataTransferred">The complete data phase was accepted by USB before status disappeared.</param>
    public BlockWriteDisconnectedException(string message, bool dataTransferred) : base(message) => DataTransferred = dataTransferred;
    /// <summary>Whether all data reached USB before the command status was lost. This does not verify flash.</summary>
    public bool DataTransferred { get; }
}

/// <summary>Observed FAT allocation layout, derived from the on-device BPB and actual device capacity.</summary>
/// <param name="Kind">FAT12, FAT16 or FAT32, selected by data-cluster count.</param>
/// <param name="VolumeLabel">BPB volume label, if present.</param>
/// <param name="VolumeStart">First sector of the filesystem, including a supported MBR partition offset.</param>
/// <param name="VolumeSectors">BPB sector count.</param>
/// <param name="ClusterBytes">Bytes per allocation cluster.</param>
/// <param name="ClusterCount">Data-cluster count.</param>
public sealed record FatVolumeInfo(string Kind, string VolumeLabel, uint VolumeStart, uint VolumeSectors,
    int ClusterBytes, uint ClusterCount);

/// <summary>Outcome of copying a file to FAT, distinct from firmware flash verification.</summary>
/// <param name="FileName">New 8.3 filename.</param>
/// <param name="BytesSubmitted">File payload bytes submitted.</param>
/// <param name="AllCommandsAcknowledged">Every data write returned a valid successful command status.</param>
/// <param name="FileReadbackMatched">File sectors could be read back and matched, or null when unavailable.</param>
/// <param name="ResetDuringFinalStatus">The final data phase completed but its status was lost on disconnect.</param>
public sealed record FatFileWriteResult(string FileName, long BytesSubmitted, bool AllCommandsAcknowledged,
    bool? FileReadbackMatched, bool ResetDuringFinalStatus);
