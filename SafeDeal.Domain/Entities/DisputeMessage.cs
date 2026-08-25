using SafeDeal.Domain.Common;

namespace SafeDeal.Domain.Entities;

/// <summary>
/// Un échange versé au litige : la réclamation initiale de l'acheteur, puis chaque
/// réponse ou preuve apportée par l'une des deux parties. L'auteur et l'horodatage
/// sont portés par l'entité, ce qui permet à l'admin de reconstituer la chronologie.
/// </summary>
public class DisputeMessage : BaseEntity, IAuditableEntity
{
    public int DisputeId { get; private set; }
    public int AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public ICollection<string> Files { get; private set; } = [];

    public Dispute Dispute { get; private set; } = null!;
    public User Author { get; private set; } = null!;

    private DisputeMessage() { }

    public static DisputeMessage Create(int authorUserId, string body, IEnumerable<string>? files = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new DisputeMessage
        {
            AuthorUserId = authorUserId,
            Body = body.Trim(),
            Files = files?.ToList() ?? []
        };
    }
}
