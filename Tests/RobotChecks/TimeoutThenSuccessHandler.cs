using System.Net;
using System.Text;

internal sealed class TimeoutThenSuccessHandler : HttpMessageHandler
{
    private int _calls;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Interlocked.Increment(ref _calls) == 1) throw new OperationCanceledException();
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"message\":{\"content\":\"{\\\"category\\\":\\\"unknown\\\",\\\"urgency\\\":0,\\\"summary\\\":\\\"test\\\",\\\"confidence\\\":0.5,\\\"keywords\\\":[]}\"}}", Encoding.UTF8, "application/json")
        });
    }
}
