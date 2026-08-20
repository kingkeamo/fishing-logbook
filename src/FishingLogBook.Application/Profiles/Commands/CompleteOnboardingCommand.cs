using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class CompleteOnboardingCommand : IRequest<CompleteOnboardingResponse>
{
    public Guid UserId { get; init; }
}

public sealed class CompleteOnboardingResponse : ValidatedResponse
{
    public ProfileDto? Profile { get; init; }
}

public sealed class CompleteOnboardingHandler : IRequestHandler<CompleteOnboardingCommand, CompleteOnboardingResponse>
{
    private readonly IProfileService _profileService;

    public CompleteOnboardingHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<CompleteOnboardingResponse> Handle(
        CompleteOnboardingCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.CompleteOnboardingAsync(command.UserId, cancellationToken);
        return result.IsFailed
            ? new CompleteOnboardingResponse { ErrorMessage = result.Errors[0].Message }
            : new CompleteOnboardingResponse { Profile = result.Value };
    }
}

public sealed class CompleteOnboardingCommandValidator : AbstractValidator<CompleteOnboardingCommand>
{
    public CompleteOnboardingCommandValidator()
    {
        RuleFor(command => command.UserId).NotEmpty();
    }
}
