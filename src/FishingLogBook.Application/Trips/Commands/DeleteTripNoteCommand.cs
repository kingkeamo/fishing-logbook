using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class DeleteTripNoteCommand : IRequest<DeleteTripNoteResponse>
{
    public Guid TripId { get; init; }

    public Guid NoteId { get; init; }
}

public sealed class DeleteTripNoteResponse : ValidatedResponse
{
}

public sealed class DeleteTripNoteHandler
    : IRequestHandler<DeleteTripNoteCommand, DeleteTripNoteResponse>
{
    private readonly ITripNoteService _tripNoteService;
    private readonly IMapper _mapper;

    public DeleteTripNoteHandler(ITripNoteService tripNoteService, IMapper mapper)
    {
        _tripNoteService = tripNoteService;
        _mapper = mapper;
    }

    public async Task<DeleteTripNoteResponse> Handle(
        DeleteTripNoteCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripNoteService.DeleteAsync(
            _mapper.Map<DeleteTripNoteArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<DeleteTripNoteResponse>(result.Errors[0])
            : new DeleteTripNoteResponse();
    }
}

public sealed class DeleteTripNoteCommandValidator : AbstractValidator<DeleteTripNoteCommand>
{
    public DeleteTripNoteCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.NoteId)
            .NotEmpty();
    }
}
