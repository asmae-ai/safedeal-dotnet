namespace SafeDeal.Application.Common.Exceptions;

/// <summary>
/// Refus de connexion pour cause d'e-mail non vérifié. Distinct d'un refus
/// d'autorisation : le client doit pouvoir rediriger vers l'écran de
/// vérification, ce qu'un message générique ne permet pas.
/// </summary>
public class EmailNotVerifiedException : Exception
{
    public EmailNotVerifiedException()
        : base("Please verify your email before logging in.") { }
}
