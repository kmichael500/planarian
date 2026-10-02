using Planarian.Library.Exceptions;
using Planarian.Modules.Map.Controllers;
using Planarian.Modules.Map.Models;
using Xunit;

namespace Planarian.Tests.EmailDelivery.Map;

public sealed class HydrologyRequestValidationTests
{
    [Theory]
    [InlineData(double.NaN, -86d, 25d)]
    [InlineData(35d, double.NaN, 25d)]
    [InlineData(35d, -86d, double.NaN)]
    public async Task NearbyGagesRejectNonFiniteSearchValues(
        double latitude,
        double longitude,
        double distanceMiles)
    {
        var service = new MapService(null!, null!, null!, [], null!);
        var request = new StreamGageRequest(
            [new StreamGageSearchOrigin("origin", "Origin", latitude, longitude)],
            distanceMiles);

        var error = await Assert.ThrowsAsync<ApiException>(() =>
            service.GetNearbyStreamGages(request, CancellationToken.None));

        Assert.Equal(400, error.StatusCode);
    }
}
