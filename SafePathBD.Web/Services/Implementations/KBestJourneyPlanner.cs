using Microsoft.Extensions.Options;
using SafePathBD.Web.Common;
using SafePathBD.Web.Models.DTOs.IntelligentRouting;
using SafePathBD.Web.Services.IntelligentRouting;
using SafePathBD.Web.Services.Interfaces;

namespace SafePathBD.Web.Services.Implementations;

/// <summary>
/// Deterministic bounded K-best planner. In addition to the all-modes search, it deliberately
/// probes each user-selected single mode and the practical Dhaka multimodal mode sets that are
/// actually enabled by the user. This prevents a very fast car path from crowding every other
/// selected vehicle/multimodal option out of the candidate pool before final ranking.
/// </summary>
public sealed class KBestJourneyPlanner : IKBestJourneyPlanner
{
    private readonly ITimeDependentJourneyRouter _router;
    private readonly IntelligentMobilityOptions _options;

    public KBestJourneyPlanner(
        ITimeDependentJourneyRouter router,
        IOptions<IntelligentMobilityOptions> options)
    {
        _router = router;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<JourneyPath>> FindCandidatesAsync(
        MobilityGraph graph,
        IntelligentRouteRequest request,
        IReadOnlySet<int>? globallyBlockedEdgeIds = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<JourneyPath>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        async Task AddAsync(
            IntelligentRouteRequest searchRequest,
            IReadOnlySet<int>? excluded = null)
        {
            if (results.Count >= _options.CandidatePoolSize)
            {
                return;
            }

            var merged = Merge(globallyBlockedEdgeIds, excluded);
            var path = await _router.FindBestAsync(
                graph,
                searchRequest,
                merged,
                singleModeOnly: null,
                cancellationToken: cancellationToken);
            if (path is null)
            {
                return;
            }

            var signature = Signature(path);
            if (seen.Add(signature))
            {
                results.Add(path);
            }
        }

        // 1) User's exact mode set. This remains the primary unconstrained multimodal search.
        await AddAsync(CloneForModes(request, request.EnabledModes));

        // 2) Probe every selected vehicle independently, including BUS. This is critical for
        // presenting meaningful alternatives when the user enabled several modes.
        foreach (var mode in NormalizedModes(request))
        {
            await AddAsync(CloneForModes(request, new[] { mode }, maxTransfers: 0));
        }

        // 3) Probe practical shared/multimodal combinations instead of hoping the one all-mode
        // Dijkstra run happens to discover them. Only combinations fully selected by the user
        // are considered.
        if (request.MaxTransfers > 0)
        {
            foreach (var profile in PracticalProfiles(request))
            {
                await AddAsync(CloneForModes(request, profile));
            }
        }

        if (results.Count == 0)
        {
            return results;
        }

        // 4) Yen-inspired bounded edge suppression produces spatially different road choices
        // within the already-validated user mode constraints.
        var seedPaths = results.Take(Math.Min(8, results.Count)).ToArray();
        foreach (var path in seedPaths)
        {
            var profile = PhysicalModeSequence(path);
            var profileRequest = profile.Count == 0
                ? CloneForModes(request, request.EnabledModes)
                : CloneForModes(request, profile);

            var physicalEdges = path.Steps
                .Where(s => s.Edge is not null)
                .Select(s => s.Edge!.Id)
                .Distinct()
                .Take(10)
                .ToArray();

            foreach (var edgeId in physicalEdges)
            {
                await AddAsync(profileRequest, new HashSet<int> { edgeId });
                if (results.Count >= _options.CandidatePoolSize)
                {
                    break;
                }
            }

            if (results.Count >= _options.CandidatePoolSize)
            {
                break;
            }
        }

        return results
            .OrderBy(p => p.GeneralizedSearchCost)
            .Take(_options.CandidatePoolSize)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizedModes(IntelligentRouteRequest request) =>
        request.EnabledModes
            .Select(m => m.Trim().ToUpperInvariant())
            .Where(MobilityModes.All.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IEnumerable<IReadOnlyList<string>> PracticalProfiles(IntelligentRouteRequest request)
    {
        var enabled = NormalizedModes(request).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var profiles = new[]
        {
            new[] { MobilityModes.Walk, MobilityModes.Bus },
            new[] { MobilityModes.Rickshaw, MobilityModes.Bus },
            new[] { MobilityModes.Walk, MobilityModes.Rickshaw },
            new[] { MobilityModes.Walk, MobilityModes.Rickshaw, MobilityModes.Bus }
        };

        var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var profile in profiles)
        {
            if (!profile.All(enabled.Contains))
            {
                continue;
            }

            var key = string.Join('|', profile.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            if (emitted.Add(key))
            {
                yield return profile;
            }
        }
    }

    private static IReadOnlyList<string> PhysicalModeSequence(JourneyPath path) =>
        path.Steps
            .Where(step => step.Edge is not null)
            .Select(step => step.Mode)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static IntelligentRouteRequest CloneForModes(
        IntelligentRouteRequest request,
        IEnumerable<string> modes,
        int? maxTransfers = null) => new()
    {
        Start = request.Start with { },
        Destination = request.Destination with { },
        DepartureTime = request.DepartureTime,
        EnabledModes = modes
            .Select(m => m.Trim().ToUpperInvariant())
            .Where(MobilityModes.All.Contains)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList(),
        Preference = request.Preference,
        MaxWalkingMeters = request.MaxWalkingMeters,
        MaxTransfers = maxTransfers ?? request.MaxTransfers
    };

    private static IReadOnlySet<int>? Merge(IReadOnlySet<int>? first, IReadOnlySet<int>? second)
    {
        if ((first is null || first.Count == 0) && (second is null || second.Count == 0))
        {
            return null;
        }

        var result = new HashSet<int>();
        if (first is not null)
        {
            result.UnionWith(first);
        }
        if (second is not null)
        {
            result.UnionWith(second);
        }
        return result;
    }

    private static string Signature(JourneyPath path) => string.Join(
        '|',
        path.Steps.Select(step => step.Edge is null
            ? $"T:{step.Mode}:{step.BusRouteId}:{step.TransferNote}"
            : $"E:{step.Edge.Id}:{step.Mode}:{step.BusRouteId}"));
}
