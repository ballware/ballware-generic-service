using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Ballware.Generic.Metadata;
using Jint;

namespace Ballware.Generic.Scripting.Jint.Internal;

public class JintEntityMetadataScriptingExecutor : IGenericEntityScriptingExecutor
{
    private IScriptingTenantDataProvider ScriptingTenantDataProvider { get; }
    private IMetadataAdapter MetadataAdapter { get; }
    
    private string TenantIdVariableName { get; } = "tenantId";

    public JintEntityMetadataScriptingExecutor(IScriptingTenantDataProvider scriptingTenantDataProvider, IMetadataAdapter metadataAdapter)
    {
        ScriptingTenantDataProvider = scriptingTenantDataProvider;
        MetadataAdapter = metadataAdapter;
    }

    public virtual IEnumerable<T> ListScript<T>(IScriptingEntityUserContext context, string identifier, IEnumerable<T> results) where T : class
    {
        if (!string.IsNullOrEmpty(context.Entity.ListScript))
        {
            return results.Select(item =>
            {
                new Engine()
                    .SetValue("identifier", identifier)
                    .SetJsonFunctions()
                    .SetClaimFunctions(context.Claims)
                    .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetValue("item", item)
                    .SetValue("addProperty",
                        new Action<string, object>((prop, value) =>
                        {
                            (item as IDictionary<string, object>)?.Add(prop, value);
                        }))
                    .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" +
                              context.Entity.ListScript);

                return item;
            });
        }

