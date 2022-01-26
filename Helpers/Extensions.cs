
using ConcreteAudit.AuditContext;
using ConcreteAudit.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using System.ComponentModel;
using System.Linq.Expressions;

namespace ConcreteAudit.Helpers
{
    public static class Extensions
    {
        public static IServiceCollection AddAuditDbContext<T>(this IServiceCollection services, Action<DbContextOptionsBuilder>? optionsAction = null, AuditDbContextOption auditOptionsAction = null, ServiceLifetime contextLifetime = ServiceLifetime.Scoped, ServiceLifetime optionsLifetime = ServiceLifetime.Scoped) where T : DbContext
        {
            services.AddSingleton(auditOptionsAction ?? new AuditDbContextOption());
            return services.AddDbContext<T>(optionsAction, contextLifetime, optionsLifetime);
        }
        public static IEnumerable<Audit<T>> Audit<T>(this DbSet<T> _this, Expression<Func<Audit<T>, bool>> predicate) where T:class, new()
        {
            var t = _this.GetService<ICurrentDbContext>().Context as AuditDbContext;
            return t.Audit<T>(predicate);
        }
        public static Dictionary<string, object> ToDictionary(this object source)
        {
            if (source == null)
                ThrowExceptionWhenSourceArgumentIsNull();

            var dictionary = new Dictionary<string, object>();
            foreach (PropertyDescriptor property in TypeDescriptor.GetProperties(source))
                AddPropertyToDictionary<object>(property, source, dictionary);
            return dictionary;
        }

        private static void AddPropertyToDictionary<T>(PropertyDescriptor property, object source, Dictionary<string, object> dictionary)
        {
            object value = property.GetValue(source);
            dictionary.Add(property.Name, value);
        }


        private static void ThrowExceptionWhenSourceArgumentIsNull()
        {
            throw new ArgumentNullException("source", "Unable to convert object to a dictionary. The source object is null.");
        }
    }

}
