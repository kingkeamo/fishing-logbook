using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FluentValidation;
using MediatR;

namespace FishingLogBook.Application.Capabilities.Commands;

public sealed class RevokePlatformCapabilityCommand : IRequest<RevokePlatformCapabilityResponse>
{
    public Guid TargetUserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }
}

public sealed class RevokePlatformCapabilityResponse : ValidatedResponse
{
}

public sealed class RevokePlatformCapabilityHandler : IRequestHandler<RevokePlatformCapabilityCommand, RevokePlatformCapabilityResponse>
{
    private readonly IPlatformCapabilityService _platformCapabilityService;

    public RevokePlatformCapabilityHandler(IPlatformCapabilityService platformCapabilityService)
    {
        _platformCapabilityService = platformCapabilityService;
    }

    public async Task<RevokePlatformCapabilityResponse> Handle(
        RevokePlatformCapabilityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _platformCapabilityService.RevokeAsync(
            new RevokePlatformCapabilityArgs
            {
                TargetUserId = command.TargetUserId,
                Capability = command.Capability
            },
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<RevokePlatformCapabilityResponse>(result.Errors[0]);
        }

        return new RevokePlatformCapabilityResponse();
    }
}

public sealed class RevokePlatformCapabilityCommandValidator : AbstractValidator<RevokePlatformCapabilityCommand>
{
    public RevokePlatformCapabilityCommandValidator()
    {
        RuleFor(command => command.TargetUserId)
            .NotEmpty();
        RuleFor(command => command.Capability)
            .IsInEnum();
    }
}
