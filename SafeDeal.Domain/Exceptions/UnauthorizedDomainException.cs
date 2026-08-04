namespace SafeDeal.Domain.Exceptions;

public class UnauthorizedDomainException : DomainException
{
    public UnauthorizedDomainException(string message = "You are not authorized to perform this action.")
        : base(message) { }
}