using System.Data;
using System.Diagnostics;
using Ballware.Meta.Client;
using Jint;
using Newtonsoft.Json;

namespace Ballware.Generic.Scripting.Jint.Internal;

public class JintEntityMetadataScriptingExecutor : IGenericEntityScriptingExecutor
{
    private ITenantDataAdapter TenantDataAdapter { get; }
    private BallwareMetaClient MetaClient { get; }
    
    public JintEntityMetadataScriptingExecutor(ITenantDataAdapter tenantDataAdapter, BallwareMetaClient metaClient)
    {
        TenantDataAdapter = tenantDataAdapter;
        MetaClient = metaClient;
    }

    public virtual IEnumerable<T> ListScript<T>(IDbConnection db,
        ServiceTenant tenant, ServiceEntity entity, string identifier,
        Dictionary<string, object> claims, IEnumerable<T> results) where T : class
    {
        if (!string.IsNullOrEmpty(entity.ListScript))
        {
            return results.Select(item =>
            {
                new Engine()
                    .SetValue("identifier", identifier)
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                    .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                    .SetValue("item", item)
                    .SetValue("addProperty",
                        new Action<string, object>((prop, value) =>
                        {
                            (item as IDictionary<string, object>).Add(prop, value);
                        }))
                    .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                                       entity.ListScript);

                return item;
            });
        }

        return results;
    }
    
    public virtual async Task<T> ByIdScriptAsync<T>(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, 
        string identifier, Dictionary<string, object> claims, T item) where T : class
    {
        if (!string.IsNullOrEmpty(entity.ByIdScript))
        {
            new Engine()
                .SetValue("item", item)
                .SetValue("identifier", identifier)
                .SetValue("addProperty", new Action<string, object>((prop, value) => { (item as IDictionary<string, object>).Add(prop, value); }))
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" + entity.ByIdScript);
        }

        return await Task.FromResult(item);
    }

    public virtual async Task BeforeSaveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, bool insert, object item)
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
                    .SetValue("item", JsonConvert.SerializeObject(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                    .SetWritingEntityFunctions(tenant, userId, db, MetaClient, TenantDataAdapter, claims)
                    .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                    .SetValue("getProcessingStateName",
                    new Func<int, string?>((state) => MetaClient.SingleProcessingStateForTenantAndEntityByValue(tenant.Id, entity.Entity, state)?.Name))
                    .SetValue("triggerNotification", new Action<string, string>((notificationIdentifier, notificationParams) =>
                    {
                        var notification = MetaClient.MetadataForNotificationByTenantAndIdentifier(tenant.Id, notificationIdentifier);

                        if (notification == null || !userId.HasValue)
                        {
                            throw new Exception($"No notification with identifier {notificationIdentifier}");
                        }

                        var notificationTrigger = MetaClient.CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(tenant.Id, notification.Id, userId.Value);
                        notificationTrigger.Params = notificationParams;
                        MetaClient.SaveNotificationTriggerBehalfOfUser(tenant.Id, userId.Value, notificationTrigger);
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

    public virtual async Task SaveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, string identifier, Dictionary<string, object> claims, bool insert, object item)
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
                    .SetValue("item", JsonConvert.SerializeObject(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(claims)
                    .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                    .SetWritingEntityFunctions(tenant, userId, db, MetaClient, TenantDataAdapter, claims)
                    .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                    .SetWritingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                    .SetValue("getProcessingStateName",
                        new Func<int, string?>((state) => MetaClient.SingleProcessingStateForTenantAndEntityByValue(tenant.Id, entity.Entity, state)?.Name))
                    .SetValue("triggerNotification", new Action<string, string>((notificationIdentifier, notificationParams) =>
                    {
                        var notification = MetaClient.MetadataForNotificationByTenantAndIdentifier(tenant.Id, notificationIdentifier);

                        if (notification == null || !userId.HasValue)
                        {
                            throw new Exception($"No notification with identifier {notificationIdentifier}");
                        }

                        var notificationTrigger = MetaClient.CreateNotificationTriggerForTenantAndNotificationBehalfOfUser(tenant.Id, notification.Id, userId.Value);
                        notificationTrigger.Params = notificationParams;
                        MetaClient.SaveNotificationTriggerBehalfOfUser(tenant.Id, userId.Value, notificationTrigger);
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

    public async Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, object p)
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
                        TenantDataAdapter.QuerySingle(db, tenant, claims, entity, identifier, p)))
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                .SetWritingEntityFunctions(tenant, userId, db, MetaClient, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" +
                          "function internalPreliminaryRemoveScript() { " + entity.RemovePreliminaryCheckScript + " } \n"
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

    public async Task RemoveScriptAsync(IDbConnection db, ServiceTenant tenant, ServiceEntity entity, Guid? userId, Dictionary<string, object> claims, object p)
    {
        if (!string.IsNullOrEmpty(entity.RemoveScript))
        {
            new Engine()
                .SetValue("params", p)
                .SetValue("tenantId", tenant.Id)
                .SetJsonFunctions()
                .SetClaimFunctions(claims)
                .SetReadingEntityFunctions(tenant, db, MetaClient, TenantDataAdapter, claims)
                .SetWritingEntityFunctions(tenant, userId, db, MetaClient, TenantDataAdapter, claims)
                .SetReadingSqlFunctions(tenant.Id, db, TenantDataAdapter)
                .Evaluate((tenant.ServerScriptDefinitions ?? "") + "\n" + entity.RemoveScript);
        }

        await Task.CompletedTask;
    }
}
