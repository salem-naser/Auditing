# Audit Logging Library for C# with MassTransit
This repository provides a comprehensive solution for implementing audit logging in any C# application. It leverages Entity Framework Core's ChangeTracker to capture changes to entities and publishes audit logs using MassTransit.

Features
Entity Auditing: Tracks changes to added, modified, and deleted entities.
Nested Entity Support: Captures changes in related entities, including collections and references.
Customizable Keys: Supports user-defined keys for special auditing requirements.
MassTransit Integration: Publishes audit logs to a message broker for centralized processing.
Error Handling: Logs errors encountered during the audit logging process. 
Code Overview

AuditLoggingService
The AuditLoggingService class implements the IAuditLoggingService interface to log changes in entities:
Key Methods:
LogAuditEntity: Processes entity changes and publishes audit logs.
PublishAuditLogForEntity: Formats and sends audit log messages via MassTransit.
GetChangedProperties: Captures changes to entity properties.
GetNestedChanges: Recursively tracks changes in related entities.

AuditLoggingExtensions
The AuditLoggingExtensions class provides a convenient extension method to integrate audit logging into your application:
public static async Task LogAuditableEntitiesAsync(
    this ChangeTracker changeTracker,
    IAuditLoggingService auditLoggingService,
    string userName,
    long userId,
    HashSet<string>? userDefinedKeys = null)



Filters auditable entities from the ChangeTracker.
Calls the LogAuditEntity method to handle the auditing process.

IAuditableEntity Interface
Entities to be audited should implement the IAuditableEntity interface to be identified by the system.

Example Usage
1. Configure Services

Add the AuditLoggingService to your dependency injection container:
services.AddScoped<IAuditLoggingService, AuditLoggingService>();
services.AddMassTransit(config =>
{
    // Configure MassTransit here
});

2. Implement Auditable Entities

Ensure your entities implement the IAuditableEntity interface:
public class Patient : IAuditableEntity
{
    public int Id { get; set; }
    public string Name { get; set; }
    // Other properties
}
2. Implement Auditable Entities

Ensure your entities implement the IAuditableEntity interface:
3. Use the Extension Method
Invoke the extension method in your DbContext's SaveChangesAsync method:
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    await ChangeTracker.LogAuditableEntitiesAsync(
        _auditLoggingService,
        userName: "current_user",
        userId: 12345);

    return await base.SaveChangesAsync(cancellationToken);
}
Benefits

Centralized Logging: All audit logs are processed through a single service, ensuring consistency.

Scalability: MassTransit integration allows audit logs to be consumed by multiple services.

Maintainability: Easily extendable to handle additional auditing requirements.

How to Run

Clone the repository:

git clone https://github.com/yourusername/audit-logging.git

Configure your application to use MassTransit and Entity Framework Core.

Implement the IAuditableEntity interface in the entities you want to audit.

Call LogAuditableEntitiesAsync in your DbContext.
