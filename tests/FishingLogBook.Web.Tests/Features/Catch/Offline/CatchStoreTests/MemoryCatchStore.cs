using FishingLogBook.Web.Features.Catch.Models;
using FishingLogBook.Web.Features.Catch.Offline;

namespace FishingLogBook.Web.Tests.Features.Catch.Offline.CatchStoreTests;

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

    public Task<IReadOnlyList<CatchModel>> GetAllAsync(CancellationToken cancellationToken)
    {
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
        return Task.FromResult(items);
    }
}
