
using AntiFraud.Application.Presentation.Rest.Exceptions;
namespace AntiFraud.Application.Presentation.Rest.DataTransferObjects;

public record RestResponse<TData>(
    TData Data,
    string Message,
    int StatusCode,
    RestPresentationException? Error = null
);

public record RestRequest<TData>(
    TData Data
);