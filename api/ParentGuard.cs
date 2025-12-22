using System.Net;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

public static class ParentGuard
{
    private const string HeaderName = "x-parent-id";

    public static bool TryGetParent(HttpRequestData req, out Guid parentId, out HttpResponseData? errorResponse)
    {
        parentId = Guid.Empty;
        errorResponse = null;

        if (req.Headers.TryGetValues(HeaderName, out var headerValues))
        {
            var first = headerValues.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first) && Guid.TryParse(first, out parentId))
            {
                return true;
            }
        }

        var query = QueryHelpers.ParseQuery(req.Url.Query);
        if (query.TryGetValue("parentId", out var parentValues) && Guid.TryParse(parentValues.ToString(), out parentId))
        {
            return true;
        }

        errorResponse = CreateError(req, HttpStatusCode.BadRequest, "ParentId is required (header 'x-parent-id' or ?parentId=)");
        return false;
    }

    public static bool TryEnsureParent(HttpRequestData req, Guid expectedParent, out HttpResponseData? errorResponse)
    {
        if (!TryGetParent(req, out var parentId, out errorResponse))
        {
            return false;
        }

        if (parentId != expectedParent)
        {
            errorResponse = CreateError(req, HttpStatusCode.Forbidden, "ParentId does not match request target");
            return false;
        }

        return true;
    }

    public static HttpResponseData CreateError(HttpRequestData req, HttpStatusCode status, string message)
    {
        var res = req.CreateResponse(status);
        res.WriteAsJsonAsync(new { error = message }).GetAwaiter().GetResult();
        return res;
    }
}
