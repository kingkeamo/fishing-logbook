using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
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
    private readonly IMapper _mapper;

    public GetCatchHandler(ICatchService catchService, IMapper mapper)
    {
        _catchService = catchService;
        _mapper = mapper;
    }

    public async Task<GetCatchResponse> Handle(GetCatchQuery query, CancellationToken cancellationToken)
    {
        var result = await _catchService.GetViewAsync(
            _mapper.Map<GetCatchArgs>(query),
            cancellationToken);
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
