using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FishingLogBook.Shared.Enums;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class UpdateOwnProfileCommand : IRequest<UpdateOwnProfileResponse>
{
    public Guid UserId { get; init; }

    public UpdateProfileDto Profile { get; init; } = new(
        null,
        null,
        [],
        [],
        true,
        false,
        false,
        false,
        false);
}

public sealed class UpdateOwnProfileResponse : ValidatedResponse
{
    public ProfileDto? Profile { get; init; }
}

public sealed class UpdateOwnProfileHandler : IRequestHandler<UpdateOwnProfileCommand, UpdateOwnProfileResponse>
{
    private readonly IProfileService _profileService;

    public UpdateOwnProfileHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<UpdateOwnProfileResponse> Handle(
        UpdateOwnProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.UpdateOwnAsync(
            command.Adapt<UpdateProfileArgs>(),
            cancellationToken);
        if (result.IsFailed)
        {
            return new UpdateOwnProfileResponse
            {
                ErrorMessage = result.Errors[0].Message
            };
        }

        return new UpdateOwnProfileResponse
        {
            Profile = result.Value
        };
    }
}

public sealed class UpdateOwnProfileCommandValidator : AbstractValidator<UpdateOwnProfileCommand>
{
    public UpdateOwnProfileCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Profile)
            .NotNull();
        RuleFor(command => command.Profile.DisplayName)
            .MaximumLength(100)
            .When(command => command.Profile.DisplayName is not null);
        RuleFor(command => command.Profile.HomeRegion)
            .MaximumLength(200)
            .When(command => command.Profile.HomeRegion is not null);
        RuleForEach(command => command.Profile.PreferredFishingTypes)
            .Must(BeKnownFishingType)
            .WithMessage("Preferred fishing type is not recognised.");
        RuleForEach(command => command.Profile.PreferredSpecies)
            .Must(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 50)
            .WithMessage("Preferred species must be 50 characters or fewer.");
    }

    private static bool BeKnownFishingType(string value)
    {
        return Enum.TryParse<FishingTypeEnum>(value, ignoreCase: true, out _);
    }
}
