using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class UpsertTripCommand : IRequest<UpsertTripResponse>
{
    public Guid UserId { get; init; }

    public TripDto Trip { get; init; } = new(Guid.Empty, string.Empty, default);
}

public sealed class UpsertTripResponse : ValidatedResponse
{
    public TripDto? Trip { get; init; }
}

public sealed class UpsertTripHandler : IRequestHandler<UpsertTripCommand, UpsertTripResponse>
{
    private readonly ITripService _tripService;
    private readonly IMapper _mapper;

    public UpsertTripHandler(ITripService tripService, IMapper mapper)
    {
        _tripService = tripService;
        _mapper = mapper;
    }

    public async Task<UpsertTripResponse> Handle(
        UpsertTripCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripService.UpsertAsync(
            _mapper.Map<UpsertTripArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<UpsertTripResponse>(result.Errors[0]);
        }

        return new UpsertTripResponse
        {
            Trip = result.Value
        };
    }
}

public sealed class UpsertTripCommandValidator : AbstractValidator<UpsertTripCommand>
{
    public UpsertTripCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Trip.Id)
            .NotEmpty();
        RuleFor(command => command.Trip.Status)
            .Must(TripConstants.IsKnownStatus)
            .WithMessage("Trip status is not supported.");
        RuleFor(command => command.Trip.StartedOn)
            .NotEqual(default(DateTimeOffset))
            .Must(startedOn => TripConstants.IsStartedOnValid(startedOn, DateTimeOffset.UtcNow))
            .WithMessage("Trip start time cannot be in the future.");
        RuleFor(command => command.Trip)
            .Must(trip => TripConstants.IsEndedOnValid(trip.StartedOn, trip.EndedOn, DateTimeOffset.UtcNow))
            .WithMessage("Trip end time must not be before the start or in the future.");
        RuleFor(command => command.Trip)
            .Must(trip => trip.Status != TripConstants.Active || trip.EndedOn is null)
            .WithMessage("An active trip cannot have an end time.");
        RuleFor(command => command.Trip.Title)
            .MaximumLength(TripConstants.MaxTitleLength);
        RuleFor(command => command.Trip.PlaceName)
            .MaximumLength(TripConstants.MaxPlaceNameLength);
        When(command => command.Trip.Location is not null, () =>
        {
            RuleFor(command => command.Trip.Location!.Latitude)
                .InclusiveBetween(CatchLocationConstants.MinLatitude, CatchLocationConstants.MaxLatitude);
            RuleFor(command => command.Trip.Location!.Longitude)
                .InclusiveBetween(CatchLocationConstants.MinLongitude, CatchLocationConstants.MaxLongitude);
            RuleFor(command => command.Trip.Location!.CapturedOn)
                .NotEqual(default(DateTimeOffset));
            RuleFor(command => command.Trip.Location!.Source)
                .NotEmpty();
            RuleFor(command => command.Trip.Location!.Visibility)
                .Must(LocationDefaults.IsKnownVisibility)
                .WithMessage("Location visibility is not supported.");
            RuleFor(command => command.Trip.Location!.ConsentVersion)
                .NotEmpty();
        });
    }
}
