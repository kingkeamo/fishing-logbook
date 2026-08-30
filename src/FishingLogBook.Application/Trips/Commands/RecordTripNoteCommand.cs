using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Trips.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class RecordTripNoteCommand : IRequest<RecordTripNoteResponse>
{
    public Guid TripId { get; init; }

    public RecordTripNoteDto Note { get; init; } = new(Guid.Empty, string.Empty, default);
}

public sealed class RecordTripNoteResponse : ValidatedResponse
{
    public TripNoteDto? Note { get; init; }
}

public sealed class RecordTripNoteHandler
    : IRequestHandler<RecordTripNoteCommand, RecordTripNoteResponse>
{
    private readonly ITripNoteService _tripNoteService;
    private readonly IMapper _mapper;

    public RecordTripNoteHandler(ITripNoteService tripNoteService, IMapper mapper)
    {
        _tripNoteService = tripNoteService;
        _mapper = mapper;
    }

    public async Task<RecordTripNoteResponse> Handle(
        RecordTripNoteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripNoteService.RecordAsync(
            _mapper.Map<RecordTripNoteArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<RecordTripNoteResponse>(result.Errors[0]);
        }

        return new RecordTripNoteResponse
        {
            Note = result.Value
        };
    }
}

public sealed class RecordTripNoteCommandValidator : AbstractValidator<RecordTripNoteCommand>
{
    public RecordTripNoteCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.Note.NoteId)
            .NotEmpty();
        RuleFor(command => command.Note.RecordedOn)
            .Must(value => value != default)
            .WithMessage("A trip note must record when it was written.");
        RuleFor(command => command.Note.Text)
            .Must(TripConstants.IsNoteTextValid)
            .WithMessage($"A trip note must have text of {TripConstants.MaxNoteTextLength} characters or fewer.");
    }
}
