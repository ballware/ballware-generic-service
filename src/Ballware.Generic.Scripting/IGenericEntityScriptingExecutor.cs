using System.Data;
using Ballware.Generic.Metadata;

namespace Ballware.Generic.Scripting;

public interface IGenericEntityScriptingExecutor
{
    public IEnumerable<T> ListScript<T>(IScriptingEntityUserContext context, string identifier, IEnumerable<T> results) where T : class;    
    public Task<T> ByIdScriptAsync<T>(IScriptingEntityUserContext context, string identifier, T item) where T : class;
    public Task BeforeSaveScriptAsync(IScriptingEntityUserContext context, string identifier, bool insert, object item);
    public Task SaveScriptAsync(IScriptingEntityUserContext context, string identifier, bool insert, object item);
    public Task<(bool Result, IEnumerable<string> Messages)> RemovePreliminaryCheckAsync(IScriptingEntityUserContext context, object p);
    public Task RemoveScriptAsync(IScriptingEntityUserContext context, object p);
    public Task<bool> StateAllowedScriptAsync(IScriptingEntityUserContext context, Guid id, int currentState, IEnumerable<string> rights);
}