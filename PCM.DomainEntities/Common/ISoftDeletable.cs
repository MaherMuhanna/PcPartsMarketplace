namespace PCM.DomainEntities.Common;

/// <summary>
/// Entity that is never physically deleted. Removing it sets IsDeleted = true
/// (done by <see cref="Interceptors.AuditSaveChangesInterceptor"/>), and a global
/// query filter hides deleted rows from every query.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
}
