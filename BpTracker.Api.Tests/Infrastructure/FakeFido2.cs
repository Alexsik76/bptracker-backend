using System.Threading;
using System.Threading.Tasks;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace BpTracker.Api.Tests.Infrastructure;

public class FakeFido2 : IFido2
{
    public CredentialCreateOptions RequestNewCredential(RequestNewCredentialParams requestParameters)
    {
        return new CredentialCreateOptions
        {
            Challenge = [1, 2, 3, 4],
            Rp = new PublicKeyCredentialRpEntity("localhost", "BP Tracker"),
            User = requestParameters.User,
            PubKeyCredParams = []
        };
    }

    public Task<RegisteredPublicKeyCredential> MakeNewCredentialAsync(MakeNewCredentialParams makeNewCredentialParams, CancellationToken cancellationToken = default)
    {
        var registered = new RegisteredPublicKeyCredential
        {
            Id = makeNewCredentialParams.AttestationResponse.RawId,
            PublicKey = [4, 5, 6],
            SignCount = 1
        };
        return Task.FromResult(registered);
    }

    public AssertionOptions GetAssertionOptions(GetAssertionOptionsParams assertionParameters)
    {
        return new AssertionOptions
        {
            Challenge = [1, 2, 3, 4]
        };
    }

    public Task<VerifyAssertionResult> MakeAssertionAsync(MakeAssertionParams makeAssertionParams, CancellationToken cancellationToken = default)
    {
        var result = new VerifyAssertionResult
        {
            SignCount = 2
        };
        return Task.FromResult(result);
    }
}
