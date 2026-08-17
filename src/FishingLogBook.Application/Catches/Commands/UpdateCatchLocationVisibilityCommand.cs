using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Shared.Dtos;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Catches.Commands;

public sealed class UpdateCatchLocationVisibilityCommand : IRequest<UpdateCatchLocationVisibilityResponse>
{
    public Guid CatchId { get; init; }

    public string Visibility { get; init; } = string.Empty;
}

public sealed class UpdateCatchLocationVisibilityResponse : ValidatedResponse
{
}

public sealed class UpdateCatchLocationVisibilityHandler
    : IRequestHandler<UpdateCatchLocationVisibilityCommand, UpdateCatchLocationVisibilityResponse>
{
    private readonly ICatchService _catchService;

    public UpdateCatchLocationVisibilityHandler(ICatchService catchService)
    {
        _catchService = catchService;
    }

    public async Task<UpdateCatchLocationVisibilityResponse> Handle(
        UpdateCatchLocationVisibilityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _catchService.UpdateLocationVisibilityAsync(
            command.Adapt<UpdateCatchLocationVisibilityArgs>(),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<UpdateCatchLocationVisibilityResponse>(result.Errors[0]);
        }

        return new UpdateCatchLocationVisibilityResponse();
    }
}

public sealed class UpdateCatchLocationVisibilityCommandValidator
    : AbstractValidator<UpdateCatchLocationVisibilityCommand>
{
    public UpdateCatchLocationVisibilityCommandValidator()
    {
        RuleFor(command => command.CatchId)
            .NotEmpty();
        RuleFor(command => command.Visibility)
            .Must(LocationDefaults.IsKnownVisibility)
            .WithMessage("Location visibility is not supported.");
    }
}
