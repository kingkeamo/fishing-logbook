using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Catches.Queries;

public sealed class GetCatchQuery : IRequest<GetCatchResponse>
{
    public Guid CatchId { get; init; }
}

public sealed class GetCatchResponse : ValidatedResponse
{
    public CatchViewDto? Catch { get; init; }
}

public sealed class GetCatchHandler : IRequestHandler<GetCatchQuery, GetCatchResponse>
{
    private readonly ICatchService _catchService;

    public GetCatchHandler(ICatchService catchService)
    {
        _catchService = catchService;
    }

    public async Task<GetCatchResponse> Handle(GetCatchQuery query, CancellationToken cancellationToken)
    {
        var result = await _catchService.GetViewAsync(query.Adapt<GetCatchArgs>(), cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetCatchResponse>(result.Errors[0]);
        }

        return new GetCatchResponse
        {
            Catch = result.Value
        };
    }
}

public sealed class GetCatchQueryValidator : AbstractValidator<GetCatchQuery>
{
    public GetCatchQueryValidator()
    {
        RuleFor(query => query.CatchId)
            .NotEmpty();
    }
}
