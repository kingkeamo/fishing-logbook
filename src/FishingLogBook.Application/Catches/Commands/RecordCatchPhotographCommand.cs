using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class RecordCatchPhotographCommand : IRequest<RecordCatchPhotographResponse>
{
    public Guid CatchId { get; init; }

    public RecordPhotographDto Photograph { get; init; } =
        new(Guid.Empty, string.Empty, string.Empty);
}

public sealed class RecordCatchPhotographResponse : ValidatedResponse
{
}

public sealed class RecordCatchPhotographHandler
    : IRequestHandler<RecordCatchPhotographCommand, RecordCatchPhotographResponse>
{
    private readonly ICatchPhotographService _catchPhotographService;
    private readonly IMapper _mapper;

    public RecordCatchPhotographHandler(ICatchPhotographService catchPhotographService, IMapper mapper)
    {
        _catchPhotographService = catchPhotographService;
        _mapper = mapper;
    }

    public async Task<RecordCatchPhotographResponse> Handle(
        RecordCatchPhotographCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _catchPhotographService.RecordAsync(
            _mapper.Map<RecordCatchPhotographArgs>(command),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<RecordCatchPhotographResponse>(result.Errors[0])
            : new RecordCatchPhotographResponse();
    }
}

public sealed class RecordCatchPhotographCommandValidator
    : AbstractValidator<RecordCatchPhotographCommand>
{
    public RecordCatchPhotographCommandValidator()
    {
        RuleFor(command => command.CatchId)
            .NotEmpty();
        RuleFor(command => command.Photograph.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Photograph.ObjectKey)
            .Must(value => !string.IsNullOrWhiteSpace(value));
        RuleFor(command => command.Photograph.ContentType)
            .Must(PhotographContentTypeConstants.IsAllowed)
            .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }
}
