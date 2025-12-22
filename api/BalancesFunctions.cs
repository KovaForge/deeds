using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class BalancesFunctions
{
    private readonly string _cs;

    public BalancesFunctions(DbOptions options)
    {
        _cs = options.ConnectionString;
    }

    [Function("GetChildBalance")]
    public async Task<HttpResponseData> GetChildBalance(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "balances/{childId:guid}")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, out var parentId, out var error))
        {
            return error;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var balance = await Data.GetBalance(_cs, childId) ?? new BalanceDto(childId, 0, 0m);
        var res = req.CreateResponse(HttpStatusCode.OK);
        await res.WriteAsJsonAsync(balance);
        return res;
    }
}
