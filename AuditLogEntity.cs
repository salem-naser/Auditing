namespace Auditing
{
    public class AuditLogEntity
    {
        public string Id { get; private set; }
        public string EntityName { get; set; }
        public string Operation { get; set; }
        public DateTime Timestamp { get; set; }
        public Dictionary<string, ChangeValues> Changes { get; set; } = new();
        public List<KeyEntry> SpecialKeys { get; set; } = [];
        public List<KeyEntry> ForeignKeys { get; set; } = [];
        public long UserId { get; set; }
        public string UserName { get; set; }
        public string PrimaryKey { get; set; }

        public void GenerateId()
        {
            Id = $"{EntityName}-{UserId}-{Timestamp:yyyyMMddHHmmssfff}-{Guid.NewGuid()}";
        }
    }

    public class ChangeValues
    {
        public string Status { get; set; }
        public string Original { get; set; }
        public string Current { get; set; }
    }
    public class KeyEntry
    {
        public string Name { get; set; }
        public string Value { get; set; }
    }
}
