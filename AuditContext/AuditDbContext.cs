using ConcreteAudit.Helpers;
using ConcreteAudit.Model;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

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
        protected internal Func<List<(string auditTableName, Dictionary<string, object> auditBaseData, Dictionary<string, object> previousData, AuditType auditType)>, List<(string audTableName, Dictionary<string, object> audData)>> AuditDataExtractor { get; set; }
        protected internal AuditDbContextOption Options { get; private set; }
        protected internal Func<string, string> AuditTableNamer { get; private set; }
        protected internal Func<string, string> AuditOldColumnNamer { get; private set; }
        protected internal Dictionary<string, (string auditTableName, Dictionary<string, PropertyInfo> propNameAndTypes)> AuditsDefinition { get; set; }
        /// <summary>
        /// True after constrruction, and only set to False(even if = true is specefied, it will always set to false)
        /// </summary>
        protected internal bool IsFirstInstanciation { get => isFirstInstanciation; set => isFirstInstanciation = false; }
    }
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
        public static AuditAuditDbContextCache GetInstance(AuditDbContext context, AuditDbContextOption cacheOptions, Func<string, string> auditTableNamer = null, Func<string, string> auditOldColumnNamer = null)
        {
            if (cache.TryGetValue(context.GetType().FullName, out var result))
                return result;
            cache[context.GetType().FullName] = new AuditAuditDbContextCache(cacheOptions);
            return cache[context.GetType().FullName];
        }
    }
    public class AuditDbContext : DbContext
    {
        internal AuditAuditDbContextCache _cache;
        public AuditDbContext(DbContextOptions<AuditDbContext> o, AuditDbContextOption op) : base(o)
        {
            _cache = AuditDbContextCacheManager.GetInstance(this, op);
            if (_cache.IsFirstInstanciation)
            {
                _cache.AuditsDefinition = ScavangeAuditTables();
                _cache.AuditDataExtractor = (aud) =>
                {
                    var audToAdd = new List<(string audTableName, Dictionary<string, object> audData)>();
                    foreach (var a in aud)
                    {
                        Dictionary<string, object> temp = new Dictionary<string, object>();
                        switch (a.auditType)
                        {
                            case AuditType.None:
                                break;
                            case AuditType.Insert:
                                temp = a.auditBaseData;
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.auditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = "Admin";
                                audToAdd.Add((a.auditTableName, temp));
                                break;
                            case AuditType.Update:
                                temp = a.auditBaseData;
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.auditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = "Admin";
                                audToAdd.Add((a.auditTableName, temp.Concat(a.previousData).ToDictionary(x => x.Key, x => x.Value)));
                                break;
                            case AuditType.Delete:
                                temp = a.previousData;
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.auditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = "Admin";
                                audToAdd.Add((a.auditTableName, temp));
                                break;
                            default:
                                break;
                        }

                    }
                    return audToAdd;
                };
                _cache.IsFirstInstanciation = false;
            }
        }
        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            var changes = this.ChangeTracker.Entries().ToList();
            var audChanges = new List<(string auditTableName, Dictionary<string, object> auditData, Dictionary<string, object> previousData, AuditType auditType)>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = change.State == EntityState.Modified
                    ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : change.State == EntityState.Deleted
                        ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                        : null;
                var curr = change.Entity.ToDictionary();
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add((aud.auditTableName, curr, prev, state));
            }


            var res = base.SaveChanges(acceptAllChangesOnSuccess);
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = base.SaveChanges(acceptAllChangesOnSuccess);
            return res;
        }
        public override int SaveChanges()
        {
            var changes = this.ChangeTracker.Entries().ToList();
            var audChanges = new List<(string auditTableName, Dictionary<string, object> auditData, Dictionary<string, object> previousData, AuditType auditType)>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = change.State == EntityState.Modified
                    ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : change.State == EntityState.Deleted
                        ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                        : null;
                var curr = change.Entity.ToDictionary();
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add((aud.auditTableName, curr, prev, state));
            }


            var res = base.SaveChanges();
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = base.SaveChanges();
            return res;
        }
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            var changes = this.ChangeTracker.Entries().ToList();
            var audChanges = new List<(string auditTableName, Dictionary<string, object> auditData, Dictionary<string, object> previousData, AuditType auditType)>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = change.State == EntityState.Modified
                    ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : change.State == EntityState.Deleted
                        ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                        : null;
                var curr = change.Entity.ToDictionary();
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add((aud.auditTableName, curr, prev, state));
            }


            var res = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            return res;
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var changes = this.ChangeTracker.Entries().ToList();
            var audChanges = new List<(string auditTableName, Dictionary<string, object> auditData, Dictionary<string, object> previousData, AuditType auditType)>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = change.State == EntityState.Modified
                    ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : change.State == EntityState.Deleted
                        ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                        : null;
                var curr = change.Entity.ToDictionary();
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add((aud.auditTableName, curr, prev, state));
            }


            var res = await base.SaveChangesAsync(cancellationToken);
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = await base.SaveChangesAsync(cancellationToken);
            return res;
        }
        /// <summary>
        /// Check all entity's for Auditable attribute then create a dictionary of corresponding shadow fields and returns it.
        /// </summary>
        /// <returns></returns>
        private Dictionary<string, (string auditTableName, Dictionary<string, PropertyInfo> propNameAndTypes)> ScavangeAuditTables()
        {
            _cache.AuditProperties = ((TypeInfo)typeof(IAuditable)).DeclaredProperties;
            var audits =
                new Dictionary<string, (string auditTableName, Dictionary<string, PropertyInfo> propNameAndTypes)>();
            foreach (var entity in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(n => n.PropertyType.IsGenericType && n.PropertyType.GenericTypeArguments[0].GetCustomAttribute<AuditableAttribute>() is not null))
            {
                var tempProps = new Dictionary<string, PropertyInfo>();
                Type entityType = entity.PropertyType.GenericTypeArguments[0];
                var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                foreach (var prop in props)
                {
                    tempProps[prop.Name] = prop;
                    tempProps[_cache.AuditOldColumnNamer(prop.Name)] = prop;
                }
                foreach (var prop in _cache.AuditProperties)
                {
                    tempProps[prop.Name] = prop;
                }
                audits[entityType.Name] = (_cache.AuditTableNamer(entityType.Name), tempProps);
            }
            return audits;
        }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entity in _cache.AuditsDefinition)
            {
                var confer = mb.Entity(entity.Value.auditTableName);
                foreach (var prop in entity.Value.propNameAndTypes)
                {
                    confer.Property(prop.Value.PropertyType, prop.Key);
                    if (prop.Value.GetCustomAttribute<KeyAttribute>() is object)
                        confer.HasKey(prop.Key);
                }
                confer.ToTable(entity.Value.auditTableName, string.IsNullOrEmpty(_cache.Options.ForceSchema?.Trim()) ? null : _cache.Options.ForceSchema.Trim());
            }
        }
   
        /// <summary>
        /// use this method to query Audits of Entity type T
        /// </summary>
        /// <typeparam name="T">the Auditable Entity</typeparam>
        /// <param name="predicate">query expression</param>
        /// <returns>null if entity is not auditable</returns>
        public IEnumerable<Audit<T>> Audit<T>(Expression<Func<Audit<T>, bool>> predicate) where T : class, new()
        {
            if (!_cache.AuditsDefinition.TryGetValue(typeof(T).Name, out var AudName)) return null;


            var query = this.Set<Dictionary<string, object>>(AudName.auditTableName).AsQueryable();
            var properExpression = expGenerator(predicate);
            var rawResult = query.Where(properExpression).ToList();
            var resualt = new HashSet<Audit<T>>();
            foreach (var set in rawResult)
            {
                var temp = new Audit<T>();
                var type = temp.GetType();
                foreach (var prop in AudName.propNameAndTypes)
                {
                    if (_cache.AuditProperties.Any(p => p.Name == prop.Key))
                        prop.Value.SetValue(temp, set[prop.Key]);
                    else if (AudName.propNameAndTypes.ContainsKey(_cache.AuditOldColumnNamer(prop.Key)))
                    {
                        prop.Value.SetValue(temp.CurrentData, set[prop.Key]);
                    }
                    else
                        prop.Value.SetValue(temp.OldData, set[prop.Key]);

                }
                resualt.Add(temp);
            }


            return resualt;
        }
        /// <summary>
        /// convert Audit<T> query expression tree to Dictionary<string,object> query expression
        /// </summary>
        /// <typeparam name="T">The audited entity</typeparam>
        /// <param name="predicate">expression to be converted</param>
        /// <returns></returns>
        Expression<Func<Dictionary<string, object>, bool>> expGenerator<T>(Expression<Func<Audit<T>, bool>> predicate) where T : class, new()
        {
            ParameterExpression argParam = Expression.Parameter(typeof(Dictionary<string, object>));
            var resXp = Traverser(predicate.Body, argParam);
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(resXp, argParam);
            return lambda;
        }
        /// <summary>
        /// travers the expression tree and rebuild it
        /// </summary>
        /// <param name="exp">the original expression</param>
        /// <param name="argParam">argument parameter expression, which servs as entry point of Lambda expression</param>
        /// <returns></returns>
        /// <exception cref="NullReferenceException"></exception>
        /// <exception cref="NotImplementedException"></exception>
        Expression Traverser(Expression exp, ParameterExpression argParam)
        {

            if (exp == null)
                throw new NullReferenceException();
            if (exp.NodeType == ExpressionType.MemberAccess)
            {
                var body = (exp as MemberExpression);
                var name = "";
                var fname = body.Member.Name;
                if (body.Expression.NodeType == ExpressionType.MemberAccess)
                {
                    var member = body.Expression as MemberExpression;
                    name = member.Member.Name; // ex currentData
                }
                var key = Expression.Constant(fname);
                var ttt = typeof(Dictionary<string, object>);
                var tme = ttt.GetMethods().FirstOrDefault(n => n.Name == "get_Item");
                Expression memAccXp = Expression.Property(argParam, "Item", key);
                Expression memCall = Expression.Call(argParam, tme, key);
                if (body.Type == typeof(bool))
                {


                    var node = Expression.Equal(memCall, Expression.Convert(Expression.Constant(true), typeof(object)));
                    return node;
                }

                return memCall;

            }
            else if (exp.NodeType == ExpressionType.Constant)
            {
                return exp;
            }
            else if (exp.NodeType == ExpressionType.Convert)
            {
                var xp = exp as UnaryExpression;
                var left = Traverser(xp.Operand, argParam);
                //var type = Traverser(xp.Right, argParam);
                Expression res = Expression.Convert(left, xp.Type);

                return res;
            }
            else if (exp.NodeType == ExpressionType.Call)
            {
                return exp;
            }
            else if (exp as BinaryExpression is not null)
            {
                var xp = exp as BinaryExpression;
                var left = Traverser(xp.Left, argParam);
                var right = Traverser(xp.Right, argParam);
                if (left.Type != right.Type)
                {
                    left = Expression.Convert(left, right.Type);
                }
                Expression res = exp.NodeType switch
                {

                    ExpressionType.Equal => Expression.Equal(left, right),
                    ExpressionType.NotEqual => Expression.NotEqual(left, right),
                    ExpressionType.GreaterThan => Expression.GreaterThan(left, right),
                    ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
                    ExpressionType.LessThan => Expression.LessThan(left, right),
                    ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
                    ExpressionType.And => Expression.And(left, right),
                    ExpressionType.AndAlso => Expression.AndAlso(left, right),
                    ExpressionType.Or => Expression.Or(left, right),
                    ExpressionType.OrElse => Expression.OrElse(left, right),
                    ExpressionType.Add => Expression.Add(left, right),
                    ExpressionType.AddAssign => Expression.AddAssign(left, right),
                    ExpressionType.Subtract => Expression.SubtractAssign(left, right),
                    ExpressionType.SubtractAssign => Expression.SubtractAssign(left, right),
                    ExpressionType.Multiply => Expression.Multiply(left, right),
                    ExpressionType.MultiplyAssign => Expression.MultiplyAssign(left, right),
                    ExpressionType.Divide => Expression.Divide(left, right),
                    ExpressionType.DivideAssign => Expression.DivideAssign(left, right),
                    ExpressionType.Modulo => Expression.Modulo(left, right),
                    ExpressionType.ModuloAssign => Expression.ModuloAssign(left, right),

                    _ => throw new NotImplementedException("the expression of type binary is not suppported by Audit predicate")

                };
                Expression.Equal(left, right);

                return res;
            }
            return null;
        }
    }
    public class AuditDbContextOption
    {
        public string AuditTableNameTemplateString { get; init; } = "{0}_Audit";
        public string AuditOldColumnNameTemplateString { get; init; } = "{0}_Old";
        public long PersistIntervalSec { get; init; } = 600;
        public bool SyncronousPersistance { get; init; } = false;
        public int InmemoryAuditTreshhold { get; init; } = 100;
        public string? ForceSchema { get; init; } = null;
    }


}
