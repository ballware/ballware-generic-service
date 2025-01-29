using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Ballware.Meta.Client;

namespace Ballware.Generic.Scripting
{
    public interface IGenericEntityScriptingExecutor
    {
        public IEnumerable<T> ListScript<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, IEnumerable<T> results) where T : class;    
        public Task<T> ByIdScriptAsync<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, string identifier, Dictionary<string, object> claims, T item) where T : class;
        public Task BeforeSaveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, bool insert, object item);
        public Task SaveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, bool insert, object item);
        public Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, object p);
        public Task RemoveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, object p);
    }
}

