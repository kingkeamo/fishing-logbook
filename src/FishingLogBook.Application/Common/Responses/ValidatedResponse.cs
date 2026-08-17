using System.Text.Json.Serialization;
using FluentResults;
using FluentValidation.Results;

namespace FishingLogBook.Application.Common.Responses;

public abstract class ValidatedResponse
{
    public bool IsSuccess =>
        string.IsNullOrWhiteSpace(ErrorMessage) &&
        (ValidationErrors is null || ValidationErrors.Count == 0);

    public bool IsFailure => !IsSuccess;

    public string? ErrorMessage { get; set; }

    public IList<ValidationFailure>? ValidationErrors { get; set; }

    [JsonIgnore]
    public IError? Error { get; set; }

    public static TResponse FromError<TResponse>(IError error)
        where TResponse : ValidatedResponse, new()
    {
        return new TResponse
        {
            Error = error,
            ErrorMessage = error.Message
        };
    }
}
