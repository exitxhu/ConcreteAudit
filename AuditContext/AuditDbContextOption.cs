namespace ConcreteAudit.AuditContext
{
    public class AuditDbContextOption
    {
        public string AuditTableNameTemplateString { get; init; } = "{0}_Audit";
        public string AuditOldColumnNameTemplateString { get; init; } = "{0}_Old";
        public long PersistIntervalSec { get; init; } = 600;
        public bool SyncronousPersistance { get; init; } = false;
        public int InmemoryAuditTreshhold { get; init; } = 100;
        /// <summary>
        /// If set, will be set for all audittables, regardless of attribute configuration
        /// </summary>
        public string? ForceSchema { get; init; } = null;
    }
}
