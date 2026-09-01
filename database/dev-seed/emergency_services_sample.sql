-- ============================================================================
-- SafePath BD — OPTIONAL development seed: sample emergency services
--
-- This file is NOT part of the application schema and is never executed
-- automatically. Run it manually only if you want demonstration markers on the
-- map while `emergency_services` is still empty.
--
--   mysql -u root -p safepath_bd < database/dev-seed/emergency_services_sample.sql
--
-- It only INSERTs into `locations` and `emergency_services`. It does not drop,
-- alter or truncate anything. Coordinates are approximate and for demonstration
-- only — do not treat them as authoritative emergency contact information.
-- ============================================================================

USE safepath_bd;

START TRANSACTION;

INSERT INTO locations (latitude, longitude, address_line, area_name, city, district, division_name, place_provider)
VALUES
    (23.7392000, 90.3958000, 'Bangabandhu Sheikh Mujib Medical University area, Shahbag', 'Shahbag',   'Dhaka', 'Dhaka', 'Dhaka', 'MANUAL'),
    (23.7286000, 90.3985000, 'Dhaka Medical College area, Bakshi Bazar',                   'Bakshibazar','Dhaka', 'Dhaka', 'Dhaka', 'MANUAL'),
    (23.7465000, 90.3760000, 'Dhanmondi 27 area',                                          'Dhanmondi', 'Dhaka', 'Dhaka', 'Dhaka', 'MANUAL'),
    (23.7509000, 90.3925000, 'Kalabagan area',                                             'Kalabagan', 'Dhaka', 'Dhaka', 'Dhaka', 'MANUAL'),
    (23.7806000, 90.4070000, 'Gulshan 1 circle area',                                      'Gulshan',   'Dhaka', 'Dhaka', 'Dhaka', 'MANUAL'),
    (23.7644000, 90.3890000, 'Tejgaon industrial area',                                    'Tejgaon',   'Dhaka', 'Dhaka', 'Dhaka', 'MANUAL');

-- Bind each facility to the location inserted above by its address line.
INSERT INTO emergency_services
    (service_type_id, location_id, service_name, phone, emergency_phone, opening_hours, is_24_hours, is_verified, is_active)
SELECT t.service_type_id, l.location_id, s.service_name, s.phone, s.emergency_phone, s.opening_hours, s.is_24_hours, s.is_verified, TRUE
FROM (
    SELECT 'Hospital'         AS type_name, 'Shahbag General Hospital'      AS service_name, '+8802000000001' AS phone, '999' AS emergency_phone, 'Open 24 hours' AS opening_hours, TRUE  AS is_24_hours, TRUE  AS is_verified, 'Bangabandhu Sheikh Mujib Medical University area, Shahbag' AS address_line
    UNION ALL SELECT 'Hospital',        'Bakshi Bazar Medical Centre',  '+8802000000002', '999', 'Open 24 hours',    TRUE,  TRUE,  'Dhaka Medical College area, Bakshi Bazar'
    UNION ALL SELECT 'Police Station',  'Dhanmondi Police Station',     '+8802000000003', '999', 'Open 24 hours',    TRUE,  TRUE,  'Dhanmondi 27 area'
    UNION ALL SELECT 'Fire Service',    'Kalabagan Fire Station',       '+8802000000004', '999', 'Open 24 hours',    TRUE,  TRUE,  'Kalabagan area'
    UNION ALL SELECT 'Ambulance',       'Gulshan Ambulance Service',    '+8802000000005', '999', '06:00 - 23:00',    FALSE, FALSE, 'Gulshan 1 circle area'
    UNION ALL SELECT 'Emergency Center','Tejgaon Emergency Centre',     '+8802000000006', '999', '08:00 - 20:00',    FALSE, TRUE,  'Tejgaon industrial area'
) AS s
JOIN emergency_service_types t ON t.service_type_name = s.type_name
JOIN locations l ON l.address_line = s.address_line;

COMMIT;

SELECT COUNT(*) AS seeded_emergency_services FROM emergency_services;
