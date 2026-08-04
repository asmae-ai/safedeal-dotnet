namespace SafeDeal.Application.Common.Exceptions;

public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Unauthenticated.")
        : base(message) { }
}