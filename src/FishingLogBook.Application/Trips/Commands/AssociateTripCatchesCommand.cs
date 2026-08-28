using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class AssociateTripCatchesCommand : IRequest<AssociateTripCatchesResponse>
{
    public Guid TripId { get; init; }

    public IReadOnlyList<Guid> CatchIds { get; init; } = [];
}

public sealed class AssociateTripCatchesResponse : ValidatedResponse
{
    public TripCatchAssociationDto? Association { get; init; }
}

public sealed class AssociateTripCatchesHandler
    : IRequestHandler<AssociateTripCatchesCommand, AssociateTripCatchesResponse>
{
    private readonly ITripCatchService _tripCatchService;

    public AssociateTripCatchesHandler(ITripCatchService tripCatchService)
    {
        _tripCatchService = tripCatchService;
    }

    public async Task<AssociateTripCatchesResponse> Handle(
        AssociateTripCatchesCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripCatchService.AssociateAsync(
            new AssociateTripCatchesArgs
            {
                TripId = command.TripId,
                CatchIds = command.CatchIds
            },
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<AssociateTripCatchesResponse>(result.Errors[0]);
        }

        return new AssociateTripCatchesResponse
        {
            Association = result.Value
        };
    }
}

public sealed class AssociateTripCatchesCommandValidator
    : AbstractValidator<AssociateTripCatchesCommand>
{
    private const int MaxCatchesPerRequest = 50;

    public AssociateTripCatchesCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.CatchIds)
            .NotEmpty()
            .WithMessage("Choose at least one catch to add to the trip.");
        RuleFor(command => command.CatchIds)
            .Must(catchIds => catchIds.Count <= MaxCatchesPerRequest)
            .WithMessage($"A trip accepts at most {MaxCatchesPerRequest} catches in one request.");
        RuleForEach(command => command.CatchIds)
            .NotEmpty();
    }
}
