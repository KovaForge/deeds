using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public class ExportFunctions
{
    private readonly string _cs;

    public ExportFunctions(DbOptions options)
    {
        _cs = options.ConnectionString;
    }

    [Function("ExportChildHistoryCsv")]
    public async Task<HttpResponseData> ExportChildHistoryCsv(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "children/{childId:guid}/export/csv")] HttpRequestData req,
        Guid childId)
    {
        if (!ParentGuard.TryGetParent(req, out var parentId, out var guardError))
        {
            return guardError;
        }

        var child = await Data.GetChildById(_cs, childId);
        if (child is null || child.ParentId != parentId)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var history = await Data.GetChildHistory(_cs, childId);
        var csv = Data.ToCsv(history);

        var res = req.CreateResponse(HttpStatusCode.OK);
        res.Headers.Add("Content-Type", "text/csv; charset=utf-8");
        res.Headers.Add("Content-Disposition", $"attachment; filename=child-{childId}-history.csv");
        await res.WriteStringAsync(csv);
        return res;
    }
}
