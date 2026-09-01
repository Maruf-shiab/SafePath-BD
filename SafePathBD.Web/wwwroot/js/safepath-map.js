/* SafePath BD — shared map primitives.
   Owns tile setup, marker iconography and JSON access so the map workspace and the
   report location picker cannot drift apart. */
(function (global) {
    "use strict";

    var reduceMotion = global.matchMedia("(prefers-reduced-motion: reduce)");

    var SERVICE_STYLES = {
        "Hospital": { color: "var(--svc-hospital)", icon: '<path d="M12 7v10M7 12h10" /><rect x="3.5" y="3.5" width="17" height="17" rx="4" />' },
        "Police Station": { color: "var(--svc-police)", icon: '<path d="M12 3 5 6v6c0 4.2 2.9 7.8 7 9 4.1-1.2 7-4.8 7-9V6l-7-3Z" />' },
        "Fire Service": { color: "var(--svc-fire)", icon: '<path d="M12 3s5 4.2 5 8.7A5 5 0 0 1 7 12c0-1.6.7-2.9 1.6-4 .2 1.3 1 2 1.9 2 0-3.3 1.5-5.5 1.5-7Z" />' },
        "Ambulance": { color: "var(--svc-ambulance)", icon: '<path d="M3 15V8h11v7M14 10h3.5L21 13v2h-3" /><circle cx="7" cy="17" r="1.8" /><circle cx="17" cy="17" r="1.8" /><path d="M7.5 11.5h3M9 10v3" />' },
        "Emergency Center": { color: "var(--svc-center)", icon: '<path d="M12 3v3M12 18v3M3 12h3M18 12h3" /><circle cx="12" cy="12" r="4.5" />' }
    };

    var DEFAULT_SERVICE_STYLE = { color: "var(--accent-primary)", icon: '<circle cx="12" cy="12" r="5" />' };

    // Accident severity and hazard risk drive the marker accent, so colour always carries meaning.
    var SEVERITY_COLORS = {
        "Minor": "var(--caution)",
        "Moderate": "var(--warning)",
        "Severe": "var(--danger)",
        "Fatal": "var(--report-fatal)"
    };

    var RISK_COLORS = {
        "LOW": "var(--safe)",
        "MODERATE": "var(--caution)",
        "HIGH": "var(--warning)",
        "CRITICAL": "var(--danger)"
    };

    var ACCIDENT_GLYPH = '<path d="M4 16.5V13l1.8-4.2A2 2 0 0 1 7.7 7.5h8.6a2 2 0 0 1 1.9 1.3L20 13v3.5" />' +
        '<path d="M4 16.5h16M6.8 16.5v2M17.2 16.5v2M7.5 12.7h9" />';

    var HAZARD_GLYPH = '<path d="M12 9.2v4M12 16.4h.01" />' +
        '<path d="M10.3 4.4 2.6 17.6a2 2 0 0 0 1.7 3h15.4a2 2 0 0 0 1.7-3L13.7 4.4a2 2 0 0 0-3.4 0Z" />';

    function svg(paths, size) {
        return '<svg viewBox="0 0 24 24" width="' + (size || 18) + '" height="' + (size || 18) +
            '" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">' +
            paths + "</svg>";
    }

    function escapeHtml(value) {
        return String(value === null || value === undefined ? "" : value)
            .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
    }

    function formatCoords(lat, lng) {
        return lat.toFixed(5) + ", " + lng.toFixed(5);
    }

    function formatDistance(km) {
        if (km === null || km === undefined) {
            return "";
        }
        return km < 1 ? Math.round(km * 1000) + " m" : km.toFixed(1) + " km";
    }

    async function getJson(url) {
        var response = await fetch(url, { headers: { Accept: "application/json" } });
        var payload = null;

        try {
            payload = await response.json();
        } catch (e) {
            payload = null;
        }

        if (!response.ok || !payload || payload.success === false) {
            var error = new Error((payload && payload.message) || "The request could not be completed.");
            error.status = response.status;
            throw error;
        }

        return payload.data;
    }

    function createMap(element, options) {
        var map = L.map(element, {
            center: [options.lat, options.lng],
            zoom: options.zoom,
            zoomControl: false,
            attributionControl: true,
            preferCanvas: true
        });

        L.tileLayer("https://tile.openstreetmap.org/{z}/{x}/{y}.png", {
            maxZoom: 19,
            minZoom: 4,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        }).addTo(map);

        L.control.zoom({ position: options.zoomPosition || "bottomright" }).addTo(map);
        return map;
    }

    function pinIcon(kind, paths, colorVar) {
        return L.divIcon({
            className: "sp-marker sp-marker--enter",
            html: '<span class="sp-pin sp-pin--' + kind + '"' + (colorVar ? ' style="--pin-color: ' + colorVar + '"' : "") +
                ">" + svg(paths, 17) + "</span>",
            iconSize: [34, 34],
            iconAnchor: [17, 32],
            popupAnchor: [0, -30]
        });
    }

    function locateIcon() {
        return L.divIcon({
            className: "sp-marker",
            html: '<span class="sp-locate"><span class="sp-locate-ring"></span><span class="sp-locate-core"></span></span>',
            iconSize: [22, 22],
            iconAnchor: [11, 11]
        });
    }

    /* Report markers use a round badge rather than a teardrop so they read differently
       from emergency facilities at a glance. */
    function reportIcon(reportType, accentVar) {
        var isAccident = reportType === "ACCIDENT";
        return L.divIcon({
            className: "sp-marker sp-marker--enter",
            html: '<span class="sp-report sp-report--' + (isAccident ? "accident" : "hazard") + '"' +
                ' style="--report-color: ' + (accentVar || "var(--caution)") + '">' +
                svg(isAccident ? ACCIDENT_GLYPH : HAZARD_GLYPH, 17) + "</span>",
            iconSize: [32, 32],
            iconAnchor: [16, 16],
            popupAnchor: [0, -18]
        });
    }

    function reportAccent(report) {
        if (report.reportType === "ACCIDENT") {
            return SEVERITY_COLORS[report.severityName] || "var(--warning)";
        }
        return RISK_COLORS[report.riskLevel] || "var(--caution)";
    }

    function flyTo(map, lat, lng, zoom) {
        if (reduceMotion.matches) {
            map.setView([lat, lng], zoom || map.getZoom());
            return;
        }
        map.flyTo([lat, lng], zoom || map.getZoom(), { duration: 0.85 });
    }

    global.SafePathMap = {
        SERVICE_STYLES: SERVICE_STYLES,
        DEFAULT_SERVICE_STYLE: DEFAULT_SERVICE_STYLE,
        SEVERITY_COLORS: SEVERITY_COLORS,
        RISK_COLORS: RISK_COLORS,
        ACCIDENT_GLYPH: ACCIDENT_GLYPH,
        HAZARD_GLYPH: HAZARD_GLYPH,
        svg: svg,
        escapeHtml: escapeHtml,
        formatCoords: formatCoords,
        formatDistance: formatDistance,
        getJson: getJson,
        createMap: createMap,
        pinIcon: pinIcon,
        locateIcon: locateIcon,
        reportIcon: reportIcon,
        reportAccent: reportAccent,
        flyTo: flyTo,
        serviceStyle: function (name) { return SERVICE_STYLES[name] || DEFAULT_SERVICE_STYLE; },
        prefersReducedMotion: function () { return reduceMotion.matches; }
    };
})(window);
