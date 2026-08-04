namespace SafeDeal.Domain.Exceptions;

public class InvalidTransitionException : DomainException
{
    public InvalidTransitionException(string from, string to)
        : base($"Invalid transition from '{from}' to '{to}'.") { }
}