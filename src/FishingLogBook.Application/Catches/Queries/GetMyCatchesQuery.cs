using FishingLogBook.Application.Args;
using FishingLogBook.Application.Catches.Contracts.Services;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Catches.Queries;

public sealed class GetMyCatchesQuery : IRequest<GetMyCatchesResponse>
{
    public Guid UserId { get; init; }
}

public sealed class GetMyCatchesResponse : ValidatedResponse
{
    public IReadOnlyList<CatchViewDto> Catches { get; init; } = [];
}

public sealed class GetMyCatchesHandler : IRequestHandler<GetMyCatchesQuery, GetMyCatchesResponse>
{
    private readonly ICatchService _catchService;
    private readonly IMapper _mapper;

    public GetMyCatchesHandler(ICatchService catchService, IMapper mapper)
    {
        _catchService = catchService;
        _mapper = mapper;
    }

    public async Task<GetMyCatchesResponse> Handle(GetMyCatchesQuery query, CancellationToken cancellationToken)
    {
        var result = await _catchService.GetMyAsync(
            _mapper.Map<GetMyCatchesArgs>(query),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GetMyCatchesResponse>(result.Errors[0]);
        }

        return new GetMyCatchesResponse
        {
            Catches = result.Value
        };
    }
}

public sealed class GetMyCatchesQueryValidator : AbstractValidator<GetMyCatchesQuery>
{
    public GetMyCatchesQueryValidator()
    {
        RuleFor(query => query.UserId)
            .NotEmpty();
    }
}
