using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
namespace Auditing
{
    public static class AuditLoggingExtensions
    {
        public static async Task LogAuditableEntitiesAsync(
        this ChangeTracker changeTracker,
        IAuditLoggingService auditLoggingService,
        string userName,
        long userId,
        HashSet<string>? userDefinedKeys = null)
        {
            var auditableEntities = changeTracker.Entries()
                .Where(e => e.Entity is IAuditableEntity &&
                       (e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted || e.State == EntityState.Unchanged))
                .ToList();

            if (!auditableEntities.Any()) return;
            await auditLoggingService.LogAuditEntity(auditableEntities, userName, userId, userDefinedKeys ?? new HashSet<string>());
        }
    }
}
