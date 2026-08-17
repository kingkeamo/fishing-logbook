using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Profiles.Queries;

public sealed class GetOwnProfileQuery : IRequest<GetOwnProfileResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetOwnProfileResponse : ValidatedResponse
{
    public ProfileDto? Profile { get; init; }
}

public sealed class GetOwnProfileHandler : IRequestHandler<GetOwnProfileQuery, GetOwnProfileResponse>
{
    private readonly IProfileService _profileService;

    public GetOwnProfileHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<GetOwnProfileResponse> Handle(
        GetOwnProfileQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetOwnAsync(query.UserId, cancellationToken);
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

public sealed class GetOwnProfileQueryValidator : AbstractValidator<GetOwnProfileQuery>
{
    public GetOwnProfileQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
