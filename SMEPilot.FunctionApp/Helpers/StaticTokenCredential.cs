using Azure.Core;

namespace SMEPilot.FunctionApp.Helpers
{
    public class StaticTokenCredential : TokenCredential
    {
        private readonly AccessToken _token;

        public StaticTokenCredential(AccessToken token)
        {
            _token = token;
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return new ValueTask<AccessToken>(_token);
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            return _token;
        }
    }
}

