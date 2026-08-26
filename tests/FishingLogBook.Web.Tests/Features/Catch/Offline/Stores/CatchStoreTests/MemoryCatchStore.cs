using FishingLogBook.Web.Common;
using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public sealed class MemoryCatchStore : ICatchStore
{
    private readonly Dictionary<Guid, CatchModel> _catches;
    private readonly Dictionary<Guid, byte[]> _photographBytes;
    private readonly HashSet<Guid> _photographReads = [];

    public bool FailPhotographWrite { get; set; }

    public Func<Guid, Task>? BeforeSingleRead { get; set; }

    public int GetAllCalls { get; private set; }

    public int GetMetadataCalls { get; private set; }

    public int GetCalls { get; private set; }

    public IReadOnlyCollection<Guid> PhotographBytesReadFor => _photographReads;

    public MemoryCatchStore()
        : this([], [])
    {
    }

    public MemoryCatchStore(
        Dictionary<Guid, CatchModel> catches,
        Dictionary<Guid, byte[]> photographBytes)
    {
        _catches = catches;
        _photographBytes = photographBytes;
    }

    public Task SaveAsync(CatchModel catchRecord, CancellationToken cancellationToken)
    {
        if (catchRecord.UserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch requires an owner.");
        }

        if (catchRecord.Photographs.Count == 0
            || catchRecord.Photographs.Any(photograph => photograph.Bytes is not { Length: > 0 }))
        {
            throw new InvalidOperationException("A catch requires at least one photograph.");
        }

        if (FailPhotographWrite)
        {
            throw new InvalidOperationException("Photograph persistence failed.");
        }

        _catches[catchRecord.Id] = catchRecord with
        {
            Photographs = catchRecord.Photographs
                .Select(photograph => photograph with { Bytes = null })
                .ToArray()
        };
        foreach (var photograph in catchRecord.Photographs)
        {
            _photographBytes[photograph.Id] = photograph.Bytes!;
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CatchModel>> GetAllAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        GetAllCalls += 1;
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        IReadOnlyList<CatchModel> items = _catches.Values
            .Select(catchRecord => WithPhotographBytes(catchRecord))
            .ToArray();
        return Task.FromResult(LocalCatchVisibility.ForOwner(items, ownerUserId));
    }

    public Task<IReadOnlyList<CatchModel>> GetMetadataAsync(
        Guid ownerUserId,
        CancellationToken cancellationToken)
    {
        GetMetadataCalls += 1;
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        IReadOnlyList<CatchModel> items = _catches.Values
            .Select(catchRecord => catchRecord with
            {
                Photographs = catchRecord.Photographs
                    .Select(photograph => photograph with { Bytes = null })
                    .ToArray()
            })
            .ToArray();
        return Task.FromResult(LocalCatchVisibility.ForOwner(items, ownerUserId));
    }

    public Task<CatchModel?> GetMetadataAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        GetMetadataCalls += 1;
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        var item = _catches.GetValueOrDefault(catchId);
        return Task.FromResult(item is not null && item.UserId == ownerUserId
            ? item with
            {
                Photographs = item.Photographs
                    .Select(photograph => photograph with { Bytes = null })
                    .ToArray()
            }
            : null);
    }

    public async Task<CatchModel?> GetAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        GetCalls += 1;
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        if (BeforeSingleRead is not null)
        {
            await BeforeSingleRead(catchId);
        }

        if (!_catches.TryGetValue(catchId, out var stored) || stored.UserId != ownerUserId)
        {
            return null;
        }

        return WithPhotographBytes(stored);
    }

    public Task UpdateSyncStateAsync(
        CatchModel catchRecord,
        CancellationToken cancellationToken)
    {
        if (!_catches.TryGetValue(catchRecord.Id, out var existing)
            || existing.UserId != catchRecord.UserId)
        {
            throw new InvalidOperationException("Owned Catch was not found.");
        }

        var incomingPhotographs = catchRecord.Photographs.ToDictionary(
            photograph => photograph.Id);
        _catches[catchRecord.Id] = existing with
        {
            SyncStatus = catchRecord.SyncStatus,
            MetadataSyncStatus = catchRecord.MetadataSyncStatus,
            SyncedAt = catchRecord.SyncedAt,
            Photographs = existing.Photographs
                .Select(photograph => incomingPhotographs.TryGetValue(
                    photograph.Id,
                    out var incoming)
                    ? photograph with
                    {
                        SyncStatus = incoming.SyncStatus,
                        ObjectKey = incoming.ObjectKey
                    }
                    : photograph)
                .ToArray()
        };
        return Task.CompletedTask;
    }

    public int CleanupCalls { get; private set; }

    public Task<int> CleanupSyncedCacheAsync(
        Guid ownerUserId,
        DateTimeOffset olderThan,
        CancellationToken cancellationToken)
    {
        CleanupCalls += 1;
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        var eligible = _catches.Values
            .Where(catchRecord => catchRecord.UserId == ownerUserId)
            .Where(catchRecord => catchRecord.SyncStatus == SyncStatus.Synchronised
                && catchRecord.MetadataSyncStatus == SyncStatus.Synchronised
                && catchRecord.Photographs.Count > 0
                && catchRecord.Photographs.All(
                    photograph => photograph.SyncStatus == SyncStatus.Synchronised)
                && catchRecord.SyncedAt is not null
                && catchRecord.SyncedAt <= olderThan)
            .ToArray();

        foreach (var catchRecord in eligible)
        {
            foreach (var photograph in catchRecord.Photographs)
            {
                _photographBytes.Remove(photograph.Id);
            }

            _catches.Remove(catchRecord.Id);
        }

        return Task.FromResult(eligible.Length);
    }

    private CatchModel WithPhotographBytes(CatchModel catchRecord)
    {
        return catchRecord with
        {
            Photographs = catchRecord.Photographs
                .Select(photograph =>
                {
                    var bytes = _photographBytes.GetValueOrDefault(photograph.Id);
                    if (bytes is { Length: > 0 })
                    {
                        _photographReads.Add(photograph.Id);
                    }

                    return photograph with { Bytes = bytes };
                })
                .ToArray()
        };
    }
}
