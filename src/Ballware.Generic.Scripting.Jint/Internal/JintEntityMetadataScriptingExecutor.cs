using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Ballware.Generic.Metadata;
using Jint;

namespace Ballware.Generic.Scripting.Jint.Internal;

public class JintEntityMetadataScriptingExecutor : IGenericEntityScriptingExecutor
{
    private ITenantDataAdapter TenantDataAdapter { get; }
    private IMetadataAdapter MetadataAdapter { get; }

    public JintEntityMetadataScriptingExecutor(ITenantDataAdapter tenantDataAdapter, IMetadataAdapter metadataAdapter)
    {
        TenantDataAdapter = tenantDataAdapter;
        MetadataAdapter = metadataAdapter;
    }

    public virtual IEnumerable<T> ListScript<T>(IDbConnection db, IDbTransaction? transaction,
        Tenant tenant, Entity entity, string identifier,
        IDictionary<string, object> claims, IEnumerable<T> results) where T : class
    {
        if (!string.IsNullOrEmpty(entity.ListScript))
        {
            return results.Select(item =>
            {
                new Engine()
                    .SetValue("identifier", identifier)
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                    .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                    .SetValue("item", item)
                    .SetValue("addProperty",
                        new Action<string, object>((prop, value) =>
                        {
                            (item as IDictionary<string, object>)?.Add(prop, value);
                        }))
                    .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                              entity.ListScript);

                return item;
            });
        }

