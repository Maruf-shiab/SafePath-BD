using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Controllers.Api;
using SafePathBD.Web.Integrations.Routing;
using SafePathBD.Web.Models.DTOs.Routing;
using SafePathBD.Web.Services.Implementations;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Tests;

public class RoutingServiceTests
{
    private static readonly RoutePointRequest Start = new(23.7500, 90.3700, "Start");
    private static readonly RoutePointRequest End = new(23.7600, 90.3900, "End");

    [Fact]
    public async Task MinimumDistanceCandidate_IsShortest()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[]
        {
            Route(0, 7000, 800, 23.7550),
            Route(1, 5500, 950, 23.7560)
        });
        var incidents = new FakeIncidentService(Clear(), Clear());
        var service = CreateService(ctx, provider, incidents);

        var result = await service.SearchAsync(new RouteSearchRequest(Start, End), null);

        Assert.Equal(RouteSearchStatus.Success, result.Status);
        Assert.True(result.Response!.Candidates.Single(c => c.ProviderIndex == 1).IsShortest);
    }

    [Fact]
    public async Task MinimumDurationCandidate_IsFastest()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[]
        {
            Route(0, 7000, 720, 23.7550),
            Route(1, 5500, 960, 23.7560)
        });
        var service = CreateService(ctx, provider, new FakeIncidentService(Clear(), Clear()));

        var result = await service.SearchAsync(new RouteSearchRequest(Start, End), null);

        Assert.True(result.Response!.Candidates.Single(c => c.ProviderIndex == 0).IsFastest);
    }

    [Fact]
    public async Task SameCandidate_CanBeShortestAndFastest()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[]
        {
            Route(0, 5000, 700, 23.7550),
            Route(1, 6500, 900, 23.7560)
        });
        var service = CreateService(ctx, provider, new FakeIncidentService(Clear(), Clear()));

        var response = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;
        var first = response.Candidates.Single(c => c.ProviderIndex == 0);

        Assert.True(first.IsShortest);
        Assert.True(first.IsFastest);
        Assert.Equal(first.CandidateKey, response.ShortestCandidateKey);
        Assert.Equal(first.CandidateKey, response.FastestCandidateKey);
    }

    [Fact]
    public async Task AffectedShortest_ChoosesShortestClearAlternative()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[]
        {
            Route(0, 5000, 700, 23.7550),
            Route(1, 5800, 760, 23.7560),
            Route(2, 6400, 780, 23.7570)
        });
        var service = CreateService(ctx, provider, new FakeIncidentService(Affected(), Clear(), Clear()));

        var response = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;
        var recommended = response.Candidates.Single(c => c.CandidateKey == response.RecommendedCandidateKey);

        Assert.True(response.Candidates.Single(c => c.ProviderIndex == 0).IsShortest);
        Assert.Equal(1, recommended.ProviderIndex);
        Assert.True(recommended.IsRecommendedAlternative);
        Assert.Equal(0.8, response.RecommendedDistanceDeltaKm!.Value, 2);
    }

    [Fact]
    public async Task NoClearAlternative_ChoosesShortestCautionCandidate()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[]
        {
            Route(0, 5000, 700, 23.7550),
            Route(1, 5600, 760, 23.7560),
            Route(2, 6200, 800, 23.7570)
        });
        var service = CreateService(ctx, provider, new FakeIncidentService(Affected(), Caution(), Affected()));

        var response = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;
        var recommended = response.Candidates.Single(c => c.CandidateKey == response.RecommendedCandidateKey);

        Assert.Equal(1, recommended.ProviderIndex);
        Assert.Equal(RouteIncidentStates.Caution, recommended.IncidentState);
    }

    [Fact]
    public async Task AllNormalCandidatesAffected_AttemptsRoadRoutedDetourAndRecommendsClearOne()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(request => request.ViaPoints is { Count: > 0 }
            ? new[] { DetourRoute(request.ViaPoints[0], 6100, 850) }
            : new[] { Route(0, 5000, 700, 23.7550), Route(1, 5400, 720, 23.7560) });
        var incidents = new FakeIncidentService(Affected(), Affected(), Clear());
        var service = CreateService(ctx, provider, incidents);

        var response = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;
        var recommended = response.Candidates.Single(c => c.CandidateKey == response.RecommendedCandidateKey);

        Assert.True(response.DetourAttempted);
        Assert.True(recommended.IsDetour);
        Assert.Equal(RouteIncidentStates.Clear, recommended.IncidentState);
        Assert.True(recommended.IsRecommendedAlternative);
        Assert.Equal(2, provider.CallCount);
    }

    [Fact]
    public async Task DetourAttempts_AreStrictlyBounded()
    {
        using var ctx = new ReportTestContext();
        var options = DefaultOptions();
        options.MaxDetourAttempts = 2;
        var provider = new FakeProvider(request => request.ViaPoints is { Count: > 0 }
            ? new[] { DetourRoute(request.ViaPoints[0], 6100, 850) }
            : new[] { Route(0, 5000, 700, 23.7550) });
        var incidents = new FakeIncidentService(Affected(), Affected(), Affected());
        var service = CreateService(ctx, provider, incidents, options);

        var response = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;

        Assert.True(response.DetourAttempted);
        Assert.Equal(1 + options.MaxDetourAttempts, provider.CallCount);
        Assert.DoesNotContain(response.Candidates, c => c.IsDetour);
        Assert.Contains("No clear alternative", response.DetourMessage!);
    }

    [Fact]
    public async Task Recheck_CanChangeCachedClearRouteToAffectedAndRecommendReroute()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[] { Route(0, 5000, 700, 23.7550) });
        var incidents = new FakeIncidentService(Clear(), Affected());
        var service = CreateService(ctx, provider, incidents);

        var search = (await service.SearchAsync(new RouteSearchRequest(Start, End), null)).Response!;
        var recheck = await service.RecheckAsync(
            new RouteRecheckRequest(search.SearchId, search.Candidates[0].CandidateKey), null);

        Assert.Equal(RouteRecheckStatus.Success, recheck.Status);
        Assert.True(recheck.Response!.IncidentChanged);
        Assert.Equal(RouteIncidentStates.Affected, recheck.Response.IncidentState);
        Assert.True(recheck.Response.RerouteRecommended);
    }

    [Fact]
    public async Task UnknownSearchId_ReturnsExpiredInsteadOfCrashing()
    {
        using var ctx = new ReportTestContext();
        var service = CreateService(ctx, new FakeProvider(_ => Array.Empty<ProviderRouteCandidate>()), new FakeIncidentService());

        var result = await service.RecheckAsync(new RouteRecheckRequest(Guid.NewGuid(), "route-1"), null);

        Assert.Equal(RouteRecheckStatus.SearchExpired, result.Status);
    }

    [Fact]
    public async Task InvalidCoordinate_IsRejectedBeforeProviderCall()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[] { Route(0, 5000, 700, 23.7550) });
        var service = CreateService(ctx, provider, new FakeIncidentService(Clear()));

        var result = await service.SearchAsync(
            new RouteSearchRequest(new RoutePointRequest(100, 90, "Invalid"), End), null);

        Assert.Equal(RouteSearchStatus.InvalidRequest, result.Status);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task SamePoint_IsRejectedBeforeProviderCall()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[] { Route(0, 5000, 700, 23.7550) });
        var service = CreateService(ctx, provider, new FakeIncidentService(Clear()));

        var result = await service.SearchAsync(new RouteSearchRequest(Start, Start), null);

        Assert.Equal(RouteSearchStatus.InvalidRequest, result.Status);
        Assert.Equal(0, provider.CallCount);
    }


    [Fact]
    public async Task IncidentCheckFailure_DoesNotDiscardValidProviderRoutes()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[] { Route(0, 5000, 700, 23.7550) });
        var service = CreateService(ctx, provider, new ThrowingIncidentService());

        var result = await service.SearchAsync(new RouteSearchRequest(Start, End), null);

        Assert.Equal(RouteSearchStatus.Success, result.Status);
        Assert.False(result.Response!.IncidentCheckAvailable);
        Assert.Equal(RouteIncidentStates.Unknown, result.Response.Candidates[0].IncidentState);
    }

    [Fact]
    public async Task AuthenticatedSearchCache_IsNotReusableByAnotherUser()
    {
        using var ctx = new ReportTestContext();
        var provider = new FakeProvider(_ => new[] { Route(0, 5000, 700, 23.7550) });
        var incidents = new FakeIncidentService(Clear(), Clear());
        var service = CreateService(ctx, provider, incidents);

        var search = (await service.SearchAsync(new RouteSearchRequest(Start, End), 7)).Response!;
        var recheck = await service.RecheckAsync(
            new RouteRecheckRequest(search.SearchId, search.Candidates[0].CandidateKey), 8);

        Assert.Equal(RouteRecheckStatus.Forbidden, recheck.Status);
    }

    [Fact]
    public async Task ProviderNoRoute_IsReturnedAsDistinctStatus()
    {
        using var ctx = new ReportTestContext();
        var provider = new ThrowingProvider(new RoutingProviderNoRouteException("no route"));
        var service = CreateService(ctx, provider, new FakeIncidentService());

        var result = await service.SearchAsync(new RouteSearchRequest(Start, End), null);

        Assert.Equal(RouteSearchStatus.NoRoute, result.Status);
    }

    [Fact]
    public async Task ProviderUnavailable_IsReturnedAsDistinctStatus()
    {
        using var ctx = new ReportTestContext();
        var provider = new ThrowingProvider(new RoutingProviderUnavailableException("offline"));
        var service = CreateService(ctx, provider, new FakeIncidentService());

        var result = await service.SearchAsync(new RouteSearchRequest(Start, End), null);

        Assert.Equal(RouteSearchStatus.ProviderUnavailable, result.Status);
    }

    private static RoutingService CreateService(
        ReportTestContext ctx,
        IRoutingProvider provider,
        IRouteIncidentService incidents,
        OsrmRoutingOptions? options = null)
    {
        options ??= DefaultOptions();
        return new RoutingService(
            provider,
            incidents,
            ctx.Db,
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(options),
            NullLogger<RoutingService>.Instance);
    }

    private static OsrmRoutingOptions DefaultOptions() => new()
    {
        CacheMinutes = 20,
        IncidentProximityMeters = 50,
        IncidentRecheckSeconds = 60,
        DetourFallbackEnabled = true,
        DetourOffsetMeters = 600,
        MaxDetourAttempts = 4,
        MaxDetourDistanceFactor = 1.75,
        CautionAlternativeDistanceFactor = 1.25
    };

    private static ProviderRouteCandidate Route(int index, double distance, double duration, double middleLatitude) =>
        new(index, distance, duration, new[]
        {
            new RouteCoordinate(Start.Latitude, Start.Longitude),
            new RouteCoordinate(middleLatitude, 90.3800),
            new RouteCoordinate(End.Latitude, End.Longitude)
        });

    private static ProviderRouteCandidate DetourRoute(RouteCoordinate via, double distance, double duration) =>
        new(0, distance, duration, new[]
        {
            new RouteCoordinate(Start.Latitude, Start.Longitude),
            via,
            new RouteCoordinate(End.Latitude, End.Longitude)
        });

    private static RouteIncidentAssessment Clear() =>
        new(RouteIncidentStates.Clear, Array.Empty<RouteIncidentDto>(), DateTime.UtcNow);

    private static RouteIncidentAssessment Caution() =>
        new(RouteIncidentStates.Caution, new[] { Incident(RouteIncidentStates.Caution) }, DateTime.UtcNow);

    private static RouteIncidentAssessment Affected() =>
        new(RouteIncidentStates.Affected, new[] { Incident(RouteIncidentStates.Affected) }, DateTime.UtcNow);

    private static RouteIncidentDto Incident(string level) => new(
        500,
        ReportTypes.Accident,
        "Verified incident",
        23.7550,
        90.3800,
        "Test Area",
        "Vehicle Collision",
        level == RouteIncidentStates.Affected ? "Severe" : "Moderate",
        null,
        null,
        level,
        10,
        DateTime.UtcNow.AddMinutes(-2));

    private sealed class FakeProvider : IRoutingProvider
    {
        private readonly Func<RoutingProviderRequest, IReadOnlyList<ProviderRouteCandidate>> _handler;
        public int CallCount { get; private set; }

        public FakeProvider(Func<RoutingProviderRequest, IReadOnlyList<ProviderRouteCandidate>> handler) => _handler = handler;

        public Task<IReadOnlyList<ProviderRouteCandidate>> GetRoutesAsync(
            RoutingProviderRequest request,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class ThrowingProvider : IRoutingProvider
    {
        private readonly Exception _exception;
        public ThrowingProvider(Exception exception) => _exception = exception;

        public Task<IReadOnlyList<ProviderRouteCandidate>> GetRoutesAsync(
            RoutingProviderRequest request,
            CancellationToken cancellationToken = default) =>
            Task.FromException<IReadOnlyList<ProviderRouteCandidate>>(_exception);
    }

    private sealed class FakeIncidentService : IRouteIncidentService
    {
        private readonly Queue<RouteIncidentAssessment> _responses;
        private RouteIncidentAssessment _last = Clear();

        public FakeIncidentService(params RouteIncidentAssessment[] responses) =>
            _responses = new Queue<RouteIncidentAssessment>(responses);

        public Task<RouteIncidentAssessment> AssessRouteAsync(
            IReadOnlyList<RouteCoordinate> geometry,
            CancellationToken cancellationToken = default)
        {
            if (_responses.Count > 0) { _last = _responses.Dequeue(); }
            return Task.FromResult(_last);
        }
    }


    private sealed class ThrowingIncidentService : IRouteIncidentService
    {
        public Task<RouteIncidentAssessment> AssessRouteAsync(
            IReadOnlyList<RouteCoordinate> geometry,
            CancellationToken cancellationToken = default) =>
            Task.FromException<RouteIncidentAssessment>(new InvalidOperationException("incident db unavailable"));
    }
}


public class RoutingApiControllerTests
{
    private static readonly RouteSearchRequest ValidRequest = new(
        new RoutePointRequest(23.75, 90.37, "A"),
        new RoutePointRequest(23.76, 90.39, "B"));

    [Fact]
    public async Task Search_InvalidRequest_Returns400()
    {
        var controller = ControllerWith(RouteSearchResult.Invalid("invalid"));
        var result = await controller.Search(ValidRequest, CancellationToken.None);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Search_NoRoute_Returns422()
    {
        var controller = ControllerWith(RouteSearchResult.NoRoute("none"));
        var result = await controller.Search(ValidRequest, CancellationToken.None);
        var objectResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
    }

    [Fact]
    public async Task Search_ProviderUnavailable_Returns503()
    {
        var controller = ControllerWith(RouteSearchResult.Unavailable("offline"));
        var result = await controller.Search(ValidRequest, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
    }

    [Fact]
    public async Task Recheck_ExpiredSearch_Returns410()
    {
        var service = new StubRoutingService(
            RouteSearchResult.Invalid("unused"),
            RouteRecheckResult.Expired("expired"));
        var controller = NewController(service);

        var result = await controller.Recheck(new RouteRecheckRequest(Guid.NewGuid(), "route-1"), CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status410Gone, objectResult.StatusCode);
    }

    private static RoutingApiController ControllerWith(RouteSearchResult result) =>
        NewController(new StubRoutingService(result, RouteRecheckResult.Invalid("unused")));

    private static RoutingApiController NewController(IRoutingService service) => new(service)
    {
        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
    };

    private sealed class StubRoutingService : IRoutingService
    {
        private readonly RouteSearchResult _search;
        private readonly RouteRecheckResult _recheck;

        public StubRoutingService(RouteSearchResult search, RouteRecheckResult recheck)
        {
            _search = search;
            _recheck = recheck;
        }

        public Task<RouteSearchResult> SearchAsync(
            RouteSearchRequest request,
            ulong? userId,
            CancellationToken cancellationToken = default) => Task.FromResult(_search);

        public Task<RouteRecheckResult> RecheckAsync(
            RouteRecheckRequest request,
            ulong? userId,
            CancellationToken cancellationToken = default) => Task.FromResult(_recheck);
    }
}
