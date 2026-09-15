/* SafePath BD — routing workspace.
   Owns request-scoped OSRM route presentation and incident-aware rechecks while
   map.js remains the single owner of Leaflet endpoint selection and shared drawer. */
(function () {
    "use strict";

    var workspace = window.SafePathMapWorkspace;
    var SPM = window.SafePathMap;
    var root = document.querySelector("[data-map-root]");
    var findButton = root && root.querySelector("[data-find-routes]");

    if (!workspace || !SPM || !root || !findButton || typeof L === "undefined") {
        return;
    }

    var map = workspace.getMap();
    var reduceMotion = workspace.prefersReducedMotion();
    var toast = window.SafePathToast || { info: function () {}, warning: function () {}, error: function () {}, success: function () {} };
    var routeLayer = L.layerGroup().addTo(map);
    var routeIncidentLayer = L.layerGroup().addTo(map);

    var state = {
        response: null,
        selectedKey: null,
        polylines: {},
        recheckTimer: null,
        requestController: null,
        busy: false,
        dismissedAlert: false,
        mobileExpanded: true
    };

    function svg(paths, size) { return SPM.svg(paths, size || 16); }
    function esc(value) { return SPM.escapeHtml(value); }
    function cssColor(token, fallback) {
        var value = window.getComputedStyle(document.documentElement).getPropertyValue(token).trim();
        return value || fallback;
    }

    function validEndpoints() {
        return Boolean(workspace.getStart() && workspace.getDestination());
    }

    function updateFindButton() {
        var enabled = validEndpoints() && !state.busy;
        findButton.disabled = !enabled;
        findButton.classList.toggle("is-disabled", !enabled);
        findButton.title = validEndpoints() ? "Find road routes" : "Choose a start and destination first";
    }

    function setBusy(busy) {
        state.busy = busy;
        updateFindButton();
        var label = findButton.querySelector("[data-find-routes-label]");
        if (label) {
            label.textContent = busy ? "Finding routes…" : "Find routes";
        }
        findButton.setAttribute("aria-busy", String(busy));
    }

    function clearRecheckTimer() {
        if (state.recheckTimer) {
            window.clearInterval(state.recheckTimer);
            state.recheckTimer = null;
        }
    }

    function clearRouteResults(options) {
        var opts = options || {};
        clearRecheckTimer();
        routeLayer.clearLayers();
        routeIncidentLayer.clearLayers();
        state.polylines = {};
        state.response = null;
        state.selectedKey = null;
        state.dismissedAlert = false;
        state.mobileExpanded = true;
        if (state.requestController) {
            state.requestController.abort();
            state.requestController = null;
        }
        if (!opts.keepDrawer) {
            clearVisibleRouteDrawer();
        }
    }

    function clearVisibleRouteDrawer() {
        var title = root.querySelector("[data-drawer-title]");
        if (!title || title.textContent !== "Route alternatives") { return; }

        var hasBoth = validEndpoints();
        workspace.renderDrawer(
            "Route alternatives",
            hasBoth ? "Route inputs changed" : "Choose two locations",
            '<div class="empty-state"><span class="card-icon" aria-hidden="true">' +
            svg('<path d="M4 18c4-8 7-8 10-2s5 5 7-4" /><circle cx="4" cy="18" r="1.8" /><circle cx="21" cy="12" r="1.8" />', 20) +
            '</span><h3 class="t-h3">' + (hasBoth ? 'Find routes for these points' : 'Choose a start and destination') + '</h3>' +
            '<p class="t-body">' + (hasBoth ? 'The previous route was cleared because an endpoint changed.' : 'Set both points to compare road routes.') + '</p></div>');
    }

    function antiforgeryToken() {
        return root.querySelector('[data-routing-antiforgery] input[name="__RequestVerificationToken"]')?.value || "";
    }

    async function postJson(url, body, signal) {
        var headers = { "Content-Type": "application/json", "Accept": "application/json" };
        var token = antiforgeryToken();
        if (token) { headers["X-CSRF-TOKEN"] = token; }

        var response = await fetch(url, {
            method: "POST",
            headers: headers,
            body: JSON.stringify(body),
            signal: signal
        });

        var payload = null;
        try { payload = await response.json(); } catch (e) { payload = null; }

        if (!response.ok || !payload || payload.success === false) {
            var error = new Error((payload && payload.message) || defaultErrorMessage(response.status));
            error.status = response.status;
            throw error;
        }
        return payload.data;
    }

    function defaultErrorMessage(status) {
        if (status === 400) { return "Choose valid start and destination locations."; }
        if (status === 410) { return "This route search expired. Find routes again."; }
        if (status === 422) { return "No drivable route was found between these locations."; }
        if (status === 503) { return "The routing provider is temporarily unavailable."; }
        return "The route request could not be completed.";
    }

    function renderLoading() {
        var html = '<div class="route-loading" role="status" aria-live="polite">' +
            '<span class="route-loading-mark" aria-hidden="true">' +
            svg('<path d="M3 17c4-8 7-8 10-2s5 5 8-5" /><circle cx="4" cy="17" r="1.8" /><circle cx="21" cy="10" r="1.8" />', 24) +
            '</span><div><strong>Finding road routes…</strong>' +
            '<span>Distance, estimated time, and verified incident checks will appear together.</span></div></div>' +
            '<div class="route-skeleton-list">' +
            '<span class="route-skeleton"></span><span class="route-skeleton"></span><span class="route-skeleton"></span></div>';
        workspace.renderDrawer("Route alternatives", "Planning with OSRM road data", html);
    }

    async function searchRoutes() {
        if (!validEndpoints() || state.busy) { return; }

        window.dispatchEvent(new CustomEvent("safepath:road-route-search-start"));
        clearRouteResults({ keepDrawer: true });
        setBusy(true);
        renderLoading();

        var start = workspace.getStart();
        var destination = workspace.getDestination();
        state.requestController = new AbortController();

        try {
            var response = await postJson("/api/v1/routes/search", {
                start: { latitude: start.lat, longitude: start.lng, label: start.label || null },
                destination: { latitude: destination.lat, longitude: destination.lng, label: destination.label || null }
            }, state.requestController.signal);

            state.response = response;
            state.selectedKey = response.recommendedCandidateKey || response.shortestCandidateKey ||
                (response.candidates[0] && response.candidates[0].candidateKey);
            state.dismissedAlert = false;
            state.mobileExpanded = !window.matchMedia("(max-width: 900px)").matches;

            drawCandidates();
            renderRouteDrawer();
            fitAllCandidates();
            startRecheckTimer();
        } catch (error) {
            if (error.name === "AbortError") { return; }
            renderSearchError(error);
        } finally {
            state.requestController = null;
            setBusy(false);
        }
    }

    function renderSearchError(error) {
        var title = error.status === 503 ? "Routing is temporarily unavailable"
            : error.status === 422 ? "No drivable route found"
            : error.status === 400 ? "Check the selected locations"
            : "Couldn't find routes";

        var html = '<div class="route-error-state">' +
            '<span class="card-icon card-icon--danger" aria-hidden="true">' +
            svg('<path d="M12 9v4M12 17h.01" /><circle cx="12" cy="12" r="9" />', 20) + '</span>' +
            '<h3 class="t-h3">' + esc(title) + '</h3>' +
            '<p class="t-body">' + esc(error.message) + '</p>' +
            '<button class="btn btn-secondary" type="button" data-route-retry>Try again</button></div>';

        var body = workspace.renderDrawer("Route alternatives", "Start and destination were kept", html);
        body.querySelector("[data-route-retry]").addEventListener("click", searchRoutes);
    }

    function routeStyle(candidate, selected) {
        var affected = candidate.incidentState === "AFFECTED";
        return {
            color: selected ? cssColor("--accent-primary", "#2fd3b4")
                : (affected ? cssColor("--danger", "#f0656a") : cssColor("--text-muted", "#8493a8")),
            weight: selected ? 7 : 4,
            opacity: selected ? 0.96 : 0.55,
            dashArray: affected && !selected ? "9 8" : null,
            lineCap: "round",
            lineJoin: "round",
            className: "sp-route-line" + (selected ? " is-selected" : "")
        };
    }

    function drawCandidates() {
        routeLayer.clearLayers();
        state.polylines = {};

        state.response.candidates.forEach(function (candidate) {
            var latLngs = candidate.geometry.map(function (p) { return [p.latitude, p.longitude]; });
            var selected = candidate.candidateKey === state.selectedKey;
            var line = L.polyline(latLngs, routeStyle(candidate, selected)).addTo(routeLayer);
            line.on("click", function () { selectCandidate(candidate.candidateKey, true); });
            line.bindTooltip(routeName(candidate), { sticky: true, direction: "top", className: "route-tooltip" });
            state.polylines[candidate.candidateKey] = line;
        });

        drawSelectedIncidentMarkers();
    }

    function refreshPolylineStyles() {
        state.response.candidates.forEach(function (candidate) {
            var line = state.polylines[candidate.candidateKey];
            if (line) {
                line.setStyle(routeStyle(candidate, candidate.candidateKey === state.selectedKey));
                if (candidate.candidateKey === state.selectedKey) { line.bringToFront(); }
            }
        });
    }

    function routeName(candidate) {
        var index = state.response.candidates.indexOf(candidate) + 1;
        return candidate.isDetour ? "Detour alternative" : "Route " + index;
    }

    function fitAllCandidates() {
        var all = [];
        state.response.candidates.forEach(function (candidate) {
            all = all.concat(candidate.geometry);
        });
        workspace.fitCoordinates(all, [72, 72]);
    }

    function focusSelected() {
        var candidate = getSelected();
        if (candidate) {
            workspace.fitCoordinates(candidate.geometry, [84, 84]);
        }
    }

    function getSelected() {
        if (!state.response) { return null; }
        return state.response.candidates.filter(function (c) { return c.candidateKey === state.selectedKey; })[0] || null;
    }

    function selectCandidate(key, focus) {
        if (!state.response || state.selectedKey === key && !focus) { return; }
        state.selectedKey = key;
        state.dismissedAlert = false;
        refreshPolylineStyles();
        drawSelectedIncidentMarkers();
        renderRouteDrawer();
        if (focus) { focusSelected(); }
        startRecheckTimer();
    }

    function drawSelectedIncidentMarkers() {
        routeIncidentLayer.clearLayers();
        var candidate = getSelected();
        if (!candidate || !candidate.incidents) { return; }

        candidate.incidents.forEach(function (incident) {
            var accent = incident.incidentLevel === "AFFECTED" ? "var(--danger)" : "var(--caution)";
            var marker = L.marker([incident.latitude, incident.longitude], {
                icon: SPM.reportIcon(incident.reportType, accent),
                title: incident.title,
                riseOnHover: true
            }).addTo(routeIncidentLayer);

            marker.bindPopup('<div class="sp-popup"><strong>' + esc(incident.title) + '</strong>' +
                '<span>' + esc(incidentLabel(incident)) + '</span></div>');
        });
    }

    function stateIcon(status) {
        if (status === "AFFECTED") {
            return svg('<path d="M12 9v4M12 17h.01" /><path d="M10.3 4.4 2.6 17.6a2 2 0 0 0 1.7 3h15.4a2 2 0 0 0 1.7-3L13.7 4.4a2 2 0 0 0-3.4 0Z" />', 15);
        }
        if (status === "CAUTION") {
            return svg('<path d="M12 8v5M12 16.5h.01" /><circle cx="12" cy="12" r="9" />', 15);
        }
        if (status === "CLEAR") {
            return svg('<path d="m5 12 4 4L19 6" />', 15);
        }
        return svg('<circle cx="12" cy="12" r="9" /><path d="M9.8 9a2.4 2.4 0 1 1 3.7 2c-1 .7-1.5 1.1-1.5 2M12 17h.01" />', 15);
    }

    function statusText(candidate) {
        if (!candidate.incidentCheckAvailable || candidate.incidentState === "UNKNOWN") {
            return "Incident information unavailable";
        }
        if (candidate.incidentState === "AFFECTED") {
            var seriousCount = (candidate.incidents || []).filter(function (incident) { return incident.incidentLevel === "AFFECTED"; }).length;
            return seriousCount + " verified serious incident" + (seriousCount === 1 ? "" : "s") + " detected near route";
        }
        if (candidate.incidentState === "CAUTION") {
            var cautionCount = (candidate.incidents || []).length;
            return cautionCount + " verified caution-level incident" + (cautionCount === 1 ? "" : "s") + " detected near route";
        }
        return "No relevant verified incident currently detected near route";
    }

    function renderRouteDrawer() {
        if (!state.response) { return; }
        var selected = getSelected();
        var html = '<div class="route-results' + (state.mobileExpanded ? '' : ' is-mobile-collapsed') + '" data-route-results>' +
            renderMobileSummary(selected) + '<div class="route-expanded-content">' +
            renderIncidentAvailability() + renderDynamicAlert(selected) + renderRecommendation() +
            '<div class="route-cards">' + state.response.candidates.map(renderCandidateCard).join("") + '</div>' +
            '<div class="route-result-foot"><span>Predicted ETA uses the supplied SafePath synthetic traffic patterns where route coverage is available; it is not live traffic.</span>' +
            '<span>Incident guidance uses current public VERIFIED SafePath reports only. Recent accidents affect route avoidance for up to 2 hours.</span></div></div></div>';

        var subtitle = state.response.candidates.length + " road route" + (state.response.candidates.length === 1 ? "" : "s") +
            " · checked " + formatTime(state.response.incidentCheckedAt);
        var body = workspace.renderDrawer("Route alternatives", subtitle, html);
        wireRouteDrawer(body);
    }

    function renderMobileSummary(selected) {
        if (!selected) { return ""; }
        var badges = [];
        if (selected.isShortest) { badges.push("Shortest"); }
        if (selected.isFastest) { badges.push(state.response.trafficAwareRecommendation ? "Fastest predicted" : "Fastest"); }
        if (selected.isRecommendedAlternative) { badges.push("Recommended"); }
        var badgeText = badges.length ? badges.join(" · ") : "Selected route";
        return '<button class="route-mobile-summary" type="button" data-route-mobile-toggle aria-expanded="' + String(state.mobileExpanded) + '">' +
            '<span><small>' + esc(badgeText) + '</small><strong>' + esc(routeName(selected)) + '</strong></span>' +
            '<span class="route-mobile-metrics"><strong>' + formatKm(selected.distanceKm) + '</strong><strong>' + formatMinutes(routeEtaMinutes(selected)) + '</strong></span>' +
            '<span class="route-mobile-state">' + stateIcon(selected.incidentState) + '<span>' + esc(selected.incidentState === "UNKNOWN" ? "Status unavailable" : selected.incidentState.charAt(0) + selected.incidentState.slice(1).toLowerCase()) + '</span></span>' +
            '<span class="route-mobile-chevron" aria-hidden="true">' + svg('<path d="m7 10 5 5 5-5" />', 16) + '</span></button>';
    }

    function renderIncidentAvailability() {
        if (!state.response || state.response.incidentCheckAvailable) { return ""; }
        return '<div class="route-info-warning" role="status">' +
            '<span aria-hidden="true">' + stateIcon("UNKNOWN") + '</span><span>' +
            esc(state.response.incidentMessage || "Incident information is temporarily unavailable.") + '</span></div>';
    }

    function renderDynamicAlert(selected) {
        if (!selected || state.dismissedAlert || selected.incidentState !== "AFFECTED") { return '<div class="route-live-region" aria-live="assertive"></div>'; }
        return '<div class="route-change-alert" role="alert" aria-live="assertive">' +
            '<span aria-hidden="true">' + stateIcon("AFFECTED") + '</span><div><strong>This selected route is affected.</strong>' +
            '<p>A verified serious road incident is currently detected near this route.</p>' +
            '<div class="route-alert-actions"><button class="btn btn-secondary btn-sm" type="button" data-find-alternative>Find alternative</button>' +
            ('geolocation' in navigator ? '<button class="btn btn-ghost btn-sm" type="button" data-reroute-current>Re-route from my current location</button>' : '') +
            '<button class="btn btn-ghost btn-sm" type="button" data-keep-route>Keep current route</button></div></div></div>';
    }

    function renderRecommendation() {
        var recommended = state.response.candidates.filter(function (candidate) {
            return candidate.candidateKey === state.response.recommendedCandidateKey;
        })[0];
        var shortest = state.response.candidates.filter(function (candidate) { return candidate.isShortest; })[0];

        if (!recommended || !shortest || recommended.candidateKey === shortest.candidateKey) {
            if (state.response.detourAttempted && state.response.detourMessage) {
                return '<div class="route-recommendation route-recommendation--warning"><span class="t-eyebrow">Alternative check</span>' +
                    '<p>' + esc(state.response.detourMessage) + '</p></div>';
            }
            return "";
        }

        var distance = signed(state.response.recommendedDistanceDeltaKm, " km");
        var minutes = signed(state.response.recommendedDurationDeltaMinutes, " min");
        return '<section class="route-recommendation" aria-label="Recommended alternative">' +
            '<span class="t-eyebrow">Recommended alternative</span><div class="route-recommendation-main">' +
            '<strong>' + esc(routeName(recommended)) + '</strong><span>' + formatKm(recommended.distanceKm) + ' · ' + formatMinutes(routeEtaMinutes(recommended)) + '</span></div>' +
            '<div class="route-tradeoff"><span>' + esc(distance) + ' vs shortest</span><span>' + esc(minutes) + ' vs shortest</span></div>' +
            '<p>' + esc(state.response.recommendationReason || "Shortest suitable alternative based on current verified incident information.") + '</p></section>';
    }

    function renderCandidateCard(candidate, index) {
        var selected = candidate.candidateKey === state.selectedKey;
        var labels = [];
        if (candidate.isShortest) { labels.push('<span class="route-badge">Shortest</span>'); }
        if (candidate.isFastest) { labels.push('<span class="route-badge">' + (state.response.trafficAwareRecommendation ? 'Fastest predicted' : 'Fastest') + '</span>'); }
        if (candidate.isRecommendedAlternative) { labels.push('<span class="route-badge route-badge--recommended">Recommended alternative</span>'); }
        if (candidate.isDetour) { labels.push('<span class="route-badge route-badge--detour">Detour</span>'); }

        return '<article class="route-card ' + (selected ? 'is-selected ' : '') + 'route-card--' + candidate.incidentState.toLowerCase() + '">' +
            '<button class="route-card-select" type="button" data-route-select="' + esc(candidate.candidateKey) + '" aria-pressed="' + String(selected) + '">' +
            '<span class="route-card-top"><span><span class="t-eyebrow">' + esc(routeName(candidate)) + '</span>' +
            '<span class="route-badges">' + labels.join("") + '</span></span>' +
            '<span class="route-select-mark" aria-hidden="true">' + svg('<circle cx="12" cy="12" r="9" /><path d="m8.5 12 2.2 2.2 4.8-5" />', 18) + '</span></span>' +
            '<span class="route-metrics"><span><strong>' + formatKm(candidate.distanceKm) + '</strong><small>Distance</small></span>' +
            '<span><strong>' + formatMinutes(routeEtaMinutes(candidate)) + '</strong><small>' + (candidate.trafficEstimateAvailable ? 'Predicted ETA' : 'Provider ETA') + '</small></span></span>' +
            renderTrafficState(candidate) +
            '<span class="route-incident-state"><span aria-hidden="true">' + stateIcon(candidate.incidentState) + '</span><span>' + esc(statusText(candidate)) + '</span></span></button>' +
            renderIncidentDetails(candidate) + '</article>';
    }


    function routeEtaMinutes(candidate) {
        return candidate && candidate.trafficEstimateAvailable && candidate.trafficAdjustedDurationMinutes != null
            ? Number(candidate.trafficAdjustedDurationMinutes)
            : Number(candidate.durationMinutes);
    }

    function renderTrafficState(candidate) {
        if (!candidate.trafficEstimateAvailable || candidate.predictedCongestionIndex == null) {
            return '<span class="route-traffic-state route-traffic-state--unknown"><strong>Traffic estimate unavailable</strong><span>Using routing-provider ETA</span></span>';
        }

        var level = String(candidate.trafficLevel || 'UNKNOWN').toUpperCase();
        var confidence = candidate.trafficDataConfidence ? ' · ' + candidate.trafficDataConfidence + ' data' : '';
        return '<span class="route-traffic-state route-traffic-state--' + esc(level.toLowerCase()) + '">' +
            '<strong>Predicted traffic: ' + esc(level) + '</strong>' +
            '<span>' + Math.round(Number(candidate.predictedCongestionIndex)) + '/100 congestion' + esc(confidence) + '</span></span>';
    }

    function renderIncidentDetails(candidate) {
        if (!candidate.incidents || !candidate.incidents.length) { return ""; }
        var rows = candidate.incidents.map(function (incident) {
            var place = incident.locationLabel ? '<span>' + esc(incident.locationLabel) + '</span>' : '';
            return '<li><div><strong>' + esc(incident.title) + '</strong><span>' + esc(incidentLabel(incident)) + '</span>' + place +
                '<span>' + Math.round(incident.distanceFromRouteMeters) + ' m from route · reported ' + esc(formatDate(incident.reportedAt)) + '</span></div>' +
                '<a href="/Reports/Details/' + encodeURIComponent(incident.reportId) + '">View verified report</a></li>';
        }).join("");

        return '<details class="route-incidents"><summary>Why is this route ' + (candidate.incidentState === "AFFECTED" ? 'affected' : 'under caution') + '?</summary>' +
            '<ul>' + rows + '</ul></details>';
    }

    function incidentLabel(incident) {
        if (incident.reportType === "ACCIDENT") {
            return [incident.accidentType || "Accident", incident.severityName].filter(Boolean).join(" · ");
        }
        return [incident.hazardType || "Hazard", incident.riskLevel].filter(Boolean).join(" · ");
    }

    function wireRouteDrawer(body) {
        var mobileToggle = body.querySelector("[data-route-mobile-toggle]");
        if (mobileToggle) {
            mobileToggle.addEventListener("click", function () {
                state.mobileExpanded = !state.mobileExpanded;
                renderRouteDrawer();
            });
        }

        body.querySelectorAll("[data-route-select]").forEach(function (button) {
            button.addEventListener("click", function () { selectCandidate(button.getAttribute("data-route-select"), true); });
        });

        var alternative = body.querySelector("[data-find-alternative]");
        if (alternative) { alternative.addEventListener("click", searchRoutes); }

        var keep = body.querySelector("[data-keep-route]");
        if (keep) {
            keep.addEventListener("click", function () {
                state.dismissedAlert = true;
                renderRouteDrawer();
            });
        }

        var current = body.querySelector("[data-reroute-current]");
        if (current) {
            current.addEventListener("click", async function () {
                current.disabled = true;
                current.setAttribute("aria-busy", "true");
                try {
                    await workspace.requestCurrentLocation(true, true);
                    await searchRoutes();
                } catch (error) {
                    toast.warning("Your current location could not be used for rerouting.");
                } finally {
                    current.disabled = false;
                    current.removeAttribute("aria-busy");
                }
            });
        }
    }

    function startRecheckTimer() {
        clearRecheckTimer();
        if (!state.response || !state.selectedKey) { return; }
        var seconds = Math.max(30, Number(state.response.incidentRecheckSeconds) || 60);
        state.recheckTimer = window.setInterval(function () {
            if (!document.hidden) { recheckSelected(); }
        }, seconds * 1000);
    }

    async function recheckSelected() {
        if (!state.response || !state.selectedKey || state.busy) { return; }
        var selectedKeyAtRequest = state.selectedKey;
        try {
            var result = await postJson("/api/v1/routes/recheck", {
                searchId: state.response.searchId,
                selectedCandidateKey: selectedKeyAtRequest
            });

            if (!state.response || state.selectedKey !== selectedKeyAtRequest) { return; }
            var candidate = getSelected();
            if (!candidate) { return; }

            if (!result.incidentCheckAvailable) {
                toast.warning(result.message || "Could not refresh incident information.");
                return;
            }

            candidate.incidentState = result.incidentState;
            candidate.incidents = result.incidents || [];
            candidate.incidentCheckAvailable = true;
            state.response.incidentCheckedAt = result.incidentCheckedAt;
            state.response.hasAffectedRoute = state.response.candidates.some(function (c) { return c.incidentState === "AFFECTED"; });

            if (result.incidentChanged) {
                state.dismissedAlert = false;
                if (result.rerouteRecommended) { state.mobileExpanded = true; }
                refreshPolylineStyles();
                drawSelectedIncidentMarkers();
                renderRouteDrawer();

                if (result.rerouteRecommended) {
                    toast.warning(result.message || "A newly verified road incident now affects this route.");
                } else {
                    toast.info("Verified incident information changed for the selected route.");
                }
            }
        } catch (error) {
            if (error.status === 410) {
                clearRecheckTimer();
                toast.info("This route check expired. Find routes again for fresh incident monitoring.");
            } else {
                toast.warning("Could not refresh incident information.");
            }
        }
    }

    function signed(value, suffix) {
        var number = Number(value || 0);
        var rounded = Math.abs(number) < 0.05 ? 0 : number;
        return (rounded > 0 ? "+" : "") + rounded.toFixed(suffix === " km" ? 2 : 1) + suffix;
    }

    function formatKm(value) { return Number(value).toFixed(1) + " km"; }
    function formatMinutes(value) { return Math.max(1, Math.round(Number(value))) + " min"; }
    function formatTime(value) { return new Date(value).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }); }
    function formatDate(value) { return new Date(value).toLocaleString([], { day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }); }

    findButton.addEventListener("click", searchRoutes);

    workspace.onEndpointsChanged(function () {
        clearRouteResults({ keepDrawer: false });
        updateFindButton();
    });

    window.addEventListener("safepath:intelligent-search-start", function () {
        clearRouteResults({ keepDrawer: false });
    });
    window.addEventListener("beforeunload", clearRecheckTimer);
    updateFindButton();
})();
