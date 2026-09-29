/* SafePath BD — authenticated saved places (Home, Work and custom bookmarks).
   Saved places reuse the existing locations + saved_places schema and feed the
   same map endpoint state used by road routing and Smart Journey. */
(function () {
    "use strict";

    var root = document.querySelector("[data-map-root]");
    var workspace = window.SafePathMapWorkspace;
    var SPM = window.SafePathMap;
    if (!root || !workspace || !SPM) return;

    var toast = window.SafePathToast || { success: function(){}, warning: function(){}, error: function(){}, info: function(){} };
    var authenticated = root.getAttribute("data-user-authenticated") === "true";
    var loginUrl = root.getAttribute("data-map-login-url") || "/Account/Login?returnUrl=%2FMap";
    var list = root.querySelector("[data-saved-place-list]");
    var status = root.querySelector("[data-saved-place-status]");
    var dialog = root.querySelector("[data-saved-place-dialog]");
    var form = root.querySelector("[data-saved-place-form]");
    var typeSelect = root.querySelector("[data-saved-place-type]");
    var nameInput = root.querySelector("[data-saved-place-name]");
    var preview = root.querySelector("[data-saved-place-preview]");
    var submit = root.querySelector("[data-saved-place-submit]");
    var pendingPoint = null;

    function esc(value) { return SPM.escapeHtml(value == null ? "" : String(value)); }
    function csrf() {
        return root.querySelector('[data-routing-antiforgery] input[name="__RequestVerificationToken"]')?.value || "";
    }
    function icon(type) {
        if (type === "HOME") return '<path d="M3 11.5 12 4l9 7.5"/><path d="M5.5 10.5V20h13v-9.5"/><path d="M9.5 20v-6h5v6"/>';
        if (type === "OFFICE") return '<rect x="5" y="4" width="14" height="16" rx="2"/><path d="M9 4V2h6v2M8 9h.01M12 9h.01M16 9h.01M8 13h.01M12 13h.01M16 13h.01"/>';
        if (type === "UNIVERSITY") return '<path d="m3 9 9-5 9 5-9 5-9-5Z"/><path d="M7 12v4c3 2 7 2 10 0v-4M21 9v6"/>';
        return '<path d="M6 3h12v18l-6-4-6 4V3Z"/>';
    }
    function svg(paths) { return SPM.svg(paths, 17); }

    async function request(url, options) {
        var opts = options || {};
        var headers = Object.assign({ "Accept": "application/json" }, opts.headers || {});
        if (opts.method && opts.method !== "GET") {
            headers["X-CSRF-TOKEN"] = csrf();
        }
        var response = await fetch(url, Object.assign({}, opts, { headers: headers }));
        var payload = null;
        try { payload = await response.json(); } catch (_) { }
        if (!response.ok || !payload || payload.success === false) {
            var error = new Error(payload?.message || (response.status === 401 ? "Sign in to use saved places." : "Saved places could not be updated."));
            error.status = response.status;
            throw error;
        }
        return payload.data;
    }

    function render(items) {
        if (!list) return;
        if (!items || !items.length) {
            list.innerHTML = '<div class="saved-place-empty"><span>No saved places yet.</span><small>Choose a start, destination, or map point and tap the bookmark button.</small></div>';
            if (status) status.textContent = "0 saved";
            return;
        }

        if (status) status.textContent = items.length + (items.length === 1 ? " saved" : " saved");
        list.innerHTML = items.map(function (item) {
            var secondary = item.addressLine || [item.areaName, item.city].filter(Boolean).join(", ") || (item.latitude.toFixed(5) + ", " + item.longitude.toFixed(5));
            return '<article class="saved-place-item" data-saved-place-id="' + item.savedPlaceId + '">' +
                '<span class="saved-place-icon" aria-hidden="true">' + svg(icon(item.placeType)) + '</span>' +
                '<span class="saved-place-copy"><strong>' + esc(item.placeName) + '</strong><small>' + esc(item.typeLabel + " · " + secondary) + '</small></span>' +
                '<span class="saved-place-actions">' +
                '<button type="button" data-saved-use="start" title="Use ' + esc(item.placeName) + ' as start">From</button>' +
                '<button type="button" data-saved-use="destination" title="Use ' + esc(item.placeName) + ' as destination">To</button>' +
                '<button type="button" class="saved-place-delete" data-saved-delete title="Remove ' + esc(item.placeName) + '" aria-label="Remove ' + esc(item.placeName) + '">' + svg('<path d="M5 7h14M9 7V4h6v3M8 10v8M12 10v8M16 10v8M7 7l1 14h8l1-14"/>') + '</button>' +
                '</span></article>';
        }).join("");

        list.querySelectorAll("[data-saved-use]").forEach(function (button) {
            button.addEventListener("click", function () {
                var card = button.closest("[data-saved-place-id]");
                var item = items.find(function (x) { return String(x.savedPlaceId) === card.getAttribute("data-saved-place-id"); });
                if (!item) return;
                var role = button.getAttribute("data-saved-use") === "start" ? "start" : "end";
                workspace.setEndpoint(role, {
                    lat: item.latitude,
                    lng: item.longitude,
                    label: item.placeName,
                    addressLine: item.addressLine,
                    areaName: item.areaName,
                    city: item.city,
                    provider: "MANUAL"
                });
                toast.success(item.placeName + (role === "start" ? " set as start." : " set as destination."));
            });
        });

        list.querySelectorAll("[data-saved-delete]").forEach(function (button) {
            button.addEventListener("click", async function () {
                var card = button.closest("[data-saved-place-id]");
                var id = card.getAttribute("data-saved-place-id");
                button.disabled = true;
                try {
                    await request("/api/v1/saved-places/" + encodeURIComponent(id), { method: "DELETE" });
                    toast.success("Saved place removed.");
                    await load();
                } catch (error) {
                    button.disabled = false;
                    toast.error(error.message);
                }
            });
        });
    }

    async function load() {
        if (!authenticated || !list) return;
        list.innerHTML = '<div class="saved-place-loading"><i></i><span>Loading saved places…</span></div>';
        try {
            var items = await request("/api/v1/saved-places", { method: "GET" });
            render(items);
        } catch (error) {
            list.innerHTML = '<div class="saved-place-empty"><span>Saved places could not be loaded.</span><button type="button" data-saved-retry>Retry</button></div>';
            list.querySelector("[data-saved-retry]")?.addEventListener("click", load);
        }
    }

    function requireLogin() {
        toast.info("Sign in first to save Home, Work or bookmark locations.");
        window.location.href = loginUrl;
    }

    function endpointPoint(role) {
        return role === "start" ? workspace.getStart() : workspace.getDestination();
    }

    function openSave(point) {
        if (!authenticated) { requireLogin(); return; }
        if (!point || !Number.isFinite(Number(point.lat)) || !Number.isFinite(Number(point.lng))) {
            toast.warning("Choose a location first, then save it.");
            return;
        }

        pendingPoint = Object.assign({}, point);
        if (preview) preview.textContent = point.label || (Number(point.lat).toFixed(5) + ", " + Number(point.lng).toFixed(5));
        typeSelect.value = "FAVORITE";
        nameInput.value = point.label && point.label !== "My location" ? point.label : "";
        syncName();
        if (typeof dialog.showModal === "function") dialog.showModal();
        else dialog.setAttribute("open", "");
        window.setTimeout(function () { typeSelect.focus(); }, 0);
    }

    function closeDialog() {
        pendingPoint = null;
        if (!dialog) return;
        if (typeof dialog.close === "function") dialog.close();
        else dialog.removeAttribute("open");
    }

    function syncName() {
        var type = typeSelect.value;
        var fixed = type === "HOME" ? "Home" : type === "OFFICE" ? "Work" : "";
        nameInput.readOnly = Boolean(fixed);
        if (fixed) nameInput.value = fixed;
        else if ((type === "FAVORITE" || type === "OTHER") && (nameInput.value === "Home" || nameInput.value === "Work")) nameInput.value = "";
        nameInput.placeholder = type === "UNIVERSITY" ? "University name" : "Bookmark name";
    }

    root.querySelectorAll("[data-save-place-role]").forEach(function (button) {
        button.addEventListener("click", function () { openSave(endpointPoint(button.getAttribute("data-save-place-role"))); });
    });
    root.querySelector("[data-save-picked-place]")?.addEventListener("click", function () { openSave(workspace.getPickedPoint()); });
    root.querySelectorAll("[data-saved-login]").forEach(function (link) { link.setAttribute("href", loginUrl); });

    typeSelect?.addEventListener("change", syncName);
    root.querySelector("[data-saved-place-cancel]")?.addEventListener("click", closeDialog);
    root.querySelector("[data-saved-place-dialog-close]")?.addEventListener("click", closeDialog);
    dialog?.addEventListener("click", function (event) { if (event.target === dialog) closeDialog(); });

    form?.addEventListener("submit", async function (event) {
        event.preventDefault();
        if (!pendingPoint) return;
        var name = nameInput.value.trim();
        if ((typeSelect.value === "FAVORITE" || typeSelect.value === "OTHER" || typeSelect.value === "UNIVERSITY") && name.length < 2) {
            toast.warning("Enter a name for this saved place.");
            nameInput.focus();
            return;
        }

        submit.disabled = true;
        submit.setAttribute("aria-busy", "true");
        try {
            await request("/api/v1/saved-places", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    placeName: name,
                    placeType: typeSelect.value,
                    latitude: Number(pendingPoint.lat),
                    longitude: Number(pendingPoint.lng),
                    addressLine: pendingPoint.addressLine || pendingPoint.label || null,
                    areaName: pendingPoint.areaName || null,
                    city: pendingPoint.city || null,
                    district: pendingPoint.district || null,
                    provider: pendingPoint.provider || "MANUAL",
                    externalPlaceId: pendingPoint.externalPlaceId || null
                })
            });
            var label = typeSelect.value === "HOME" ? "Home" : typeSelect.value === "OFFICE" ? "Work" : name;
            toast.success(label + " saved.");
            closeDialog();
            await load();
        } catch (error) {
            toast.error(error.message);
        } finally {
            submit.disabled = false;
            submit.removeAttribute("aria-busy");
        }
    });

    if (authenticated) load();
})();
