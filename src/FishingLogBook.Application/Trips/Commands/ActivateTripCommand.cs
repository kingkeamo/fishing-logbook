using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class ActivateTripCommand : IRequest<ActivateTripResponse>
{
    public Guid TripId { get; init; }
}

public sealed class ActivateTripResponse : ValidatedResponse
{
    public TripDto? Trip { get; init; }
}

public sealed class ActivateTripHandler : IRequestHandler<ActivateTripCommand, ActivateTripResponse>
{
    private readonly ITripService _tripService;
    private readonly IMapper _mapper;

    public ActivateTripHandler(ITripService tripService, IMapper mapper)
    {
        _tripService = tripService;
        _mapper = mapper;
    }

    public async Task<ActivateTripResponse> Handle(
        ActivateTripCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripService.ActivateAsync(
            _mapper.Map<ActivateTripArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<ActivateTripResponse>(result.Errors[0]);
        }

        return new ActivateTripResponse
        {
            Trip = result.Value
        };
    }
}

public sealed class ActivateTripCommandValidator : AbstractValidator<ActivateTripCommand>
{
    public ActivateTripCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
    }
}
