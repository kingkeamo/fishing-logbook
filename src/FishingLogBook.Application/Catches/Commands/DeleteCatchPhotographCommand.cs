using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Common.Responses;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class DeleteCatchPhotographCommand : IRequest<DeleteCatchPhotographResponse>
{
    public Guid CatchId { get; init; }

    public Guid PhotographId { get; init; }
}

public sealed class DeleteCatchPhotographResponse : ValidatedResponse
{
}

public sealed class DeleteCatchPhotographHandler
    : IRequestHandler<DeleteCatchPhotographCommand, DeleteCatchPhotographResponse>
{
    private readonly ICatchPhotographService _catchPhotographService;
    private readonly IMapper _mapper;

    public DeleteCatchPhotographHandler(ICatchPhotographService catchPhotographService, IMapper mapper)
    {
        _catchPhotographService = catchPhotographService;
        _mapper = mapper;
    }

    public async Task<DeleteCatchPhotographResponse> Handle(
        DeleteCatchPhotographCommand command,
        CancellationToken cancellationToken)
    {
        if (!_catchPhotographService.IsObjectStorageConfigured)
        {
            return ValidatedResponse.FromError<DeleteCatchPhotographResponse>(
                new CatchObjectStorageNotConfiguredError());
        }

        var result = await _catchPhotographService.DeleteAsync(
            _mapper.Map<DeleteCatchPhotographArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<DeleteCatchPhotographResponse>(result.Errors[0])
            : new DeleteCatchPhotographResponse();
    }
}

public sealed class DeleteCatchPhotographCommandValidator : AbstractValidator<DeleteCatchPhotographCommand>
{
    public DeleteCatchPhotographCommandValidator()
    {
        RuleFor(command => command.CatchId)
            .NotEmpty();
        RuleFor(command => command.PhotographId)
            .NotEmpty();
    }
}
