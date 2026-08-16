using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Profiles.Commands;

public sealed class GetOwnProfileCommand : IRequest<GetOwnProfileResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetOwnProfileResponse : ValidatedResponse
{
    public ProfileDto? Profile { get; init; }
}

public sealed class GetOwnProfileHandler : IRequestHandler<GetOwnProfileCommand, GetOwnProfileResponse>
{
    private readonly IProfileService _profileService;

    public GetOwnProfileHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<GetOwnProfileResponse> Handle(
        GetOwnProfileCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetOrCreateOwnAsync(command.UserId, cancellationToken);
        if (result.IsFailed)
        {
            return new GetOwnProfileResponse
            {
                ErrorMessage = result.Errors[0].Message
            };
        }

        return new GetOwnProfileResponse
        {
            Profile = result.Value
        };
    }
}

public sealed class GetOwnProfileCommandValidator : AbstractValidator<GetOwnProfileCommand>
{
    public GetOwnProfileCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
    }
}
