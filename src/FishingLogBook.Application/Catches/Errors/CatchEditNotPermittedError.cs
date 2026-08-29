using FluentResults;

namespace FishingLogBook.Application.Catches.Errors;

public sealed class CatchEditNotPermittedError : Error
{
    public CatchEditNotPermittedError()
        : base("Only the angler or the recorder may edit this catch.")
    {
    }
}
