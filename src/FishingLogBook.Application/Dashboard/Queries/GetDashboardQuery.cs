using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Dashboard.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Dashboard.Queries;

public sealed class GetDashboardQuery : IRequest<GetDashboardResponse>
{
    public Guid UserId { get; init; }
    public DateTimeOffset? FromInclusive { get; init; }
    public DateTimeOffset? ToExclusive { get; init; }
    public string? Species { get; init; }
    public string? Method { get; init; }
    public string? Venue { get; init; }
}

public sealed class GetDashboardResponse : ValidatedResponse
{
    public DashboardDto? Dashboard { get; init; }
}

public sealed class GetDashboardHandler(IDashboardService service, IMapper mapper) : IRequestHandler<GetDashboardQuery, GetDashboardResponse>
{
    public async Task<GetDashboardResponse> Handle(GetDashboardQuery query, CancellationToken cancellationToken)
    {
        var result = await service.GetAsync(mapper.Map<GetDashboardArgs>(query), cancellationToken);
        return result.IsFailed
            ? ValidatedResponse.FromError<GetDashboardResponse>(result.Errors[0])
            : new GetDashboardResponse { Dashboard = result.Value };
    }
}

public sealed class GetDashboardQueryValidator : AbstractValidator<GetDashboardQuery>
{
    public GetDashboardQueryValidator()
    {
        RuleFor(query => query.UserId).NotEmpty();
        RuleFor(query => query.ToExclusive)
            .Must((query, end) => query.FromInclusive is null || end is null || end > query.FromInclusive)
            .WithMessage("The end of the date range must follow its start.");
        RuleFor(query => query.Species).MaximumLength(200);
        RuleFor(query => query.Method).MaximumLength(200);
        RuleFor(query => query.Venue).MaximumLength(200);
    }
}
