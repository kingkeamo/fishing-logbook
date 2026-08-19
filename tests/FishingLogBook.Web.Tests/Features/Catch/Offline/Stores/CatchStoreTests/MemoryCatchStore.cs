using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;
using FishingLogBook.Web.Features.Catch.Offline.Stores;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.Stores.CatchStoreTests;

public sealed class MemoryCatchStore : ICatchStore
{
    private readonly Dictionary<Guid, CatchModel> _catches;
    private readonly Dictionary<Guid, byte[]> _photographBytes;

    public bool FailPhotographWrite { get; set; }

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
        if (ownerUserId == Guid.Empty)
        {
            throw new InvalidOperationException("A catch owner is required.");
        }

        IReadOnlyList<CatchModel> items = _catches.Values
            .Select(catchRecord => catchRecord with
            {
                Photographs = catchRecord.Photographs
                    .Select(photograph => photograph with
                    {
                        Bytes = _photographBytes.GetValueOrDefault(photograph.Id)
                    })
                    .ToArray()
            })
            .ToArray();
        return Task.FromResult(LocalCatchVisibility.ForOwner(items, ownerUserId));
    }

    public async Task<CatchModel?> GetAsync(
        Guid ownerUserId,
        Guid catchId,
        CancellationToken cancellationToken)
    {
        var catches = await GetAllAsync(ownerUserId, cancellationToken);
        return catches.SingleOrDefault(catchRecord => catchRecord.Id == catchId);
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
}
