namespace ConcreteAudit.Model
{
    /// <summary>
    /// Decorate your entities with this and AuditContext witll automaticly do the rest ;)
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AuditableAttribute : Attribute
    {
        public string Schema { get; set; }
        public AuditableAttribute(string schema = null, AuditPattern pattern = AuditPattern.KeepCurrent)
        {
            Schema = schema;
        }
    }

}
