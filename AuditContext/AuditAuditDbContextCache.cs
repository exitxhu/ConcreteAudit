using ConcreteAudit.Model;
using System.Reflection;
using static ConcreteAudit.AuditContext.AuditDbContext;

namespace ConcreteAudit.AuditContext
{
    internal class AuditAuditDbContextCache
    {
        private bool isFirstInstanciation = true;

        public AuditAuditDbContextCache(AuditDbContextOption options, Func<string, string> auditTableNamer = null, Func<string, string> auditOldColumnNamer = null)
        {
            AuditTableNamer = auditTableNamer ?? ((a) => string.Format(options.AuditTableNameTemplateString, a));
            AuditOldColumnNamer = auditOldColumnNamer ?? ((a) => string.Format(options.AuditOldColumnNameTemplateString, a));
            Options = options;
        }
        /// <summary>
        /// properties of IAuditable 
        /// </summary>
        protected internal IEnumerable<PropertyInfo> AuditProperties { get; internal set; }

        /// <summary>
        /// the logic for populating Audit entities should be implementin here
        /// </summary>
        protected internal Func<List<ChangesToAudit>, List<(string audTableName, Dictionary<string, object> audData)>> AuditDataExtractor { get; set; }
        protected internal AuditDbContextOption Options { get; private set; }
        protected internal Func<string, string> AuditTableNamer { get; private set; }
        protected internal Func<string, string> AuditOldColumnNamer { get; private set; }
        protected internal AuditTableRuntimeCollection AuditsDefinition { get; set; }
        /// <summary>
        /// True after constrruction, and only set to False(even if = true is specefied, it will always set to false)
        /// </summary>
        protected internal bool IsFirstInstanciation { get => isFirstInstanciation; set => isFirstInstanciation = false; }
    }


}
