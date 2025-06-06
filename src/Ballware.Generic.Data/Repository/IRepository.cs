namespace Ballware.Generic.Data.Repository;

public struct RemoveResult
{
    public bool Result;
    public IEnumerable<string> Messages;
}

public interface IRepository<TEditable> where TEditable : class
{
    Task<TEditable?> ByIdAsync(string identifier, IDictionary<string, object> claims, Guid id);
    Task<TEditable> NewAsync(string identifier, IDictionary<string, object> claims);

    Task SaveAsync(Guid? userId, string identifier, IDictionary<string, object> claims, TEditable value);

    Task<RemoveResult> RemoveAsync(Guid? userId, IDictionary<string, object> claims, IDictionary<string, object> removeParams);
}