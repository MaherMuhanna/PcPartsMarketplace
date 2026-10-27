namespace PCM.DomainEntities.Common;

/// <summary>
/// Entity with UTC creation and last-update timestamps.
/// UpdatedAt is set automatically by <see cref="Interceptors.AuditSaveChangesInterceptor"/>.
/// </summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }

    DateTime? UpdatedAt { get; set; }
}
