using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Profiles.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Profiles.Queries;

public sealed class FindAnglersQuery : IRequest<FindAnglersResponse>
{
    public Guid RequestingUserId { get; init; }

    public string Query { get; init; } = string.Empty;

    public int MaxResults { get; init; } = AnglerLookupConstants.MaxResults;
}

public sealed class FindAnglersResponse : ValidatedResponse
{
    public IReadOnlyList<AnglerSummaryDto> Anglers { get; init; } = [];
}

public sealed class FindAnglersHandler : IRequestHandler<FindAnglersQuery, FindAnglersResponse>
{
    private readonly IAnglerLookupService _anglerLookupService;
    private readonly IMapper _mapper;

    public FindAnglersHandler(IAnglerLookupService anglerLookupService, IMapper mapper)
    {
        _anglerLookupService = anglerLookupService;
        _mapper = mapper;
    }

    public async Task<FindAnglersResponse> Handle(FindAnglersQuery query, CancellationToken cancellationToken)
    {
        var result = await _anglerLookupService.FindAsync(
            _mapper.Map<FindAnglersArgs>(query),
            cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<FindAnglersResponse>(result.Errors[0])
            : new FindAnglersResponse { Anglers = result.Value };
    }
}

public sealed class FindAnglersQueryValidator : AbstractValidator<FindAnglersQuery>
{
    public FindAnglersQueryValidator()
    {
        RuleFor(query => query.RequestingUserId)
            .NotEmpty();
        RuleFor(query => query.Query)
            .NotEmpty()
            .MinimumLength(AnglerLookupConstants.MinQueryLength)
            .MaximumLength(AnglerLookupConstants.MaxQueryLength);
        RuleFor(query => query.MaxResults)
            .InclusiveBetween(1, AnglerLookupConstants.MaxResults);
    }
}
