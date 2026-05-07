
namespace AntiFraud.Application.Presentation.Rest.Exceptions;

public sealed class RestPresentationException : Exception
{
    public RestPresentationException(string message) : base(message)
    {
    }
}