/* SafePath BD — report creation.
   Reuses SafePathMap for the location picker, plus an image dropzone and section stepper. */
(function () {
    "use strict";

    var root = document.querySelector("[data-report-form]");
    if (!root || typeof L === "undefined" || !window.SafePathMap) {
        return;
    }

    var SPM = window.SafePathMap;
    var toast = window.SafePathToast || { show: function () {}, warning: function () {}, error: function () {}, info: function () {} };
    var config = JSON.parse(root.getAttribute("data-report-form") || "{}");
    var reduceMotion = SPM.prefersReducedMotion();

    var fields = {
        lat: root.querySelector("[data-field-lat]"),
        lng: root.querySelector("[data-field-lng]"),
        address: root.querySelector("[data-field-address]"),
        landmark: root.querySelector("[data-field-landmark]"),
        area: root.querySelector("[data-field-area]"),
        city: root.querySelector("[data-field-city]"),
        district: root.querySelector("[data-field-district]"),
        provider: root.querySelector("[data-field-provider]"),
        externalPlaceId: root.querySelector("[data-field-external-place-id]")
    };

    var el = {
        canvas: root.querySelector("[data-picker-canvas]"),
        summary: root.querySelector("[data-location-summary]"),
        coords: root.querySelector("[data-location-coords]"),
        locate: root.querySelector("[data-picker-locate]"),
        search: root.querySelector("[data-picker-search]"),
        suggest: root.querySelector("[data-picker-suggest]"),
        dropzone: root.querySelector("[data-dropzone]"),
        fileInput: root.querySelector("[data-file-input]"),
        previews: root.querySelector("[data-previews]"),
        // The root element is the form itself, not a container around one.
        form: root.matches("form") ? root : root.querySelector("form"),
        submit: root.querySelector("[data-submit]")
    };

    var map = null;
    var marker = null;
    var selectedFiles = [];

    /* ------------------------------------------------------------- location */

    function markerIcon() {
        return SPM.pinIcon("picked", '<path d="M12 8v5M12 16h.01" />');
    }

    function setLocation(lat, lng, place) {
        fields.lat.value = lat;
        fields.lng.value = lng;
        fields.address.value = (place && place.addressLine) || "";
        fields.landmark.value = (place && place.landmarkName) || "";
        fields.area.value = (place && place.areaName) || "";
        fields.city.value = (place && place.city) || "";
        fields.district.value = (place && place.district) || "";
        fields.provider.value = (place && place.provider) || "MANUAL";
        fields.externalPlaceId.value = (place && place.externalPlaceId) || "";

        el.coords.textContent = SPM.formatCoords(lat, lng);
        el.summary.textContent = (place && (place.addressLine || place.displayName)) || "Location selected";
        el.summary.closest("[data-location-card]").classList.add("is-set");

        if (marker) {
            map.removeLayer(marker);
        }
        marker = L.marker([lat, lng], { icon: markerIcon(), title: "Report location" }).addTo(map);

        // Clear any server-side validation message once a point exists.
        root.querySelectorAll("[data-location-error]").forEach(function (node) {
            node.textContent = "";
        });
    }

    async function resolveAndSet(lat, lng) {
        el.summary.textContent = "Looking up this place…";
        el.coords.textContent = SPM.formatCoords(lat, lng);

        try {
            var place = await SPM.getJson("/api/v1/locations/reverse?lat=" + lat + "&lng=" + lng);
            setLocation(lat, lng, place);
        } catch (error) {
            // Coordinates are the source of truth; the address is a bonus.
            setLocation(lat, lng, null);
            el.summary.textContent = "Address unavailable — coordinates saved";
            toast.info("Address information could not be loaded. The selected coordinates are still used.");
        }
    }

    function initMap() {
        map = SPM.createMap(el.canvas, {
            lat: config.lat,
            lng: config.lng,
            zoom: config.zoom,
            zoomPosition: "bottomright"
        });

        map.on("click", function (event) {
            resolveAndSet(event.latlng.lat, event.latlng.lng);
        });

        // Leaflet needs a re-measure once the panel has finished its entrance transition.
        window.setTimeout(function () {
            map.invalidateSize();
        }, reduceMotion ? 0 : 320);
    }

    function locateUser() {
        if (!("geolocation" in navigator)) {
            toast.error("This browser cannot share your location. Tap the map instead.");
            return;
        }

        el.locate.classList.add("is-busy");

        navigator.geolocation.getCurrentPosition(
            function (position) {
                el.locate.classList.remove("is-busy");
                var lat = position.coords.latitude;
                var lng = position.coords.longitude;
                SPM.flyTo(map, lat, lng, 16);
                resolveAndSet(lat, lng);
            },
            function (error) {
                el.locate.classList.remove("is-busy");
                if (error.code === error.PERMISSION_DENIED) {
                    toast.warning("Location access was denied. Tap the map to choose the spot instead.");
                } else if (error.code === error.TIMEOUT) {
                    toast.warning("Finding your location took too long. Tap the map instead.");
                } else {
                    toast.warning("Your location is unavailable. Tap the map to choose the spot.");
                }
            },
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 60000 }
        );
    }

    function initSearch() {
        var timer = null;
        var results = [];

        function close() {
            el.suggest.classList.remove("is-open");
            el.search.setAttribute("aria-expanded", "false");
        }

        function status(message) {
            el.suggest.innerHTML = '<div class="suggest-status"><span>' + SPM.escapeHtml(message) + "</span></div>";
            el.suggest.classList.add("is-open");
            el.search.setAttribute("aria-expanded", "true");
        }

        el.search.addEventListener("input", function () {
            window.clearTimeout(timer);
            var value = el.search.value.trim();

            if (value.length < 2) {
                close();
                return;
            }

            status("Searching…");

            timer = window.setTimeout(async function () {
                try {
                    results = await SPM.getJson("/api/v1/locations/search?q=" + encodeURIComponent(value) + "&limit=6");

                    if (!results.length) {
                        status("No matching places found.");
                        return;
                    }

                    el.suggest.innerHTML = results.map(function (r, i) {
                        return '<button class="suggest-item" type="button" data-index="' + i + '">' +
                            "<strong>" + SPM.escapeHtml(r.shortName) + "</strong>" +
                            "<span>" + SPM.escapeHtml(r.displayName) + "</span></button>";
                    }).join("");

                    el.suggest.classList.add("is-open");
                    el.search.setAttribute("aria-expanded", "true");

                    el.suggest.querySelectorAll(".suggest-item").forEach(function (item) {
                        item.addEventListener("click", function () {
                            var r = results[Number(item.getAttribute("data-index"))];
                            el.search.value = r.shortName;
                            close();
                            SPM.flyTo(map, r.latitude, r.longitude, 16);
                            setLocation(r.latitude, r.longitude, {
                                addressLine: r.displayName,
                                landmarkName: r.shortName,
                                provider: r.provider,
                                externalPlaceId: r.externalPlaceId
                            });
                        });
                    });
                } catch (error) {
                    status("Location search is unavailable right now.");
                }
            }, 400);
        });

        el.search.addEventListener("keydown", function (event) {
            if (event.key === "Escape") { close(); }
            if (event.key === "Enter") { event.preventDefault(); }
        });

        el.search.addEventListener("blur", function () {
            window.setTimeout(close, 160);
        });
    }

    /* --------------------------------------------------------------- images */

    function formatSize(bytes) {
        return bytes < 1024 * 1024
            ? Math.round(bytes / 1024) + " KB"
            : (bytes / (1024 * 1024)).toFixed(1) + " MB";
    }

    function syncFileInput() {
        var transfer = new DataTransfer();
        selectedFiles.forEach(function (file) {
            transfer.items.add(file);
        });
        el.fileInput.files = transfer.files;
    }

    function renderPreviews() {
        el.previews.innerHTML = "";

        if (!selectedFiles.length) {
            el.previews.innerHTML = '<p class="t-meta" data-empty>No supporting images added.</p>';
            return;
        }

        selectedFiles.forEach(function (file, index) {
            var item = document.createElement("figure");
            item.className = "preview";

            var img = document.createElement("img");
            img.alt = "";
            img.loading = "lazy";
            img.src = URL.createObjectURL(file);
            img.addEventListener("load", function () {
                URL.revokeObjectURL(img.src);
            });

            var caption = document.createElement("figcaption");
            caption.textContent = formatSize(file.size);

            var remove = document.createElement("button");
            remove.type = "button";
            remove.className = "preview-remove";
            remove.setAttribute("aria-label", "Remove image " + (index + 1));
            remove.innerHTML = SPM.svg('<path d="M6 6l12 12M18 6 6 18" />', 13);
            remove.addEventListener("click", function () {
                selectedFiles.splice(index, 1);
                syncFileInput();
                renderPreviews();
            });

            item.appendChild(img);
            item.appendChild(caption);
            item.appendChild(remove);
            el.previews.appendChild(item);
        });
    }

    function acceptFiles(fileList) {
        var incoming = Array.prototype.slice.call(fileList);
        var allowed = config.allowedTypes || ["image/jpeg", "image/png", "image/webp"];
        var maxBytes = (config.maxImageMb || 5) * 1024 * 1024;
        var maxCount = config.maxImages || 4;

        incoming.forEach(function (file) {
            if (selectedFiles.length >= maxCount) {
                toast.warning("You can attach at most " + maxCount + " images.");
                return;
            }
            if (allowed.indexOf(file.type) === -1) {
                toast.warning('"' + file.name + '" is not a JPG, PNG or WebP image.');
                return;
            }
            if (file.size > maxBytes) {
                toast.warning('"' + file.name + '" is larger than ' + (config.maxImageMb || 5) + " MB.");
                return;
            }
            selectedFiles.push(file);
        });

        syncFileInput();
        renderPreviews();
    }

    function initDropzone() {
        renderPreviews();

        el.dropzone.addEventListener("click", function () {
            el.fileInput.click();
        });

        el.dropzone.addEventListener("keydown", function (event) {
            if (event.key === "Enter" || event.key === " ") {
                event.preventDefault();
                el.fileInput.click();
            }
        });

        el.fileInput.addEventListener("change", function () {
            // input.files is live, so copy it before the input is cleared.
            var picked = Array.prototype.slice.call(el.fileInput.files);
            el.fileInput.value = "";
            acceptFiles(picked);
        });

        ["dragenter", "dragover"].forEach(function (name) {
            el.dropzone.addEventListener(name, function (event) {
                event.preventDefault();
                el.dropzone.classList.add("is-hover");
            });
        });

        ["dragleave", "drop"].forEach(function (name) {
            el.dropzone.addEventListener(name, function (event) {
                event.preventDefault();
                el.dropzone.classList.remove("is-hover");
            });
        });

        el.dropzone.addEventListener("drop", function (event) {
            if (event.dataTransfer && event.dataTransfer.files) {
                acceptFiles(event.dataTransfer.files);
            }
        });
    }

    /* -------------------------------------------------------------- stepper */

    function initStepper() {
        var sections = Array.prototype.slice.call(root.querySelectorAll("[data-step-section]"));
        var links = Array.prototype.slice.call(root.querySelectorAll("[data-step-link]"));

        if (!sections.length || !("IntersectionObserver" in window)) {
            return;
        }

        var observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (!entry.isIntersecting) {
                    return;
                }
                var id = entry.target.getAttribute("data-step-section");
                links.forEach(function (link) {
                    link.classList.toggle("is-active", link.getAttribute("data-step-link") === id);
                });
            });
        }, { rootMargin: "-45% 0px -45% 0px" });

        sections.forEach(function (section) {
            observer.observe(section);
        });

        links.forEach(function (link) {
            link.addEventListener("click", function () {
                var target = root.querySelector('[data-step-section="' + link.getAttribute("data-step-link") + '"]');
                if (target) {
                    target.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
                }
            });
        });
    }

    /* ------------------------------------------------------------ submission */

    function initSubmit() {
        var submitting = false;

        el.form.addEventListener("submit", function (event) {
            if (submitting) {
                event.preventDefault();
                return;
            }

            if (!fields.lat.value || !fields.lng.value) {
                event.preventDefault();
                toast.warning("Choose the location of the report on the map first.");
                el.canvas.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "center" });
                return;
            }

            submitting = true;
            // The width is fixed first so swapping the label cannot shift the layout.
            el.submit.style.minWidth = el.submit.offsetWidth + "px";
            el.submit.classList.add("is-loading");
            el.submit.setAttribute("aria-busy", "true");
            el.submit.textContent = "Submitting…";
        });
    }

    initMap();
    initSearch();
    initDropzone();
    initStepper();
    initSubmit();

    el.locate.addEventListener("click", locateUser);

    if (fields.lat.value && fields.lng.value) {
        var lat = Number(fields.lat.value);
        var lng = Number(fields.lng.value);
        setLocation(lat, lng, {
            addressLine: fields.address.value,
            landmarkName: fields.landmark.value,
            areaName: fields.area.value,
            city: fields.city.value,
            district: fields.district.value,
            provider: fields.provider.value,
            externalPlaceId: fields.externalPlaceId.value
        });
        map.setView([lat, lng], 16);
    }
})();
