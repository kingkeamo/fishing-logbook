using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Constants;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class UpsertCatchCommand : IRequest<UpsertCatchResponse>
{
    public Guid UserId { get; init; }

    public CatchDto Catch { get; init; } = new(Guid.Empty, default, []);
}

public sealed class UpsertCatchResponse : ValidatedResponse
{
    public CatchDto? Catch { get; init; }
}

public sealed class UpsertCatchHandler : IRequestHandler<UpsertCatchCommand, UpsertCatchResponse>
{
    private readonly ICatchService _catchService;

    public UpsertCatchHandler(ICatchService catchService)
    {
        _catchService = catchService;
    }

    public async Task<UpsertCatchResponse> Handle(
        UpsertCatchCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _catchService.UpsertAsync(
            command.Adapt<UpsertCatchArgs>(),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<UpsertCatchResponse>(result.Errors[0]);
        }

        return new UpsertCatchResponse
        {
            Catch = result.Value
        };
    }
}

public sealed class UpsertCatchCommandValidator : AbstractValidator<UpsertCatchCommand>
{
    public UpsertCatchCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();
        RuleFor(command => command.Catch.Id)
            .NotEmpty();
        RuleFor(command => command.Catch.CaughtOn)
            .NotEqual(default(DateTimeOffset));
        RuleFor(command => command.Catch.Photographs)
            .NotEmpty()
            .WithMessage("A catch requires at least one photograph.");
        RuleForEach(command => command.Catch.Photographs)
            .ChildRules(photograph =>
            {
                photograph.RuleFor(item => item.Id)
                    .NotEmpty();
                photograph.RuleFor(item => item.ContentType)
                    .Must(PhotographContentTypeConstants.IsAllowed)
                    .WithMessage("Photograph content type must be image/jpeg, image/png, or image/webp.");
            });
        RuleForEach(command => command.Catch.Photographs)
            .Must((command, photograph) => photograph.CatchId == command.Catch.Id)
            .WithMessage("Each photograph must belong to the catch.");
        When(command => command.Catch.Location is not null, () =>
        {
            RuleFor(command => command.Catch.Location!.Latitude)
                .InclusiveBetween(CatchLocationConstants.MinLatitude, CatchLocationConstants.MaxLatitude);
            RuleFor(command => command.Catch.Location!.Longitude)
                .InclusiveBetween(CatchLocationConstants.MinLongitude, CatchLocationConstants.MaxLongitude);
            RuleFor(command => command.Catch.Location!.CapturedOn)
                .NotEqual(default(DateTimeOffset));
            RuleFor(command => command.Catch.Location!.Source)
                .NotEmpty();
            RuleFor(command => command.Catch.Location!.Visibility)
                .NotEmpty();
            RuleFor(command => command.Catch.Location!.ConsentVersion)
                .NotEmpty();
        });
    }
}
