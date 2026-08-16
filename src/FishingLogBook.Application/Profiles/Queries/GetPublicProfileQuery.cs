using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Profiles.Queries;

public sealed class GetPublicProfileQuery : IRequest<GetPublicProfileResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetPublicProfileResponse : ValidatedResponse
{
    public PublicProfileDto? Profile { get; init; }
}

public sealed class GetPublicProfileHandler : IRequestHandler<GetPublicProfileQuery, GetPublicProfileResponse>
{
    private readonly IProfileService _profileService;

    public GetPublicProfileHandler(IProfileService profileService)
    {
        _profileService = profileService;
    }

    public async Task<GetPublicProfileResponse> Handle(
        GetPublicProfileQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _profileService.GetPublicAsync(query.UserId, cancellationToken);
        if (result.IsFailed)
        {
            return new GetPublicProfileResponse
            {
                ErrorMessage = result.Errors[0].Message
            };
        }

        return new GetPublicProfileResponse
        {
            Profile = result.Value
        };
    }
}

public sealed class GetPublicProfileQueryValidator : AbstractValidator<GetPublicProfileQuery>
{
    public GetPublicProfileQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
