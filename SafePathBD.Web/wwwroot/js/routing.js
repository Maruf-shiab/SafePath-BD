/* SafePath BD — routing workspace.
   Owns request-scoped OSRM route presentation and incident-aware rechecks while
   map.js remains the single owner of Leaflet endpoint selection and shared drawer. */
(function () {
    "use strict";

 