using System.Data;
using System.Text.Json;
using Ballware.Generic.Metadata;
using Jint;

namespace Ballware.Generic.Scripting.Jint.Internal
{
    static class JintEngineExtensions
    {
        public static Engine SetMlFunctions(this Engine engine, Guid tenantId, Guid userId, IMlAdapter mlAdapter)
        {
            return engine.SetValue("mlPredict", new Func<string, IDictionary<string, object>, object>((model, input) =>
            {
                var predictInput = new Dictionary<string, object>();

                foreach (var val in input)
                {
                    predictInput.Add(val.Key, val.Value.ToString());
                }
                
                return mlAdapter.ConsumeByIdentifierBehalfOfUserAsync(tenantId, userId, model, predictInput).GetAwaiter().GetResult();
            }));
        }
        
        public static Engine SetClaimFunctions(this Engine engine, IDictionary<string, object> claims)
        {
            return engine
                .SetValue("getClaim", new Func<string, string?>(claim => ClaimUtils.GetClaim(claims, claim)))
                .SetValue("hasClaim", new Func<string, string, bool>((claim, value) => ClaimUtils.HasClaim(claims, claim, value)))
                .SetValue("hasAnyClaim", new Func<string, string, bool>((claim, value) => ClaimUtils.HasAnyClaim(claims, claim, value)));
        }

        public static Engine SetJsonFunctions(this Engine engine)
        {
            return engine
                .SetValue("parse", new Func<string, dynamic?>(str => JsonSerializer.Deserialize<dynamic>(str)))
                .SetValue("stringify", new Func<object, string>(obj =>
                {
                    var result = JsonSerializer.Serialize(obj);

                    return result;
                }));
        }

        public static Engine SetReadingEntityFunctions(this Engine engine, IScriptingEntityUserContext context, IMetadataAdapter metadataAdapter, IScriptingTenantDataAdapter scriptingTenantDataAdapter)
        {
            return engine
                .SetValue("entityCount", new Func<string, string, string, dynamic, long>((application, entity, query, p) =>
                    {
                        p.tenantId = context.Tenant.Id;
                        return scriptingTenantDataAdapter.Count(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), query, p);
                    }))
                .SetValue("entityQueryList", new Func<string, string, string, dynamic, object[]>((application, entity, query, p) =>
                    {
                        p.tenantId = context.Tenant.Id;
                        return scriptingTenantDataAdapter.QueryList(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), query, p).ToArray();
                    }))
                .SetValue("entityQuerySingle", new Func<string, string, string, dynamic, object>((application, entity, query, p) =>
                    {
                        p.tenantId = context.Tenant.Id;
                        return scriptingTenantDataAdapter.QuerySingle(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), query, p);
                    }))
                .SetValue("entityNew", new Func<string, string, string, dynamic, object>((application, entity, query, p) =>
                    {
                        p.tenantId = context.Tenant.Id;
                        return scriptingTenantDataAdapter.QueryNew(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), query, p);
                    }));
        }

        public static Engine SetWritingEntityFunctions(this Engine engine, IScriptingEntityUserContext context, IMetadataAdapter metadataAdapter, IScriptingTenantDataAdapter scriptingTenantDataAdapter)
        {
            return engine
                .SetValue("entitySave", new Action<string, string, string, dynamic>((application, entity, statement, p) =>
                    {
                        p.tenantId = context.Tenant.Id;
                        scriptingTenantDataAdapter.Save(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), statement, p);
                    }))
                .SetValue("entityRemove", new Action<string, string, dynamic>((application, entity, p) =>
                    {
                        scriptingTenantDataAdapter.Remove(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, entity)), p);
                    }));
        }

        public static Engine SetReadingSqlFunctions(this Engine engine, IScriptingEntityUserContext context, IMetadataAdapter metadataAdapter, IScriptingTenantDataAdapter scriptingTenantDataAdapter)
        {
            return engine
                .SetValue("getCount", new Func<string, string, object, int>((table, where, p) => scriptingTenantDataAdapter.RawCount(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, where, p)))
                .SetValue("getList",
                    new Func<string, string, string, dynamic, object[]>((table, columns, where, p) =>
                    {
                        var sqlParams = new Dictionary<string, object>(p);

                        sqlParams["tenantId"] = context.Tenant.Id;

                        return scriptingTenantDataAdapter.RawQuery(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, columns, where, sqlParams).ToArray();
                    }))
                .SetValue("getSingleColumnList",
                    new Func<string, string, string, dynamic, object?[]>((table, column, where, p) =>
                    {
                        var sqlParams = new Dictionary<string, object>(p);

                        sqlParams["tenantId"] = context.Tenant.Id;

                        return scriptingTenantDataAdapter.RawQuery(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, column, where, sqlParams)
                            .Select(d =>
                            {
                                if ((d as IDictionary<string, object>)?.TryGetValue(column, out var value) ?? false)
                                {
                                    return value;
                                }

                                return null;
                            }).ToArray();
                    }))
                .SetValue("getStringColumnList",
                    new Func<string, string, string, dynamic, string?[]>((table, column, where, p) =>
                    {
                        var sqlParams = new Dictionary<string, object>(p);

                        sqlParams["tenantId"] = context.Tenant.Id;

                        return scriptingTenantDataAdapter.RawQuery(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, column, where, sqlParams)
                            .Select(d =>
                            {
                                if ((d as IDictionary<string, object>)?.TryGetValue(column, out var value) ?? false)
                                {
                                    return value.ToString();
                                }

                                return null;
                            }).ToArray();
                    }));
        }

        public static Engine SetWritingSqlFunctions(this Engine engine, IScriptingEntityUserContext context, IMetadataAdapter metadataAdapter, IScriptingTenantDataAdapter scriptingTenantDataAdapter)
        {
            return engine
                .SetValue("dbDelete", new Action<string, string, dynamic>((table, where, p) =>
                    {
                        p.tenantId = context.Tenant.Id;

                        scriptingTenantDataAdapter.RawDelete(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, where, p);
                    }))
                .SetValue("dbInsert",
                    new Action<string, string, string, dynamic>((table, columns, values, p) =>
                    {
                        p.tenantId = context.Tenant.Id;

                        scriptingTenantDataAdapter.RawInsert(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, columns, values, p);
                    }))
                .SetValue("dbUpdate",
                    new Action<string, string, string, dynamic>((table, columns, where, p) =>
                    {
                        p.tenantId = context.Tenant.Id;

                        scriptingTenantDataAdapter.RawUpdate(DefaultScriptingEntityUserContext.DuplicateForEntity(context, metadataAdapter.MetadataForEntityByTenantAndIdentifier(context.Tenant.Id, table)), table, columns, where, p);
                    }));
        }
    }
}