using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class BalancesFunctions
{
    private readonly string _cs;

    public BalancesFunctions(DbOptions options)
    {
        _cs = options?.ConnectionString ?? string.Empty;
        if (string.IsNullOrWhiteSpace(_cs))
        {
            throw new InvalidOperationException("Database connection string (DB environment variable) is not configured. Set the DB environment variable in Azure Static Web Apps configuration.");
        }
    }

    [Function("GetChildBalance")]
    public async Task<HttpResponseData> GetChildBalance(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "balances/{childId:guid}")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, _cs, out var parentId, out var error))
        {
            return error;
        }

        try
        {
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
        catch (Exception ex)
        {
            var res = req.CreateResponse(HttpStatusCode.InternalServerError);
            await res.WriteAsJsonAsync(new { error = $"Failed to fetch balance: {ex.Message}" });
            return res;
        }
    }

    private static async Task<HttpResponseData> CreateErrorResponse(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        await res.WriteAsJsonAsync(new { error = message });
        return res;
    }
}
