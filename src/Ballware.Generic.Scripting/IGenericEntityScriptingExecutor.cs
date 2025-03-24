using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting
{
    public interface IGenericEntityScriptingExecutor
    {
        public IEnumerable<T> ListScript<T>(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, IEnumerable<T> results) where T : class;    
        public Task<T> ByIdScriptAsync<T>(IDbConnection db, IDbTransaction? transaction, Tenant tenant, Entity entity, string identifier, IDictionary<string, object> claims, T item) where T : class;
        public Task BeforeSaveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, bool insert, object item);
        public Task SaveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, bool insert, object item);
        public Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, object p);
        public Task RemoveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, object p);
    }
}

