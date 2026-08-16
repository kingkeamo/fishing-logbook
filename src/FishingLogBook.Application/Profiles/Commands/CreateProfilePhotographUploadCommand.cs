using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class CreateProfilePhotographUploadCommand : IRequest<CreateProfilePhotographUploadResponse>
{
    public Guid UserId { get; init; }

    public PhotographUploadRequestDto Request { get; init; } = new(Guid.Empty, string.Empty);
}

public sealed class CreateProfilePhotographUploadResponse : ValidatedResponse
{
    public PhotographUploadDto? Upload { get; init; }
}

public sealed class CreateProfilePhotographUploadHandler
    : IRequestHandler<CreateProfilePhotographUploadCommand, CreateProfilePhotographUploadResponse>
{
    private readonly IProfileService _profileService;

    public CreateProfilePhotographUploadHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<CreateProfilePhotographUploadResponse> Handle(
        CreateProfilePhotographUploadCommand command,
        CancellationToken cancellationToken)
    {
        if (!_profileService.IsObjectStorageConfigured)
        {
            return new CreateProfilePhotographUploadResponse
            {
                ErrorMessage = "Object storage is not configured."
            };
        }

        var result = await _profileService.CreatePhotographUploadAsync(
            command.UserId,
            command.Request,
            cancellationToken);
        if (result.IsFailed)
        {
            return new CreateProfilePhotographUploadResponse
            {
                ErrorMessage = result.Errors[0].Message
            };
        }

        return new CreateProfilePhotographUploadResponse
        {
            Upload = result.Value
        };
    }
}

public sealed class CreateProfilePhotographUploadCommandValidator
    : AbstractValidator<CreateProfilePhotographUploadCommand>
{
    public CreateProfilePhotographUploadCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Request.PhotographId)
            .NotEmpty();
        RuleFor(command => command.Request.ContentType)
            .Must(value => !string.IsNullOrWhiteSpace(value)
                && value.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Photograph content type must be an image.");
    }
}
