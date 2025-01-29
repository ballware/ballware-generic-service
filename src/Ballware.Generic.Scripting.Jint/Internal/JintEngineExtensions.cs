using System.Data;
using Ballware.Meta.Client;
using Jint;
using Newtonsoft.Json;

namespace Ballware.Generic.Scripting.Jint.Internal
{
    static class JintEngineExtensions
    {
        public static Engine SetClaimFunctions(this Engine engine, Dictionary<string, object> claims)
        {
            return engine
                .SetValue("getClaim", new Func<string, string?>(claim => ClaimUtils.GetClaim(claims, claim)))
                .SetValue("hasClaim", new Func<string, string, bool>((claim, value) => ClaimUtils.HasClaim(claims, claim, value)))
                .SetValue("hasAnyClaim", new Func<string, string, bool>((claim, value) => ClaimUtils.HasAnyClaim(claims, claim, value)));
        }

        public static Engine SetJsonFunctions(this Engine engine)
        {
            return engine
                .SetValue("parse", new Func<string, dynamic?>(str => JsonConvert.DeserializeObject<dynamic>(str)))
                .SetValue("stringify", new Func<object, string>(obj =>
                {
                    var result = JsonConvert.SerializeObject(obj);

                    return result;
                }));
        }

        public static Engine SetReadingEntityFunctions(this Engine engine, ServiceTenant tenant, IDbConnection db, BallwareMetaClient metaClient, ITenantDataAdapter tenantDataAdapter, Dictionary<string, object> claims)
        {
            return engine
                .SetValue("entityCount", new Func<string, string, string, dynamic, long>((application, entity, query, p) =>
                    {
                        p.tenantId = tenant.Id;
                        return tenantDataAdapter.Count(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), claims, query, p);
                    }))
                .SetValue("entityQueryList", new Func<string, string, string, dynamic, object[]>((application, entity, query, p) =>
                    {
                        p.tenantId = tenant.Id;
                        return tenantDataAdapter.QueryList(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), claims, query, p).ToArray();
                    }))
                .SetValue("entityQuerySingle", new Func<string, string, string, dynamic, object>((application, entity, query, p) =>
                    {
                        p.tenantId = tenant.Id;
                        return tenantDataAdapter.QuerySingle(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), claims, query, p);
                    }))
                .SetValue("entityNew", new Func<string, string, string, dynamic, object>((application, entity, query, p) =>
                    {
                        p.tenantId = tenant.Id;
                        return tenantDataAdapter.QueryNew(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), claims, query, p);
                    }));
        }

        public static Engine SetWritingEntityFunctions(this Engine engine, ServiceTenant tenant, Guid? userId, IDbConnection db, BallwareMetaClient metaClient, ITenantDataAdapter tenantDataAdapter, Dictionary<string, object> claims)
        {
            return engine
                .SetValue("entitySave", new Action<string, string, string, dynamic>((application, entity, statement, p) =>
                    {
                        p.tenantId = tenant.Id;
                        tenantDataAdapter.Save(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), userId, claims, statement, p);
                    }))
                .SetValue("entityRemove", new Action<string, string, dynamic>((application, entity, p) =>
                    {
                        tenantDataAdapter.Remove(db, tenant, metaClient.MetadataForEntityByTenantdAndIdentifier(tenant.Id, entity), userId, claims, p);
                    }));
        }

        public static Engine SetReadingSqlFunctions(this Engine engine, Guid tenantId, IDbConnection db, ITenantDataAdapter tenantDataAdapter)
        {
            return engine
                .SetValue("getCount", new Func<string, string, object, int>((table, where, p) => tenantDataAdapter.RawCount(db, table, where, p)))
                .SetValue("getList",
                    new Func<string, string, string, dynamic, object[]>((table, columns, where, p) =>
                    {
                        var sqlParams = new Dictionary<string, object>(p);

                        sqlParams["tenantId"] = tenantId;

                        return tenantDataAdapter.RawQuery(db, table, columns, where, sqlParams).ToArray();
                    }))
                .SetValue("getSingleColumnList",
                    new Func<string, string, string, dynamic, object?[]>((table, column, where, p) =>
                    {
                        var sqlParams = new Dictionary<string, object>(p);

                        sqlParams["tenantId"] = tenantId;

                        return tenantDataAdapter.RawQuery(db, table, column, where, sqlParams)
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

                        sqlParams["tenantId"] = tenantId;

                        return tenantDataAdapter.RawQuery(db, table, column, where, sqlParams)
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

        public static Engine SetWritingSqlFunctions(this Engine engine, Guid tenantId, IDbConnection db, ITenantDataAdapter tenantDataAdapter)
        {
            return engine
                .SetValue("dbDelete", new Action<string, string, dynamic>((table, where, p) =>
                    {
                        p.tenantId = tenantId;

                        tenantDataAdapter.RawDelete(db, table, where, p);
                    }))
                .SetValue("dbInsert",
                    new Action<string, string, string, dynamic>((table, columns, values, p) =>
                    {
                        p.tenantId = tenantId;

                        tenantDataAdapter.RawInsert(db, table, columns, values, p);
                    }))
                .SetValue("dbUpdate",
                    new Action<string, string, string, dynamic>((table, columns, where, p) =>
                    {
                        p.tenantId = tenantId;

                        tenantDataAdapter.RawUpdate(db, table, columns, where, p);
                    }));
        }
    }
}