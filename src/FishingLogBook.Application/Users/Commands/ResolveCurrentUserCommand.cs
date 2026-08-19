using FishingLogBook.Application.Args;
using FishingLogBook.Application.Common.Responses;
using FishingLogBook.Application.Contracts.Services;
using FluentValidation;
using MapsterMapper;
using MediatR;

namespace FishingLogBook.Application.Users.Commands;

public sealed class ResolveCurrentUserCommand : IRequest<ResolveCurrentUserResponse>
{
    public string Provider { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;
}

public sealed class ResolveCurrentUserResponse : ValidatedResponse
{
    public Guid UserId { get; init; }
}

public sealed class ResolveCurrentUserHandler : IRequestHandler<ResolveCurrentUserCommand, ResolveCurrentUserResponse>
{
    private readonly IUserIdentityService _userIdentityService;
    private readonly IMapper _mapper;

    public ResolveCurrentUserHandler(IUserIdentityService userIdentityService, IMapper mapper)
    {
        _userIdentityService = userIdentityService;
        _mapper = mapper;
    }

    public async Task<ResolveCurrentUserResponse> Handle(
        ResolveCurrentUserCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _userIdentityService.ResolveAsync(
            _mapper.Map<ResolveUserIdentityArgs>(command),
            cancellationToken);
        if (result.IsFailed)
        {
            return new ResolveCurrentUserResponse
            {
                ErrorMessage = result.Errors[0].Message
            };
        }

        if (result.Value == Guid.Empty)
        {
            return new ResolveCurrentUserResponse
            {
                ErrorMessage = "FishingLogBook UserId cannot be empty."
            };
        }

        return new ResolveCurrentUserResponse
        {
            UserId = result.Value
        };
    }
}

public sealed class ResolveCurrentUserCommandValidator : AbstractValidator<ResolveCurrentUserCommand>
{
    public ResolveCurrentUserCommandValidator()
    {
        RuleFor(command => command.Provider)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("External identity is missing.");
        RuleFor(command => command.Subject)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("External identity is missing.");
        RuleFor(command => command.Email)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithMessage("Authenticated email is missing.");
    }
}
