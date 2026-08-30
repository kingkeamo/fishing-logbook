using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class RecordTripPhotographCommand : IRequest<RecordTripPhotographResponse>
{
    public Guid TripId { get; init; }

    public RecordTripPhotographDto Photograph { get; init; } =
        new(Guid.Empty, string.Empty, string.Empty, default);
}

public sealed class RecordTripPhotographResponse : ValidatedResponse
{
    public TripPhotographDto? Photograph { get; init; }
}

public sealed class RecordTripPhotographHandler
    : IRequestHandler<RecordTripPhotographCommand, RecordTripPhotographResponse>
{
    private readonly ITripPhotographService _tripPhotographService;
    private readonly IMapper _mapper;

    public RecordTripPhotographHandler(ITripPhotographService tripPhotographService, IMapper mapper)
    {
        _tripPhotographService = tripPhotographService;
        _mapper = mapper;
    }

    public async Task<RecordTripPhotographResponse> Handle(
        RecordTripPhotographCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripPhotographService.RecordAsync(
            _mapper.Map<RecordTripPhotographArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<RecordTripPhotographResponse>(result.Errors[0]);
        }

        return new RecordTripPhotographResponse
        {
            Photograph = result.Value
        };
    }
}

public sealed class RecordTripPhotographCommandValidator
    : AbstractValidator<RecordTripPhotographCommand>
{
    public RecordTripPhotographCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.Photograph.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Photograph.ObjectKey)
            .Must(value => !string.IsNullOrWhiteSpace(value));
        RuleFor(command => command.Photograph.AddedOn)
            .Must(value => value != default)
            .WithMessage("A trip photograph must record when it was added.");
        RuleFor(command => command.Photograph.ContentType)
            .Must(PhotographContentTypeConstants.IsAllowed)
            .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }
}
