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
        public T Data { get
            {
                return AuditType switch
                {
                    AuditType.Insert => CurrentData,
                    AuditType.Update => CurrentData,
                    AuditType.Delete => OldData,
                };
            }
        }
        public T CurrentData { get; set; }
        public T OldData { get; set; }
        public long AuditId { get; set; }
        public DateTime AuditCreateDate { get; set; }
        public string AuditCreatorUserId { get; set; }
        public AuditType AuditType { get; set; }
    }

}