        return results;
    }

    public virtual async Task<T> ByIdScriptAsync<T>(IDbConnection db, IDbTransaction? transaction, Tenant tenant,
        Entity entity,
        string identifier, IDictionary<string, object> claims, T item) where T : class
    {
        if (!string.IsNullOrEmpty(entity.ByIdScript))
        {
            new Engine()
                .SetValue("item", item)
                .SetValue("identifier", identifier)
                .SetValue("addProperty",
                    new Action<string, object>((prop, value) =>
                    {
                        (item as IDictionary<string, object>)?.Add(prop, value);
                    }))
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" + entity.ByIdScript);
        }

        return await Task.FromResult(item);
    }

    public virtual async Task BeforeSaveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant,
        Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, bool insert, object item)
    {
        if (!string.IsNullOrEmpty(entity.BeforeSaveScript))
        {
            try
            {
                var engine = new Engine();

                engine
                    .SetValue("log", new Action<object>((obj) => { Debug.WriteLine($"{obj}"); }))
                    .SetValue("tenantId", tenant.Id)
                    .SetValue("identifier", identifier)
                    .SetValue("insert", insert)
                    .SetValue("item", JsonSerializer.Serialize(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                    .SetWritingEntityFunctions(tenant, userId, db, transaction, MetadataAdapter, TenantDataAdapter,
                        claims)
                    .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                    .SetValue("getProcessingStateName",
                        new Func<int, string?>((state) =>
                            MetadataAdapter
                                .SingleProcessingStateForTenantAndEntityByValue(tenant.Id, entity.Identifier, state)
                                ?.Name))
                    .SetValue("triggerNotification", new Action<string, string>(
                        (notificationIdentifier, notificationParams) =>
                        {
                            var notification =
                                MetadataAdapter.MetadataForNotificationByTenantAndIdentifier(tenant.Id,
                                    notificationIdentifier);

                            if (notification == null || !userId.HasValue)
                            {
                                throw new Exception($"No notification with identifier {notificationIdentifier}");
                            }

                            MetadataAdapter.CreateNotificationTriggerForTenantBehalfOfUser(tenant.Id, userId.Value, new NotificationTriggerCreatePayload()
                            {
                                NotificationId = notification.Id
                            });
                        }))
                    .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                              "item=JSON.parse(item);" +
                              entity.BeforeSaveScript);
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                throw new Exception("Error executing save script", ex);
            }
        }

        await Task.CompletedTask;
    }

    public virtual async Task SaveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant,
        Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, bool insert, object item)
    {

        if (!string.IsNullOrEmpty(entity.SaveScript))
        {
            try
            {
                var engine = new Engine();

                engine
                    .SetValue("log", new Action<object>((obj) => { Debug.WriteLine($"{obj}"); }))
                    .SetValue("tenantId", tenant.Id)
                    .SetValue("identifier", identifier)
                    .SetValue("insert", insert)
                    .SetValue("item", JsonSerializer.Serialize(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                    .SetWritingEntityFunctions(tenant, userId, db, transaction, MetadataAdapter, TenantDataAdapter,
                        claims)
                    .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                    .SetWritingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                    .SetValue("getProcessingStateName",
                        new Func<int, string?>((state) =>
                            MetadataAdapter
                                .SingleProcessingStateForTenantAndEntityByValue(tenant.Id, entity.Identifier, state)
                                ?.Name))
                    .SetValue("triggerNotification", new Action<string, string>(
                        (notificationIdentifier, notificationParams) =>
                        {
                            var notification =
                                MetadataAdapter.MetadataForNotificationByTenantAndIdentifier(tenant.Id,
                                    notificationIdentifier);

                            if (notification == null || !userId.HasValue)
                            {
                                throw new Exception($"No notification with identifier {notificationIdentifier}");
                            }

                            MetadataAdapter.CreateNotificationTriggerForTenantBehalfOfUser(tenant.Id, userId.Value, new NotificationTriggerCreatePayload()
                            {
                                NotificationId = notification.Id
                            });
                        }))
                    .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" + "item=JSON.parse(item);" + "\n" +
                              entity.SaveScript);
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                throw new Exception("Error executing save script", ex);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IDbConnection db,
        IDbTransaction transaction, Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims,
        object p)
    {
        var resultMessages = new List<string>();

        if (!string.IsNullOrEmpty(entity.RemovePreliminaryCheckScript))
        {
            var result = bool.Parse(new Engine()
                .SetValue("params", p)
                .SetValue("tenantId", tenant.Id)
                .SetValue("addResultMessage", new Action<string>((msg) => { resultMessages.Add(msg); }))
                .SetValue("querySingle",
                    new Func<string, dynamic, dynamic>((identifier, p) =>
                        TenantDataAdapter.QuerySingle(db, transaction, tenant, entity, claims, identifier, p)))
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                .SetWritingEntityFunctions(tenant, userId, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                          "function internalPreliminaryRemoveScript() { " + entity.RemovePreliminaryCheckScript +
                          " } \n"
                          + "internalPreliminaryRemoveScript();"
                )
                .ToString());

            return await Task.FromResult((result, resultMessages));
        }
        else
        {
            return await Task.FromResult((true, resultMessages));
        }
    }

    public async Task RemoveScriptAsync(IDbConnection db, IDbTransaction transaction, Tenant tenant, Entity entity,
        Guid? userId, IDictionary<string, object> claims, object p)
    {
        if (!string.IsNullOrEmpty(entity.RemoveScript))
        {
            new Engine()
                .SetValue("params", p)
                .SetValue("tenantId", tenant.Id)
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                .SetWritingEntityFunctions(tenant, userId, db, transaction, MetadataAdapter, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, transaction, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" + entity.RemoveScript);
        }

        await Task.CompletedTask;
    }

    public async Task<bool> StateAllowedScriptAsync(IDbConnection db, IDbTransaction? transaction, Tenant tenant,
        Entity entity, Guid id, int currentState, IDictionary<string, object> claims, IEnumerable<string> rights)
    {
        if (!string.IsNullOrEmpty(entity.StateAllowedScript))
        {
            var result = bool.Parse(new Engine()
                .SetValue("state", currentState)
                .SetValue("hasRight", new Func<string, bool>((right) => { return rights?.Contains(right.ToLowerInvariant()) ?? false; }))
                .SetValue("hasAnyRight", new Func<string, bool>((right) => { return rights?.Any(r => r.StartsWith(right.ToLowerInvariant())) ?? false; }))
                .SetValue("getValue", new Func<string, object>((column) => TenantDataAdapter.QueryScalarValue(db, transaction, tenant, entity, claims, column, new Dictionary<string, object>() { { "tenantId", tenant.Id }, { "id", id } })))
                .Evaluate(entity.StateAllowedScript)
                .ToString());
            
            return await Task.FromResult(result);
        }
        
        return await Task.FromResult(false);
    }

}
