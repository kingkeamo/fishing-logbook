using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class CorrectCatchAnglerCommand : IRequest<CorrectCatchAnglerResponse>
{
    public Guid CatchId { get; init; }

    public Guid CaughtByUserId { get; init; }
}

public sealed class CorrectCatchAnglerResponse : ValidatedResponse
{
    public CatchViewDto? Catch { get; init; }
}

public sealed class CorrectCatchAnglerHandler
    : IRequestHandler<CorrectCatchAnglerCommand, CorrectCatchAnglerResponse>
{
    private readonly ICatchService _catchService;
    private readonly IMapper _mapper;

    public CorrectCatchAnglerHandler(ICatchService catchService, IMapper mapper)
    {
        _catchService = catchService;
        _mapper = mapper;
    }

    public async Task<CorrectCatchAnglerResponse> Handle(
        CorrectCatchAnglerCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _catchService.CorrectAnglerAsync(
            _mapper.Map<CorrectCatchAnglerArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<CorrectCatchAnglerResponse>(result.Errors[0]);
        }

        return new CorrectCatchAnglerResponse { Catch = result.Value };
    }
}

public sealed class CorrectCatchAnglerCommandValidator : AbstractValidator<CorrectCatchAnglerCommand>
{
    public CorrectCatchAnglerCommandValidator()
    {
        RuleFor(command => command.CatchId)
            .NotEmpty();
        RuleFor(command => command.CaughtByUserId)
            .NotEmpty();
    }
}
