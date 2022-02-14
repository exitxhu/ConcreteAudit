using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace ConcreteAudit.AuditContext
{
    internal static class AuditDbContextCacheManager
    {
        private static ConcurrentDictionary<string, AuditAuditDbContextCache> cache;
        static AuditDbContextCacheManager()
        {
            cache = new ConcurrentDictionary<string, AuditAuditDbContextCache>();
        }
        /// <summary>
        /// build and cache an instance of AuditAuditDbContextCache with corresponding parameters(singletone)
        /// </summary>
        /// <param name="context">which context this cache blongs to, usefull specially when you have several DbContext</param>
        /// <param name="cacheOptions">options which will be provided for cache to store</param>
        /// <param name="auditTableNamer">optional, inject your logic for naming the audit tables</param>
        /// <param name="auditOldColumnNamer">optional, inject your logic for naming the columns which store old data</param>
        /// <returns>the caches instance</returns>
        public static AuditAuditDbContextCache GetInstance<T>(T context, AuditDbContextOption cacheOptions, Func<string, string> auditTableNamer = null, Func<string, string> auditOldColumnNamer = null) where T : DbContext
        {
            if (cache.TryGetValue(context.GetType().FullName, out var result))
                return result;
            cache[context.GetType().FullName] = new AuditAuditDbContextCache(cacheOptions);
            return cache[context.GetType().FullName];
        }
    }


}
