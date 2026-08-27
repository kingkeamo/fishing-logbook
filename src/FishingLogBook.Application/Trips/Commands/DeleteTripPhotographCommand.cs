using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Trips.Commands;

public sealed class DeleteTripPhotographCommand : IRequest<DeleteTripPhotographResponse>
{
    public Guid TripId { get; init; }

    public Guid PhotographId { get; init; }
}

public sealed class DeleteTripPhotographResponse : ValidatedResponse
{
}

public sealed class DeleteTripPhotographHandler
    : IRequestHandler<DeleteTripPhotographCommand, DeleteTripPhotographResponse>
{
    private readonly ITripPhotographService _tripPhotographService;
    private readonly IMapper _mapper;

    public DeleteTripPhotographHandler(ITripPhotographService tripPhotographService, IMapper mapper)
    {
        _tripPhotographService = tripPhotographService;
        _mapper = mapper;
    }

    public async Task<DeleteTripPhotographResponse> Handle(
        DeleteTripPhotographCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _tripPhotographService.DeleteAsync(
            _mapper.Map<DeleteTripPhotographArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<DeleteTripPhotographResponse>(result.Errors[0])
            : new DeleteTripPhotographResponse();
    }
}

public sealed class DeleteTripPhotographCommandValidator : AbstractValidator<DeleteTripPhotographCommand>
{
    public DeleteTripPhotographCommandValidator()
    {
        RuleFor(command => command.TripId)
            .NotEmpty();
        RuleFor(command => command.PhotographId)
            .NotEmpty();
    }
}
