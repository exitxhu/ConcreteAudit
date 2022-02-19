using ConcreteAudit.Helpers;
using ConcreteAudit.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Reflection;

namespace ConcreteAudit.AuditContext
{
    public partial class AuditDbContext : DbContext
    {
        internal AuditAuditDbContextCache _cache;
        public AuditDbContext(DbContextOptions o, AuditDbContextOption op, IHttpContextAccessor httpContextAccessor) : base(o)
        {
            _cache = AuditDbContextCacheManager.GetInstance(this, op);
            var auditUserId = httpContextAccessor.HttpContext?.User?.Identity?.Name;
            if (_cache.IsFirstInstanciation)
            {
                _cache.AuditsDefinition = ScavangeAuditTables();
                _cache.AuditDataExtractor = (aud) =>
                {
                    var audToAdd = new List<(string audTableName, Dictionary<string, object> audData)>();
                    foreach (var a in aud)
                    {
                        Dictionary<string, object> temp = new Dictionary<string, object>();
                        switch (a.AuditType)
                        {
                            case AuditType.None:
                                break;
                            case AuditType.Insert:
                                temp = a.RawChange.ToDictionary();
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.AuditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = auditUserId ?? "NotSet";
                                audToAdd.Add((a.AuditTable.Name, temp));
                                break;
                            case AuditType.Update:
                                temp = a.RawChange.ToDictionary();
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.AuditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = auditUserId ?? "NotSet";
                                switch (a.AuditTable.Pattern)
                                {
                                    case AuditPattern.KeepCurrent:
                                        audToAdd.Add((a.AuditTable.Name, temp));
                                        break;
                                    case AuditPattern.KeepCurrentAndOld:
                                        audToAdd.Add((a.AuditTable.Name, temp.Concat(a.AuditOldData).ToDictionary(x => x.Key, x => x.Value)));
                                        break;
                                }

                                break;
                            case AuditType.Delete:
                                temp = a.AuditTable.Pattern switch
                                {
                                    AuditPattern.KeepCurrentAndOld => a.AuditOldData,
                                    AuditPattern.KeepCurrent => a.RawChange.ToDictionary()
                                };
                                temp[nameof(IAuditable.AuditCreateDate)] = DateTime.UtcNow;
                                temp[nameof(IAuditable.AuditType)] = a.AuditType;
                                temp[nameof(IAuditable.AuditCreatorUserId)] = auditUserId ?? "NotSet";
                                audToAdd.Add((a.AuditTable.Name, temp));
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
            var audChanges = new List<ChangesToAudit>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = aud.Pattern switch
                {
                    AuditPattern.KeepCurrentAndOld => change.State == EntityState.Modified
                ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                : change.State == EntityState.Deleted
                    ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : null,
                    AuditPattern.KeepCurrent => null,
                };
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add(new ChangesToAudit(change.Entity, aud, prev, state));
            }


            var res = base.SaveChanges(acceptAllChangesOnSuccess);
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = base.SaveChanges(acceptAllChangesOnSuccess);
            return res;
        }
        public override int SaveChanges()
        {
            return base.SaveChanges();
        }
        public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            var changes = this.ChangeTracker.Entries().ToList();
            var audChanges = new List<ChangesToAudit>();

            foreach (var change in changes)
            {
                if (change.State == EntityState.Unchanged || change.State == EntityState.Detached)
                    continue;
                if (!_cache.AuditsDefinition.TryGetValue(change.Metadata.ClrType.Name, out var aud))
                    continue;
                var prev = aud.Pattern switch
                {
                    AuditPattern.KeepCurrentAndOld => change.State == EntityState.Modified
                ? change.Properties.Select(n => new KeyValuePair<string, object>(n.Metadata.Name, n.OriginalValue)).ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                : change.State == EntityState.Deleted
                    ? change.Entity.ToDictionary().ToDictionary(x => _cache.AuditOldColumnNamer(x.Key), x => x.Value)
                    : null,
                    AuditPattern.KeepCurrent => null,
                };
                var state = change.State switch
                {
                    EntityState.Modified => AuditType.Update,
                    EntityState.Added => AuditType.Insert,
                    EntityState.Deleted => AuditType.Delete,
                };
                audChanges.Add(new ChangesToAudit(change.Entity, aud, prev, state));
            }


            var res = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            foreach (var m in _cache.AuditDataExtractor(audChanges))
                this.Set<Dictionary<string, object>>(m.audTableName).Add(m.audData);

            res = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
            return res;
        }
        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        /// <summary>
        /// Check all entity's for Auditable attribute then create a dictionary of corresponding shadow fields and returns it.
        /// </summary>
        /// <returns></returns>
        private AuditTableRuntimeCollection ScavangeAuditTables()
        {
            _cache.AuditProperties = ((TypeInfo)typeof(IAuditable)).DeclaredProperties;
            var audrun = new AuditTableRuntimeCollection();
            foreach (var entity in this.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(n => n.PropertyType.IsGenericType && n.PropertyType.GenericTypeArguments.Any(n => n.GetCustomAttribute<AuditableAttribute>() is not null)))
            {
                var temprun = new AuditTableRuntime();
                Type entityType = entity.PropertyType.GenericTypeArguments[0];
                var props = entityType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var attr = entityType.GetCustomAttribute<AuditableAttribute>(true);
                temprun.Pattern = attr.Pattern;
                temprun.Schema = attr.Schema;
                foreach (var prop in props)
                {
                    temprun.Columns.Add(new AuditTableRuntimeProperty(prop.Name, prop));
                    if (attr.Pattern == AuditPattern.KeepCurrentAndOld)
                        temprun.Columns.Add(new AuditTableRuntimeProperty(_cache.AuditOldColumnNamer(prop.Name), prop));

                }
                foreach (var prop in _cache.AuditProperties)
                {
                    temprun.Columns.Add(new AuditTableRuntimeProperty(prop.Name, prop));
                }
                temprun.Name = _cache.AuditTableNamer(entityType.Name);
                temprun.BaseTableName = entityType.Name;
                audrun.Add(temprun);
            }
            return audrun;
        }
        protected override void OnModelCreating(ModelBuilder mb)
        {
            foreach (var entity in _cache.AuditsDefinition)
            {
                var confer = mb.Entity(entity.Name);
                foreach (var column in entity.Columns)
                {
                    confer.Property(column.ColumnMetadata.PropertyType, column.ColumnName);
                    if (column.ColumnMetadata.GetCustomAttribute<KeyAttribute>() is object)
                        confer.HasKey(column.ColumnName);
                }
                var schema = entity.Schema;
                confer.ToTable(entity.Name, string.IsNullOrEmpty(_cache.Options.ForceSchema?.Trim())
                                                                                                        ? schema
                                                                                                        : _cache.Options.ForceSchema.Trim());
            }
        }

        /// <summary>
        /// use this method to query Audits of Entity type T
        /// </summary>
        /// <typeparam name="T">the Auditable Entity</typeparam>
        /// <param name="predicate">query expression</param>
        /// <returns>null if entity is not auditable</returns>
        public IEnumerable<Audit<T>> Audit<T>(Expression<Func<Audit<T>, bool>> predicate = null) where T : class, new()
        {
            if (!_cache.AuditsDefinition.TryGetValue(typeof(T).Name, out var AudName)) return null;


            var query = this.Set<Dictionary<string, object>>(AudName.Name).AsQueryable();
            var properExpression = expGenerator(predicate);
            var rawResult = properExpression is null
                ? query.ToList()
                : query.Where(properExpression).ToList();
            var resualt = new HashSet<Audit<T>>();
            foreach (var set in rawResult)
            {
                var temp = new Audit<T>();
                var type = temp.GetType();
                foreach (var column in AudName.Columns)
                {
                    if (!set.ContainsKey(column.ColumnName))
                        continue;
                    if (_cache.AuditProperties.Any(p => p.Name == column.ColumnName))
                        column.ColumnMetadata.SetValue(temp, set[column.ColumnName]);
                    else switch (AudName.Pattern)
                        {
                            case AuditPattern.KeepCurrent:
                                column.ColumnMetadata.SetValue(temp.CurrentData, set[column.ColumnName]);
                                break;
                            case AuditPattern.KeepCurrentAndOld:
                                if (AudName.Columns.Contains(n => n.ColumnName == _cache.AuditOldColumnNamer(column.ColumnName)))
                                {
                                    column.ColumnMetadata.SetValue(temp.CurrentData, set[column.ColumnName]);
                                }
                                else
                                    column.ColumnMetadata.SetValue(temp.OldData, set[column.ColumnName]);
                                break;

                        }

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
        static Expression<Func<Dictionary<string, object>, bool>> expGenerator<T>(Expression<Func<Audit<T>, bool>> predicate) where T : class, new()
        {
            if (predicate == null)
                return null;
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
        static Expression Traverser(Expression exp, ParameterExpression argParam)
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
    public class AuditTableRuntimeProperty
    {
        public AuditTableRuntimeProperty(string columnName, PropertyInfo columnMetadata)
        {
            ColumnName = columnName;
            ColumnMetadata = columnMetadata;
        }

        public string ColumnName { get; private set; }
        public PropertyInfo ColumnMetadata { get; private set; }
    }
    public class AuditTableRuntime
    {
        public string BaseTableName { get; set; }
        public string Name { get; set; }
        public AuditPattern Pattern { get; set; }
        public string Schema { get; set; }
        public AuditTableRuntimePropertyCollection Columns { get; set; } = new();

    }
    public class AuditTableRuntimeCollection : ICollection<AuditTableRuntime>
    {

        public HashSet<AuditTableRuntime> Tables { get; set; } = new();

        public int Count => Tables.Count;

        public bool IsReadOnly => false;

        public void Add(AuditTableRuntime item)
        {
            Tables.Add(item);
        }

        public void Clear()
        {
            Tables.Clear();
        }

        public bool Contains(AuditTableRuntime item)
        {
            return Tables.Contains(item);
        }
        public bool Contains(Func<AuditTableRuntime, bool> predicate)
        {
            return Tables.Any(predicate);
        }
        public void CopyTo(AuditTableRuntime[] array, int arrayIndex)
        {
            Tables.CopyTo(array, arrayIndex);
        }

        public IEnumerator<AuditTableRuntime> GetEnumerator()
        {
            return Tables.GetEnumerator();
        }

        public bool Remove(AuditTableRuntime item)
        {
            return Tables.Remove(item);
        }

        internal bool TryGetValue(string name, out AuditTableRuntime audName)
        {
            audName = Tables.FirstOrDefault(x => x.BaseTableName == name);
            return audName is not null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Tables.GetEnumerator();
        }
    }
    public class AuditTableRuntimePropertyCollection : ICollection<AuditTableRuntimeProperty>
    {

        public HashSet<AuditTableRuntimeProperty> Properties { get; set; } = new();

        public int Count => Properties.Count;

        public bool IsReadOnly => false;

        public void Add(AuditTableRuntimeProperty item)
        {
            Properties.Add(item);
        }

        public void Clear()
        {
            Properties.Clear();
        }

        public bool Contains(AuditTableRuntimeProperty item)
        {
            return Properties.Contains(item);
        }
        public bool Contains(Func<AuditTableRuntimeProperty, bool> predicate)
        {
            return Properties.Any(predicate);
        }
        public void CopyTo(AuditTableRuntimeProperty[] array, int arrayIndex)
        {
            Properties.CopyTo(array, arrayIndex);
        }

        public IEnumerator<AuditTableRuntimeProperty> GetEnumerator()
        {
            return Properties.GetEnumerator();
        }

        public bool Remove(AuditTableRuntimeProperty item)
        {
            return Properties.Remove(item);
        }

        public bool TryGetValue(string name, out AuditTableRuntimeProperty audName)
        {
            audName = Properties.FirstOrDefault(x => x.ColumnName == name);
            return audName is not null;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Properties.GetEnumerator();
        }
    }

    public abstract class Strategy
    {

    }
    public class KeepCurrentStrategy : Strategy
    {

    }
    public class KeepCurrentAndOldStrategy : Strategy
    {

    }
}
