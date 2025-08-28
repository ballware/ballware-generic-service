using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting
{
    public interface IScriptingTenantDataAdapter
    {
        IEnumerable<dynamic> RawQuery(IScriptingEntityUserContext context, string table, string columns, string where, object p);
        int RawCount(IScriptingEntityUserContext context, string table, string where, object p);
        void RawDelete(IScriptingEntityUserContext context, string table, string where, object p);
        void RawInsert(IScriptingEntityUserContext context, string table, string columns, string values, object p);
        void RawUpdate(IScriptingEntityUserContext context, string table, string columns, string where, object p);

        object? QueryScalarValue(IScriptingEntityUserContext context, string column, IDictionary<string, object> p);
        long Count(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p);
        IEnumerable<dynamic> QueryList(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p);
        dynamic? QuerySingle(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p);
        dynamic? QueryNew(IScriptingEntityUserContext context, string queryIdentifier, IDictionary<string, object> p);
        void Save(IScriptingEntityUserContext context, string statementIdentifier, IDictionary<string, object> p);
        (bool Result, IEnumerable<string> Messages) Remove(IScriptingEntityUserContext context, IDictionary<string, object> p);
    }
}