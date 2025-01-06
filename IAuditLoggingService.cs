using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Auditing
{
    public interface IAuditLoggingService
    {
        Task LogAuditEntity(IEnumerable<EntityEntry> auditLogs, string userName, long userId, HashSet<string> userDefinedKeys = null);
    }
}
