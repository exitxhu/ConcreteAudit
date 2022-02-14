using System.ComponentModel.DataAnnotations;

namespace ConcreteAudit.Model
{
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

}
