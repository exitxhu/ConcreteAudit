using System.ComponentModel.DataAnnotations;

namespace ConcreteAudit.Model
{
    /// <summary>
    /// the result of audit queries will be presented with this type
    /// </summary>
    public class Audit<T> : IAuditable where T : class, new()
    {
        public Audit()
        {
            CurrentData = new();
            OldData = new();
        }
        public T CurrentData { get; set; }
        public T OldData { get; set; }
        public long AuditId { get; set; }
        public DateTime AuditCreateDate { get; set; }
        public string AuditCreatorUserId { get; set; }
        public AuditType AuditType { get; set; }
    }
    /// <summary>
    /// the generalFields of audit table
    /// </summary>
    public interface IAuditable
    {
        [Key]
        public long AuditId { get; set; }
        public DateTime AuditCreateDate { get; set; }
        public string AuditCreatorUserId { get; set; }
        public AuditType AuditType { get; set; }

    }
    /// <summary>
    /// Decorate your entities with this and AuditContext witll automaticly do the rest ;)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AuditableAttribute : Attribute
    {
        public string Schema { get; set; }
        public AuditableAttribute(string schema = null)
        {
            Schema = schema;
        }
    }
    public enum AuditType
    {
        None = 0,
        Insert = 1,
        Update = 2,
        Delete = 3,
    }


}
