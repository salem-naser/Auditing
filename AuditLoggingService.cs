using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.Logging;

namespace Auditing
{
    public class AuditLoggingService : IAuditLoggingService
    {
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly ILogger<AuditLoggingService> _logger;
        public AuditLoggingService(IPublishEndpoint publishEndpoint, ILogger<AuditLoggingService> logger)
        {
            _publishEndpoint = publishEndpoint;
            _logger = logger;

        }

        public async Task LogAuditEntity(IEnumerable<EntityEntry> auditLogs, string userName, long userId, HashSet<string> userDefinedKeys = null)
        {
            userDefinedKeys ??= new HashSet<string>();
            var publishTasks = auditLogs.Select(async entry =>
            {
                try
                {
                    var rootChanges = GetChangedProperties(entry);
                    var nestedChanges = GetNestedChanges(entry);
                    var allChanges = rootChanges.Concat(nestedChanges).ToDictionary(k => k.Key, v => v.Value);

                    if (allChanges.Count > 0)
                    {

                        await PublishAuditLogForEntity(entry, allChanges, userName, userId, userDefinedKeys);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while processing Auditing");
                }
            });

            await Task.WhenAll(publishTasks);
        }

        private async Task PublishAuditLogForEntity(EntityEntry entry, Dictionary<string, ChangeValues> changes, string userName, long userId, HashSet<string> userDefinedKeys)
        {
            var rootId = GetPrimaryKey(entry);
            var foreignKeys = GetForeignKeys(entry)
               .ToDictionary(k => k.Key, v => v.Value.ToString());

            var specialKeys = userDefinedKeys.Count > 0
           ? GetSpecialKeys(entry, rootId, foreignKeys, userDefinedKeys)
           : new List<KeyEntry>();

            await _publishEndpoint.Publish(new AuditLogEntity
            {
                EntityName = entry.Entity.GetType().Name.ToLower(),
                Operation = entry.State.ToString(),
                Timestamp = DateTime.UtcNow,
                Changes = changes,
                UserId = userId,
                ForeignKeys = foreignKeys.Select(fk => new KeyEntry
                {
                    Name = fk.Key,
                    Value = fk.Value
                }).ToList(),
                SpecialKeys = specialKeys,
                UserName = userName,
                PrimaryKey = rootId
            });
        }

        private static Dictionary<string, ChangeValues> GetChangedProperties(EntityEntry entry)
        {
            var changes = new Dictionary<string, ChangeValues>();
            var properties = entry.Metadata.GetProperties().Select(r => r.Name);
            if (entry.State == EntityState.Added)
            {
                foreach (var property in properties)
                {
                    var current = entry.CurrentValues[property];
                    if (current is null or "")
                        continue;
                    changes[property] = new ChangeValues
                    {
                        Status = EntityState.Added.ToString(),
                        Original = null,
                        Current = current?.ToString()
                    };
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                foreach (var property in properties)
                {
                    var original = entry.OriginalValues[property];
                    var current = entry.CurrentValues[property];

                    if (!Equals(original, current))
                    {
                        changes[property] = new ChangeValues
                        {
                            Status = EntityState.Added.ToString(),
                            Original = original?.ToString(),
                            Current = current?.ToString()
                        };
                    }
                }
            }
            else if (entry.State == EntityState.Deleted)
            {
                foreach (var property in properties)
                {
                    var original = entry.OriginalValues[property];
                    changes[property] = new ChangeValues
                    {
                        Status = EntityState.Deleted.ToString(),
                        Original = original?.ToString(),
                        Current = null
                    };
                }
            }

            return changes;
        }

        private static string GetPrimaryKey(EntityEntry entry)
        {
            var keyValues = entry.Metadata.FindPrimaryKey().Properties
                .Select(p => entry.Property(p.Name).CurrentValue)
                .ToArray();

            return keyValues.Length switch
            {
                0 => null,
                1 => keyValues[0]?.ToString(),
                _ => string.Join("-", keyValues.Select(k => k?.ToString()))
            };
        }

        private static Dictionary<string, object> GetForeignKeys(EntityEntry entry)
        {
            var foreignKeys = entry.Metadata.GetForeignKeys();

            return foreignKeys
                .SelectMany(fk => fk.Properties)
                .ToDictionary(
                    p => p.Name,
                    p => entry.Property(p.Name).CurrentValue
                );
        }

        private static Dictionary<string, ChangeValues> GetNestedChanges(EntityEntry entry)
        {
            var nestedChanges = new Dictionary<string, ChangeValues>();

            foreach (var navigation in entry.Navigations)
            {
                if (!navigation.IsLoaded) continue;

                if (navigation.CurrentValue is IEnumerable<object> childEntities)
                {
                    ProcessChildEntities(childEntities, entry, navigation, nestedChanges);
                }
                else if (navigation.CurrentValue != null)
                {
                    ProcessReferenceNavigation(entry, navigation, nestedChanges);
                }
            }

            return nestedChanges;
        }
        private static void ProcessReferenceNavigation(EntityEntry entry, NavigationEntry navigation, Dictionary<string, ChangeValues> nestedChanges)
        {
            var referenceEntry = entry.Reference(navigation.Metadata.Name).TargetEntry;
            if (referenceEntry != null && IsTrackedEntityState(referenceEntry))
            {
                AddChangesToNestedDictionary(referenceEntry, entry, navigation, nestedChanges);

                // Recursively check for deeper nested changes
                var deeperNestedChanges = GetNestedChanges(referenceEntry);
                foreach (var change in deeperNestedChanges)
                {
                    nestedChanges[change.Key] = change.Value;
                }
            }
        }
        private static bool IsTrackedEntityState(EntityEntry entry)
        {
            return entry.State == EntityState.Modified || entry.State == EntityState.Added || entry.State == EntityState.Deleted;
        }

        private static void AddChangesToNestedDictionary(EntityEntry childEntry, EntityEntry parentEntry, NavigationEntry navigation, Dictionary<string, ChangeValues> nestedChanges)
        {
            var childChanges = GetChangedProperties(childEntry);
            foreach (var change in childChanges)
            {
                var key = $"{parentEntry.Entity.GetType().Name}.{navigation.Metadata.Name}.{change.Key}";
                nestedChanges[key] = change.Value;
            }
        }
        private static void ProcessChildEntities(IEnumerable<object> childEntities, EntityEntry entry, NavigationEntry navigation, Dictionary<string, ChangeValues> nestedChanges)
        {
            foreach (var child in childEntities)
            {
                var childEntry = entry.Context.Entry(child);
                if (IsTrackedEntityState(childEntry))
                {
                    AddChangesToNestedDictionary(childEntry, entry, navigation, nestedChanges);
                }


                var deeperNestedChanges = GetNestedChanges(childEntry);
                foreach (var change in deeperNestedChanges)
                {
                    nestedChanges[change.Key] = change.Value;
                }
            }
        }

        private List<KeyEntry> GetSpecialKeys(
        EntityEntry entry,
        string rootId,
        Dictionary<string, string> foreignKeys,
        HashSet<string> SpecialKeysToLog)
        {
            var specialKeys = new List<KeyEntry>();

            var primaryKeyName = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault()?.Name;
            if (!string.IsNullOrEmpty(primaryKeyName) && SpecialKeysToLog.Contains(primaryKeyName))
            {
                specialKeys.Add(new KeyEntry { Name = primaryKeyName, Value = rootId });
            }

            foreach (var foreignKey in foreignKeys)
            {
                if (SpecialKeysToLog.Contains(foreignKey.Key))
                {
                    specialKeys.Add(new KeyEntry { Name = foreignKey.Key, Value = foreignKey.Value });
                }
            }
            var properties = entry.Properties
                .Where(p => SpecialKeysToLog.Contains(p.Metadata.Name))
                .Select(p => new KeyEntry { Name = p.Metadata.Name, Value = p.CurrentValue?.ToString() });

            specialKeys.AddRange(properties);

            return specialKeys;
        }


    }
}
