using ConcreteAudit.Model;

namespace ConcreteAudit.AuditContext
{
    public partial class AuditDbContext
    {
        internal class ChangesToAudit
        {
            public ChangesToAudit(object rawChange, AuditTableRuntime auditTable, Dictionary<string, object> auditOldData, AuditType auditType)
            {
                RawChange = rawChange;
                AuditTable = auditTable;
                AuditOldData = auditOldData;
                AuditType = auditType;
            }
            public ChangesToAudit()
            {

            }
            public object RawChange { get; set; }
            public AuditTableRuntime AuditTable { get; set; }
            public Dictionary<string, object> AuditOldData { get; set; }
            public AuditType AuditType { get; set; }
        }
    }
}
