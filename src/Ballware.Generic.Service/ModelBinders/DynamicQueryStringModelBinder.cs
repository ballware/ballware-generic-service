using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Primitives;

namespace Ballware.Generic.Service.ModelBinders;

public class DynamicQueryStringModelBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        var filters = new Dictionary<string, StringValues>();

        foreach (var kvp in bindingContext.HttpContext.Request.Query)
        {
            filters.Add(kvp.Key, kvp.Value);
        }

        bindingContext.Result = ModelBindingResult.Success(filters);

        await Task.CompletedTask;
    }
}