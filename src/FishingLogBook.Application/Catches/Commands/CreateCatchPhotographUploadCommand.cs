using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Errors;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class CreateCatchPhotographUploadCommand : IRequest<CreateCatchPhotographUploadResponse>
{
    public Guid CatchId { get; init; }

    public PhotographUploadRequestDto Request { get; init; } = new(Guid.Empty, string.Empty);
}

public sealed class CreateCatchPhotographUploadResponse : ValidatedResponse
{
    public PhotographUploadDto? Upload { get; init; }
}

public sealed class CreateCatchPhotographUploadHandler
    : IRequestHandler<CreateCatchPhotographUploadCommand, CreateCatchPhotographUploadResponse>
{
    private readonly ICatchPhotographService _catchPhotographService;

    public CreateCatchPhotographUploadHandler(ICatchPhotographService catchPhotographService)
    {
        _catchPhotographService = catchPhotographService;
    }

    public async Task<CreateCatchPhotographUploadResponse> Handle(
        CreateCatchPhotographUploadCommand command,
        CancellationToken cancellationToken)
    {
        if (!_catchPhotographService.IsObjectStorageConfigured)
        {
            return ValidatedResponse.FromError<CreateCatchPhotographUploadResponse>(
                new CatchObjectStorageNotConfiguredError());
        }

        var result = await _catchPhotographService.CreateUploadAsync(
            command.Adapt<CreateCatchPhotographUploadArgs>(),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<CreateCatchPhotographUploadResponse>(result.Errors[0]);
        }

        return new CreateCatchPhotographUploadResponse
        {
            Upload = result.Value
        };
    }
}

public sealed class CreateCatchPhotographUploadCommandValidator
    : AbstractValidator<CreateCatchPhotographUploadCommand>
{
    public CreateCatchPhotographUploadCommandValidator()
    {
        RuleFor(command => command.CatchId)
            .NotEmpty();
        RuleFor(command => command.Request.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Request.ContentType)
            .Must(PhotographContentTypeConstants.IsAllowed)
            .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
    }
}
