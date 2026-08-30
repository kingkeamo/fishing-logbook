using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.OfflineAccess.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.OfflineAccess.Queries;

public sealed class GetOfflineAccessPreferenceQuery : IRequest<GetOfflineAccessPreferenceResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetOfflineAccessPreferenceResponse : ValidatedResponse
{
    public OfflineAccessPreferenceDto? Preference { get; init; }
}

public sealed class GetOfflineAccessPreferenceHandler : IRequestHandler<GetOfflineAccessPreferenceQuery, GetOfflineAccessPreferenceResponse>
{
    private readonly IOfflineAccessPreferenceService _service;

    public GetOfflineAccessPreferenceHandler(IOfflineAccessPreferenceService service) => _service = service;

    public async Task<GetOfflineAccessPreferenceResponse> Handle(GetOfflineAccessPreferenceQuery query, CancellationToken cancellationToken)
    {
        var result = await _service.GetAsync(query.UserId, cancellationToken);
        return result.IsFailed
            ? new GetOfflineAccessPreferenceResponse { ErrorMessage = result.Errors[0].Message }
            : new GetOfflineAccessPreferenceResponse { Preference = result.Value };
    }
}

public sealed class GetOfflineAccessPreferenceQueryValidator : AbstractValidator<GetOfflineAccessPreferenceQuery>
{
    public GetOfflineAccessPreferenceQueryValidator() => RuleFor(query => query.UserId).NotEmpty();
}
