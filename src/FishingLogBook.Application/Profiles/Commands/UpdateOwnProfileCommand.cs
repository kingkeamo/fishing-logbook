using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class UpdateOwnProfileCommand : IRequest<UpdateOwnProfileResponse>
{
    public Guid UserId { get; init; }

    public UpdateProfileDto Profile { get; init; } = new(
        null,
        null,
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
    private readonly IMapper _mapper;

    public UpdateOwnProfileHandler(IProfileService profileService, IMapper mapper)
    {
        _profileService = profileService;
        _mapper = mapper;
    }

    public async Task<UpdateOwnProfileResponse> Handle(
        UpdateOwnProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.UpdateOwnAsync(
            _mapper.Map<UpdateProfileArgs>(command),
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
            .MaximumLength(ProfileDetailConstants.MaxDisplayNameLength)
            .When(command => command.Profile.DisplayName is not null);
        RuleFor(command => command.Profile.HomeRegion)
            .MaximumLength(ProfileDetailConstants.MaxHomeRegionLength)
            .When(command => command.Profile.HomeRegion is not null);
        RuleFor(command => command.Profile.PreferredWeightUnit)
            .IsInEnum()
            .WithMessage("Preferred weight unit is not recognised.");
        RuleFor(command => command.Profile.PreferredLengthUnit)
            .IsInEnum()
            .WithMessage("Preferred length unit is not recognised.");
    }
}
