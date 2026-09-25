namespace Blocks.Domain;

public interface IAuditableAction
{
    public DateTime CreatedOn => DateTime.UtcNow;
    public int CreatedById { get; set; }
}