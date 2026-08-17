using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FishingLogBook.Domain.Enums;
using FluentValidation;
using Mapster;
using MediatR;

namespace FishingLogBook.Application.Capabilities.Commands;

public sealed class GrantPlatformCapabilityCommand : IRequest<GrantPlatformCapabilityResponse>
{
    public Guid TargetUserId { get; init; }

    public PlatformCapabilityEnum Capability { get; init; }
}

public sealed class GrantPlatformCapabilityResponse : ValidatedResponse
{
}

public sealed class GrantPlatformCapabilityHandler : IRequestHandler<GrantPlatformCapabilityCommand, GrantPlatformCapabilityResponse>
{
    private readonly IPlatformCapabilityService _platformCapabilityService;

    public GrantPlatformCapabilityHandler(IPlatformCapabilityService platformCapabilityService)
    {
        _platformCapabilityService = platformCapabilityService;
    }

    public async Task<GrantPlatformCapabilityResponse> Handle(
        GrantPlatformCapabilityCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _platformCapabilityService.GrantAsync(
            command.Adapt<GrantPlatformCapabilityArgs>(),
            cancellationToken);
        if (result.IsFailed)
        {
            return ValidatedResponse.FromError<GrantPlatformCapabilityResponse>(result.Errors[0]);
        }

        return new GrantPlatformCapabilityResponse();
    }
}

public sealed class GrantPlatformCapabilityCommandValidator : AbstractValidator<GrantPlatformCapabilityCommand>
{
    public GrantPlatformCapabilityCommandValidator()
    {
        RuleFor(command => command.TargetUserId)
            .NotEmpty();
        RuleFor(command => command.Capability)
            .IsInEnum();
    }
}