        return results;
    }

    public virtual async Task<T> ByIdScriptAsync<T>(IScriptingEntityUserContext context, string identifier, T item) where T : class
    {
        if (!string.IsNullOrEmpty(context.Entity.ByIdScript))
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
                .SetClaimFunctions(context.Claims)
                .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" + context.Entity.ByIdScript);
        }

        return await Task.FromResult(item);
    }

    public virtual async Task BeforeSaveScriptAsync(IScriptingEntityUserContext context, string identifier, bool insert, object item)
    {
        if (!string.IsNullOrEmpty(context.Entity.BeforeSaveScript))
        {
            try
            {
                var engine = new Engine();

                engine
                    .SetValue("log", new Action<object>((obj) => { Debug.WriteLine($"{obj}"); }))
                    .SetValue(TenantIdVariableName, context.Tenant.Id)
                    .SetValue("identifier", identifier)
                    .SetValue("insert", insert)
                    .SetValue("item", JsonSerializer.Serialize(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(context.Claims)
                    .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetWritingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetValue("getProcessingStateName",
                        new Func<int, string?>((state) =>
                            MetadataAdapter
                                .SingleProcessingStateForTenantAndEntityByValue(context.Tenant.Id, context.Entity.Identifier, state)
                                ?.Name))
                    .SetValue("triggerNotification", new Action<string, string>(
                        (notificationIdentifier, notificationParams) =>
                        {
                            var notification =
                                MetadataAdapter.MetadataForNotificationByTenantAndIdentifier(context.Tenant.Id,
                                    notificationIdentifier);

                            if (notification == null)
                            {
                                throw new ArgumentException($"No notification with identifier {notificationIdentifier}");
                            }

                            MetadataAdapter.CreateNotificationTriggerForTenantBehalfOfUser(context.Tenant.Id, context.UserId, new NotificationTriggerCreatePayload()
                            {
                                NotificationId = notification.Id
                            });
                        }))
                    .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" +
                              "item=JSON.parse(item);" +
                              context.Entity.BeforeSaveScript);
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                throw new InvalidOperationException("Error executing save script", ex);
            }
        }

        await Task.CompletedTask;
    }

    public virtual async Task SaveScriptAsync(IScriptingEntityUserContext context, string identifier, bool insert, object item)
    {

        if (!string.IsNullOrEmpty(context.Entity.SaveScript))
        {
            try
            {
                var engine = new Engine();

                engine
                    .SetValue("log", new Action<object>((obj) => { Debug.WriteLine($"{obj}"); }))
                    .SetValue(TenantIdVariableName, context.Tenant.Id)
                    .SetValue("identifier", identifier)
                    .SetValue("insert", insert)
                    .SetValue("item", JsonSerializer.Serialize(item))
                    .SetJsonFunctions()
                    .SetClaimFunctions(context.Claims)
                    .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetWritingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetWritingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                    .SetValue("getProcessingStateName",
                        new Func<int, string?>((state) =>
                            MetadataAdapter
                                .SingleProcessingStateForTenantAndEntityByValue(context.Tenant.Id, context.Entity.Identifier, state)
                                ?.Name))
                    .SetValue("triggerNotification", new Action<string, string>(
                        (notificationIdentifier, notificationParams) =>
                        {
                            var notification =
                                MetadataAdapter.MetadataForNotificationByTenantAndIdentifier(context.Tenant.Id,
                                    notificationIdentifier);

                            if (notification == null)
                            {
                                throw new ArgumentException($"No notification with identifier {notificationIdentifier}");
                            }

                            MetadataAdapter.CreateNotificationTriggerForTenantBehalfOfUser(context.Tenant.Id, context.UserId, new NotificationTriggerCreatePayload()
                            {
                                NotificationId = notification.Id
                            });
                        }))
                    .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" + "item=JSON.parse(item);" + "\n" +
                              context.Entity.SaveScript);
            }
            catch (Exception ex)
            {
                Debug.Write(ex);
                throw new InvalidOperationException("Error executing save script", ex);
            }
        }

        await Task.CompletedTask;
    }

    public async Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IScriptingEntityUserContext context, object p)
    {
        var resultMessages = new List<string>();

        if (!string.IsNullOrEmpty(context.Entity.RemovePreliminaryCheckScript))
        {
            var result = bool.Parse(new Engine()
                .SetValue("params", p)
                .SetValue(TenantIdVariableName, context.Tenant.Id)
                .SetValue("addResultMessage", new Action<string>((msg) => { resultMessages.Add(msg); }))
                .SetValue("querySingle",
                    new Func<string, dynamic, dynamic>((identifier, p) =>
                        ScriptingTenantDataProvider.QuerySingle(context, identifier, p)))
                .SetJsonFunctions()
                .SetClaimFunctions(context.Claims)
                .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .SetWritingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" +
                          "function internalPreliminaryRemoveScript() { " + context.Entity.RemovePreliminaryCheckScript +
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

    public async Task RemoveScriptAsync(IScriptingEntityUserContext context, object p)
    {
        if (!string.IsNullOrEmpty(context.Entity.RemoveScript))
        {
            new Engine()
                .SetValue("params", p)
                .SetValue(TenantIdVariableName, context.Tenant.Id)
                .SetJsonFunctions()
                .SetClaimFunctions(context.Claims)
                .SetReadingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .SetWritingEntityFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .SetReadingSqlFunctions(context, MetadataAdapter, ScriptingTenantDataProvider)
                .Evaluate((context.Tenant.ServerScriptDefinitions ?? "") + "\n" + context.Entity.RemoveScript);
        }

        await Task.CompletedTask;
    }

    public async Task<bool> StateAllowedScriptAsync(IScriptingEntityUserContext context, Guid id, int currentState, IEnumerable<string> rights)
    {
        if (!string.IsNullOrEmpty(context.Entity.StateAllowedScript))
        {
            var result = bool.Parse(new Engine()
                .SetValue("state", currentState)
                .SetValue("hasRight", new Func<string, bool>((right) => { return rights?.Contains(right.ToLowerInvariant()) ?? false; }))
                .SetValue("hasAnyRight", new Func<string, bool>((right) => { return rights?.Any(r => r.StartsWith(right.ToLowerInvariant())) ?? false; }))
                .SetValue("getValue", new Func<string, object>((column) => ScriptingTenantDataProvider.QueryScalarValue(context, column, new Dictionary<string, object>() { { "id", id } })))
                .Evaluate(context.Entity.StateAllowedScript)
                .ToString());
            
            return await Task.FromResult(result);
        }
        
        return await Task.FromResult(false);
    }

}
