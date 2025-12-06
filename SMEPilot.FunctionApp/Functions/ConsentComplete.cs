using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace SMEPilot.FunctionApp.Functions
{
    /// <summary>
    /// Simple landing endpoint used as redirect target after Azure AD admin consent.
    /// This does not perform any authentication or Graph calls – it only shows a friendly message.
    /// </summary>
    public class ConsentComplete
    {
        [Function("ConsentComplete")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "options")] HttpRequestData req)
        {
            // Handle potential OPTIONS preflight, just in case browsers send it
            if (req.Method == "OPTIONS")
            {
                var preflight = req.CreateResponse(HttpStatusCode.OK);
                preflight.Headers.Add("Access-Control-Allow-Origin", "*");
                preflight.Headers.Add("Access-Control-Allow-Methods", "GET, OPTIONS");
                preflight.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization");
                return preflight;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "text/html; charset=utf-8");

            // Very small, static confirmation page – no secrets, no tenancy info
            const string html = @"
<!DOCTYPE html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"" />
    <title>SMEPilot – Permissions granted</title>
    <style>
      body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; margin: 40px; color: #323130; }
      .card { max-width: 640px; margin: 0 auto; padding: 32px; border-radius: 8px; border: 1px solid #edebe9; box-shadow: 0 2px 4px rgba(0,0,0,0.06); }
      h1 { font-size: 22px; margin-top: 0; color: #0078d4; }
      p { font-size: 14px; line-height: 1.6; }
      .muted { color: #605e5c; }
    </style>
  </head>
  <body>
    <div class=""card"">
      <h1>SMEPilot permissions granted</h1>
      <p>Thank you. The required Microsoft 365 permissions for SMEPilot have been granted successfully.</p>
      <p class=""muted"">
        You can now close this tab and return to the SMEPilot configuration page in SharePoint to complete the setup.
      </p>
    </div>
  </body>
</html>";

            await response.WriteStringAsync(html);
            return response;
        }
    }
}



