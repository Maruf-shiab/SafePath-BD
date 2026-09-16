/* SafePath BD — Chunk 7 intelligent multimodal mobility workspace.
   Reuses the existing map and endpoint state; server remains authoritative for
   traffic prediction, multimodal ETA, safety, ranking and explanations. */
(function () {
    "use strict";

    var workspace = window.SafePathMapWorkspace;
    var SPM = window.SafePathMap;
    var root = document.querySelector("[data-map-root]");
    var controls = root && root.querySelector("[data-intelligent-controls]");
    var planButton = root && root.querySelector("[data-plan-intelligent]");
    if (!workspace || !SPM || !root || !controls || !planButton || typeof L === "undefined") return;

    var map = workspace.getMap();
    var reduceMotion = workspace.prefersReducedMotion();
    var toast = window.SafePathToast || { info: function(){}, warning: function(){}, error: function(){}, success: function(){} };
    var journeyLayer = L.layerGroup().addTo(map);
    var transferLayer = L.layerGroup().addTo(map);
    var state = { response: null, selectedId: null, displayMode: "JOURNEY", busy: false, controller: null };

    var leaveNow = controls.querySelector("[data-leave-now]");
    var departure = controls.querySelector("[data-departure-time]");
    var preference = controls.querySelector("[data-journey-preference]");
    var maxWalking = controls.querySelector("[data-max-walking]");
    var maxTransfers = controls.querySelector("[data-max-transfers]");
    var walkModeInput = controls.querySelector('[data-mode-pills] input[value="WALK"]');

    function esc(value) { return SPM.escapeHtml(value == null ? "" : String(value)); }
    function svg(paths, size) { return SPM.svg(paths, size || 16); }
    function token(name, fallback) {
        var value = getComputedStyle(document.documentElement).getPropertyValue(name).trim();
        return value || fallback;
    }
    function endpointsReady() { return Boolean(workspace.getStart() && workspace.getDestination()); }
    function selectedJourney() {
        return state.response && state.response.journeys.find(function (j) { return j.id === state.selectedId; });
    }
    function antiforgery() {
        return root.querySelector('[data-routing-antiforgery] input[name="__RequestVerificationToken"]')?.value || "";
    }
    async function postJson(url, body, signal) {
        var headers = { "Content-Type": "application/json", "Accept": "application/json" };
        var tokenValue = antiforgery();
        if (tokenValue) headers["X-CSRF-TOKEN"] = tokenValue;
        var response = await fetch(url, { method: "POST", headers: headers, body: JSON.stringify(body), signal: signal });
        var payload = null;
        try { payload = await response.json(); } catch (_) { }
        if (!response.ok || !payload || payload.success === false) {
            var error = new Error(payload?.message || (response.status === 503 ? "Road routing is temporarily unavailable." : "The intelligent journey request could not be completed."));
            error.status = response.status;
            throw error;
        }
        return payload.data;
    }

    function updatePlanButton() {
        var modes = enabledModes();
        planButton.disabled = state.busy || !endpointsReady() || modes.length === 0;
        planButton.title = endpointsReady() ? (modes.length ? "Plan intelligent multimodal journeys" : "Enable at least one mode") : "Choose a start and destination first";
    }
    function enabledModes() {
        return Array.from(controls.querySelectorAll('[data-mode-pills] input:checked:not(:disabled)')).map(function (i) { return i.value; });
    }
    function syncWalkingConstraint() {
        if (!walkModeInput) return;
        var noWalking = Number(maxWalking.value) === 0;
        var label = walkModeInput.closest("label");
        if (noWalking) {
            walkModeInput.checked = false;
            walkModeInput.disabled = true;
            if (label) label.title = "Walk mode is disabled because Maximum walking is set to No walking.";
        } else {
            walkModeInput.disabled = false;
            if (label) label.removeAttribute("title");
        }
        updatePlanButton();
    }
    function setBusy(value) {
        state.busy = value;
        var label = planButton.querySelector("[data-plan-intelligent-label]");
        if (label) label.textContent = value ? "Building journeys…" : "Plan smart journey";
        planButton.setAttribute("aria-busy", String(value));
        updatePlanButton();
    }
    function clearSmart(options) {
        var opts = options || {};
        journeyLayer.clearLayers();
        transferLayer.clearLayers();
        if (state.controller) { state.controller.abort(); state.controller = null; }
        state.response = null;
        state.selectedId = null;
        if (!opts.keepDrawer) {
            var title = root.querySelector("[data-drawer-title]");
            if (title && title.textContent === "Intelligent journeys") workspace.closeDrawer();
        }
    }

    function requestBody() {
        var start = workspace.getStart(), end = workspace.getDestination();
        var departureValue = null;
        if (!leaveNow.checked && departure.value) {
            var parsed = new Date(departure.value);
            if (!isNaN(parsed.getTime())) departureValue = parsed.toISOString();
        }
        return {
            start: { latitude: start.lat, longitude: start.lng, label: start.label || null },
            destination: { latitude: end.lat, longitude: end.lng, label: end.label || null },
            departureTime: departureValue,
            enabledModes: enabledModes(),
            preference: preference.value,
            maxWalkingMeters: Number(maxWalking.value),
            maxTransfers: Number(maxTransfers.value)
        };
    }

    async function plan() {
        if (state.busy || !endpointsReady() || enabledModes().length === 0) return;
        window.dispatchEvent(new CustomEvent("safepath:intelligent-search-start"));
        clearSmart({ keepDrawer: true });
        setBusy(true);
        renderLoading();
        state.controller = new AbortController();
        try {
            var data = await postJson("/api/v1/routes/intelligent", requestBody(), state.controller.signal);
            state.response = data;
            state.selectedId = data.bestOverallJourneyId || data.journeys[0]?.id;
            renderDrawer();
            drawSelected();
        } catch (error) {
            if (error.name !== "AbortError") renderError(error);
        } finally {
            state.controller = null;
            setBusy(false);
        }
    }

    function renderLoading() {
        workspace.renderDrawer("Intelligent journeys", "Predicting traffic · evaluating modes · ranking options",
            '<div class="intel-loading" role="status" aria-live="polite">' +
            '<span class="intel-orbit" aria-hidden="true"></span><div><strong>Building practical journeys…</strong>' +
            '<p>Checking predicted congestion, verified incidents, walking, transfers, ETA, confidence and resilience.</p></div></div>' +
            '<div class="intel-skeleton"><i></i><i></i><i></i></div>');
    }
    function renderError(error) {
        var message = error.status === 422 ? "No practical journey matched these mode, walking and transfer constraints." : error.message;
        var body = workspace.renderDrawer("Intelligent journeys", "Your start and destination were kept",
            '<div class="intel-empty"><span aria-hidden="true">' + svg('<path d="M12 8v5M12 17h.01"/><circle cx="12" cy="12" r="9"/>',22) +
            '</span><h3 class="t-h3">Journey planning unavailable</h3><p class="t-body">' + esc(message) + '</p>' +
            '<button type="button" class="btn btn-secondary" data-intel-retry>Try again</button></div>');
        body.querySelector("[data-intel-retry]")?.addEventListener("click", plan);
    }

    function renderDrawer() {
        if (!state.response) return;
        var selected = selectedJourney();
        var html = renderHeader(selected) + renderVisualizationTabs() + renderWarnings() +
            '<div class="journey-cards">' + state.response.journeys.map(renderJourneyCard).join("") + '</div>' +
            (selected ? renderSelectedDetail(selected) : "") + renderDepartureWindow() + renderBackup();
        var body = workspace.renderDrawer("Intelligent journeys", "Top 3 practical options across your selected modes · predicted traffic, not live traffic", html);
        bindDrawer(body);
    }

    function renderHeader(journey) {
        if (!journey) return "";
        return '<section class="intel-hero" aria-live="polite"><div><span class="t-eyebrow">' + esc(journey.label) + '</span>' +
            '<h3>' + esc(modeSequence(journey)) + '</h3><p>' + fmtMin(journey.totalDurationMinutes) + ' · ' + fmtKm(journey.totalDistanceKm) + '</p>' +
            incidentBadge(journey) + '</div>' +
            '<div class="intel-hero-score"><strong>' + Math.round(journey.dataConfidence) + '%</strong><span>data confidence</span></div></section>';
    }
    function renderVisualizationTabs() {
        return '<div class="intel-tabs" role="group" aria-label="Map visualization">' +
            ["JOURNEY","TRAFFIC","SAFETY"].map(function (mode) {
                return '<button type="button" data-intel-map-mode="' + mode + '" class="' + (state.displayMode === mode ? 'is-active' : '') + '" aria-pressed="' + (state.displayMode === mode) + '">' + mode[0] + mode.slice(1).toLowerCase() + '</button>';
            }).join("") + '</div>';
    }
    function renderWarnings() {
        if (!state.response.warnings?.length) return "";
        return '<details class="intel-warnings"><summary>Data notes (' + state.response.warnings.length + ')</summary><ul>' +
            state.response.warnings.map(function (w) { return '<li>' + esc(w) + '</li>'; }).join("") + '</ul></details>';
    }
    function renderJourneyCard(j, index) {
        var selected = j.id === state.selectedId;
        var incidentClass = String(j.incidentState || "CLEAR").toLowerCase();
        return '<button type="button" class="journey-card journey-card--' + esc(incidentClass) + ' ' + (selected ? 'is-selected' : '') + '" data-journey-id="' + esc(j.id) + '" aria-pressed="' + selected + '">' +
            '<span class="journey-rank">#' + (index + 1) + '</span><span class="journey-card__main"><span class="journey-label">' + esc(j.label || "ALTERNATIVE") + '</span>' +
            '<strong>' + esc(modeSequence(j)) + '</strong><span>' + fmtMin(j.totalDurationMinutes) + ' · ' + fmtKm(j.totalDistanceKm) + '</span>' + incidentBadge(j) + '</span>' +
            '<span class="journey-card__metrics"><span><b>' + Math.round(j.safetyScore || 0) + '</b> safety*</span><span><b>' + Math.round(j.resilience) + '</b> resilience</span>' +
            '<span><b>' + esc(j.trafficLevel) + '</b> traffic</span></span></button>';
    }

    function renderSelectedDetail(j) {
        return '<section class="intel-detail"><div class="intel-metric-grid">' +
            metric("ETA", fmtMin(j.totalDurationMinutes)) + metric("Walking", fmtMeters(j.walkingDistanceMeters)) + metric("Transfers", String(j.transferCount)) +
            metric("Congestion", Math.round(j.predictedCongestionIndex) + "/100") + metric("Incident", incidentTitle(j.incidentState)) + metric("Confidence", Math.round(j.dataConfidence) + "%") +
            metric("Exposure", fmtMin(j.safetyExposure.elevatedRiskMinutes)) +
            '</div>' + renderDna(j) + '<div class="intel-explain"><h4>Why this route?</h4><p>' + esc(j.explanation) + '</p>' + renderTradeoffs(j) + '</div>' +
            '<div class="journey-timeline"><h4>Journey timeline</h4>' + renderTimeline(j) + '</div></section>';
    }
    function metric(label, value) { return '<div><span>' + esc(label) + '</span><strong>' + esc(value) + '</strong></div>'; }
    function renderDna(j) {
        var dna = j.journeyDna || {};
        var items = [["Safety",dna.safety],["Traffic",dna.trafficResilience],["Transfer",dna.transferConvenience],["Walking",dna.walkingBurden],["Resilience",dna.routeResilience],["Confidence",dna.dataConfidence]];
        return '<div class="journey-dna"><div class="journey-dna__head"><h4>Journey DNA</h4><span>0–100</span></div>' + items.map(function (x) {
            var v=Math.max(0,Math.min(100,Number(x[1]||0))); return '<div class="dna-row"><span>'+x[0]+'</span><div><i style="width:'+v+'%"></i></div><b>'+Math.round(v)+'</b></div>';
        }).join("") + '</div>';
    }
    function renderTradeoffs(j) {
        if (!j.tradeoffs?.length) return '<p class="t-meta">No material trade-off statement was generated for this option.</p>';
        return '<details class="intel-tradeoffs"><summary>Why not the other route?</summary>' + j.tradeoffs.slice(0,8).map(function(t){return '<div class="tradeoff tradeoff--'+esc(t.tone)+'"><strong>'+esc(t.title)+'</strong><span>'+esc(t.detail)+'</span></div>';}).join("") + '</details>';
    }
    function renderTimeline(j) {
        var groups = groupLegs(j.legs || []);
        return groups.map(function (g, index) {
            var isTransfer = g.isTransfer;
            var whatIf = !isTransfer && g.sequence != null ? '<button class="whatif-link" type="button" data-whatif-leg="'+g.sequence+'">What if this section is blocked?</button>' : '';
            return '<article class="timeline-leg mode-'+esc(g.mode.toLowerCase())+'"><span class="timeline-node" aria-hidden="true"></span><div><span class="mode-tag">'+esc(modeTitle(g.mode))+'</span>' +
                '<strong>'+esc(g.roadName || g.busRoute || g.transferNote || (index===0?'Start':'Journey leg'))+'</strong>' +
                '<p>'+ (isTransfer ? esc(g.transferNote || "Transfer") + ' · ' : fmtKm(g.distanceMeters/1000) + ' · ') + fmtMin(g.durationMinutes) +
                (g.expectedWaitMinutes>0?' · wait ~'+Math.round(g.expectedWaitMinutes)+' min':'') + '</p>' +
                (!isTransfer ? '<small>'+esc(g.trafficLevel)+' traffic · '+Math.round(g.predictedCongestionIndex)+'/100 · '+esc(g.dataConfidence)+' data · '+esc(incidentTitle(g.hazardState))+'</small>' : '') + whatIf + '</div></article>';
        }).join("");
    }
    function groupLegs(legs) {
        var groups=[];
        legs.forEach(function(l){
            var prev=groups[groups.length-1];
            if(prev && !l.isTransfer && !prev.isTransfer && prev.mode===l.mode && prev.busRouteId===l.busRouteId){
                prev.distanceMeters += l.distanceMeters; prev.durationMinutes += l.durationMinutes; prev.geometry=prev.geometry.concat(l.geometry||[]); prev.end=l.end;
                prev.predictedCongestionIndex=(prev.predictedCongestionIndex+l.predictedCongestionIndex)/2; prev.sequence = prev.sequence ?? l.sequence;
            } else groups.push(Object.assign({},l,{geometry:(l.geometry||[]).slice()}));
        });
        return groups;
    }
    function renderDepartureWindow() {
        var d=state.response.departureWindow;
        if(!d) return "";
        var cards=(d.options||[]).map(function(o){return '<div><strong>'+new Date(o.departureTime).toLocaleTimeString([], {hour:'2-digit',minute:'2-digit'})+'</strong><span>'+fmtMin(o.estimatedDurationMinutes)+'</span><small>'+Math.round(o.predictedCongestionIndex)+'/100 traffic</small></div>';}).join("");
        return '<section class="departure-window"><h4>Departure window</h4><div class="departure-grid">'+cards+'</div>'+(d.hasSuggestion?'<p class="departure-suggestion">'+esc(d.suggestion)+'</p>':'<p class="t-meta">No later departure produced a material predicted improvement.</p>')+'</section>';
    }
    function renderBackup() {
        var b=state.response.backupJourney; if(!b) return "";
        return '<section class="backup-plan"><span class="t-eyebrow">Backup plan</span><strong>'+esc(modeSequence(b))+'</strong><p>'+fmtMin(b.totalDurationMinutes)+' · '+fmtKm(b.totalDistanceKm)+' · resilience '+Math.round(b.resilience)+'/100</p><button type="button" class="btn btn-secondary" data-view-backup>View backup</button></section>';
    }

    function bindDrawer(body) {
        body.querySelectorAll("[data-journey-id]").forEach(function(btn){btn.addEventListener("click",function(){state.selectedId=btn.dataset.journeyId; renderDrawer(); drawSelected();});});
        body.querySelectorAll("[data-intel-map-mode]").forEach(function(btn){btn.addEventListener("click",function(){state.displayMode=btn.dataset.intelMapMode; renderDrawer(); drawSelected();});});
        body.querySelectorAll("[data-whatif-leg]").forEach(function(btn){btn.addEventListener("click",function(){runWhatIf(Number(btn.dataset.whatifLeg),btn);});});
        body.querySelector("[data-view-backup]")?.addEventListener("click",function(){var b=state.response.backupJourney;if(!b)return; var existing=state.response.journeys.find(function(j){return j.id===b.id;}); if(existing){state.selectedId=existing.id;renderDrawer();drawSelected();}else{drawJourney(b);}});
    }

    async function runWhatIf(sequence, button) {
        var j=selectedJourney(); if(!j||!state.response) return;
        button.disabled=true; button.textContent="Simulating…";
        try {
            var data=await postJson("/api/v1/routes/intelligent/what-if",{searchId:state.response.searchId,journeyId:j.id,legSequence:sequence});
            var content='<div class="simulation"><span class="t-eyebrow">WHAT-IF SIMULATION</span><h3>'+esc(data.scenario)+'</h3><p>'+esc(data.message||"")+'</p>'+
                (data.journeys||[]).map(function(x){return '<div class="simulation-option"><strong>'+esc(x.label||modeSequence(x))+'</strong><span>'+fmtMin(x.totalDurationMinutes)+' · '+fmtKm(x.totalDistanceKm)+'</span></div>';}).join("")+
                '<button class="btn btn-secondary" type="button" data-simulation-back>Back to journeys</button></div>';
            var body=workspace.renderDrawer("What-if disruption", "Simulation only — not a real event", content);
            body.querySelector("[data-simulation-back]")?.addEventListener("click",renderDrawer);
        } catch(error){ toast.warning(error.message||"The what-if simulation could not be completed."); }
        finally { button.disabled=false; button.textContent="What if this section is blocked?"; }
    }

    function drawSelected() {
        var selected = selectedJourney();
        if (!selected) return;
        journeyLayer.clearLayers();
        transferLayer.clearLayers();

        // Keep the other two Top-3 options visible as subdued context. The selected journey
        // remains visually dominant so users can immediately see that Smart Journey found
        // genuinely different route/mode choices.
        (state.response?.journeys || []).forEach(function (journey) {
            if (journey.id !== selected.id) drawJourneyGeometry(journey, false, false);
        });
        var selectedPoints = drawJourneyGeometry(selected, true, true);
        if (selectedPoints.length) workspace.fitCoordinates(selectedPoints, [80,80]);
    }

    function drawJourney(j) {
        journeyLayer.clearLayers();
        transferLayer.clearLayers();
        var points = drawJourneyGeometry(j, true, true);
        if (points.length) workspace.fitCoordinates(points, [80,80]);
    }

    function drawJourneyGeometry(j, isSelected, showTransfers) {
        var all=[];
        (j.legs||[]).forEach(function(leg){
            if(leg.isTransfer || !(leg.geometry||[]).length) return;
            var points=leg.geometry.map(function(p){all.push(p);return [p.latitude,p.longitude];});
            var style=legStyle(leg);
            if(!isSelected){
                style=Object.assign({},style,{opacity:.24,weight:Math.max(2,(style.weight||6)-3),dashArray:style.dashArray||"7 9"});
            }
            var line=L.polyline(points,style).addTo(journeyLayer);
            if(isSelected) line.bindTooltip(modeTitle(leg.mode)+" · "+fmtMin(leg.durationMinutes),{sticky:true,className:"route-tooltip"});
        });
        if(showTransfers){
            groupLegs(j.legs||[]).forEach(function(leg){
                if(!leg.isTransfer) return;
                var point=leg.end||leg.start; if(!point)return;
                L.marker([point.latitude,point.longitude],{icon:transferIcon(leg.mode)}).bindTooltip(esc(leg.transferNote||"Transfer")).addTo(transferLayer);
            });
        }
        return all;
    }

    function legStyle(leg) {
        var base={weight:6,opacity:.9,lineCap:"round",lineJoin:"round"};
        if(state.displayMode==="TRAFFIC"){
            var i=Number(leg.predictedCongestionIndex||0); base.color=i>=80?token("--danger","#ef6262"):i>=55?"#e99a3e":i>=30?"#d5b34d":token("--accent-primary","#2fd3b4"); return base;
        }
        if(state.displayMode==="SAFETY"){
            var s=Number(leg.safetyScore||0); base.color=s>=75?token("--accent-primary","#2fd3b4"):s>=55?"#d5b34d":token("--danger","#ef6262"); return base;
        }
        var styles={WALK:{color:"#8fa1b8",dashArray:"4 8",weight:5},RICKSHAW:{color:"#d7a64a",dashArray:"12 6",weight:6},BUS:{color:"#4d8cf5",weight:8},MOTORBIKE:{color:"#b779e8",weight:6},CAR:{color:token("--accent-primary","#2fd3b4"),weight:6}};
        return Object.assign(base,styles[leg.mode]||{});
    }
    function transferIcon(mode){return L.divIcon({className:"intel-transfer-marker",html:'<span aria-hidden="true">↔</span><b>'+esc(modeTitle(mode))+'</b>',iconSize:[54,32],iconAnchor:[27,16]});}

    function incidentTitle(state){
        var value=String(state||"CLEAR").toUpperCase();
        return value==="AFFECTED"?"Affected":value==="CAUTION"?"Caution":"Clear";
    }
    function incidentBadge(j){
        var state=String(j.incidentState||"CLEAR").toUpperCase();
        var text=state==="AFFECTED"?"Serious verified incident":state==="CAUTION"?"Verified incident caution":"No active verified incident detected";
        return '<span class="journey-incident journey-incident--'+esc(state.toLowerCase())+'">'+esc(text)+'</span>';
    }

    function modeSequence(j){return (j.modes||[]).map(modeTitle).join(" → ") || "Journey";}
    function modeTitle(m){return ({WALK:"Walk",RICKSHAW:"Rickshaw",BUS:"Bus",MOTORBIKE:"Motorbike",CAR:"Car"})[m]||m;}
    function fmtMin(v){return Math.max(1,Math.round(Number(v||0)))+" min";}
    function fmtKm(v){return Number(v||0).toFixed(1)+" km";}
    function fmtMeters(v){return Number(v||0)>=1000?(Number(v)/1000).toFixed(1)+" km":Math.round(Number(v||0))+" m";}

    leaveNow.addEventListener("change",function(){departure.disabled=leaveNow.checked;if(!leaveNow.checked&&!departure.value){var d=new Date(Date.now()+5*60000);departure.value=new Date(d.getTime()-d.getTimezoneOffset()*60000).toISOString().slice(0,16);}});
    maxWalking.addEventListener("change", syncWalkingConstraint);
    controls.querySelectorAll("input,select").forEach(function(el){el.addEventListener("change",updatePlanButton);});
    planButton.addEventListener("click",plan);
    workspace.onEndpointsChanged(function(){clearSmart();updatePlanButton();});
    window.addEventListener("safepath:road-route-search-start",function(){clearSmart();});
    syncWalkingConstraint();
    updatePlanButton();
})();
