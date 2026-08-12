-- Scout warehouse: a trail camera fleet as a sensor network.
--
-- Deliberately boring, deliberately realistic. Note what is MISSING:
-- there is no offset or timezone column on detections. The captured_at
-- timestamp is naive. That is not an oversight in this demo -- it is the
-- single most common real-world schema decision that makes silent
-- timezone drift possible.

DROP TABLE IF EXISTS detections;
DROP TABLE IF EXISTS weather_hourly;
DROP TABLE IF EXISTS sits;
DROP TABLE IF EXISTS cameras;
DROP TABLE IF EXISTS stands;
DROP TABLE IF EXISTS firmware_events;

-- Reference / dimension data. Slow moving, hand maintained, authoritative.
CREATE TABLE stands (
    stand_name          VARCHAR PRIMARY KEY,
    stand_type          VARCHAR NOT NULL,   -- ladder | ground blind | saddle
    ideal_wind_cardinal VARCHAR NOT NULL,   -- wind you need for a clean approach
    access_route        VARCHAR NOT NULL,
    notes               VARCHAR
);

-- The sensor fleet. firmware_version is the column that ends up mattering.
CREATE TABLE cameras (
    camera_id        INTEGER PRIMARY KEY,
    stand_name       VARCHAR NOT NULL REFERENCES stands(stand_name),
    orientation      VARCHAR NOT NULL,      -- fixed angle: the whole point of a control
    install_date     DATE NOT NULL,
    firmware_version VARCHAR NOT NULL
);

-- The event stream. High volume, machine generated, on-board classifier.
CREATE TABLE detections (
    detection_id          BIGINT PRIMARY KEY,
    camera_id             INTEGER NOT NULL REFERENCES cameras(camera_id),
    captured_at           TIMESTAMP NOT NULL,  -- naive. no offset. this is the wound.
    species               VARCHAR NOT NULL,
    antlered              BOOLEAN NOT NULL,
    count                 INTEGER NOT NULL,
    classifier_confidence DOUBLE NOT NULL
);

-- External enrichment feed. Someone else's system, someone else's SLA.
CREATE TABLE weather_hourly (
    observed_at       TIMESTAMP PRIMARY KEY,
    temp_f            DOUBLE NOT NULL,
    wind_dir_cardinal VARCHAR NOT NULL,
    wind_dir_deg      INTEGER NOT NULL,
    wind_mph          DOUBLE NOT NULL,
    pressure_inhg     DOUBLE NOT NULL
);

-- Outcome data. The table every organization is missing.
-- Without this you cannot tell a bad recommendation from bad luck.
CREATE TABLE sits (
    sit_id        INTEGER PRIMARY KEY,
    sit_date      DATE NOT NULL,
    stand_name    VARCHAR NOT NULL REFERENCES stands(stand_name),
    start_local   TIMESTAMP NOT NULL,
    hours         DOUBLE NOT NULL,
    deer_observed INTEGER NOT NULL,
    recommended_by VARCHAR,                 -- 'scout' once the agent is in the loop
    notes         VARCHAR
);

-- Audit trail for fleet changes. Exists so the demo can prove the break was
-- knowable -- the information was recorded, just never joined to anything.
CREATE TABLE firmware_events (
    event_id        INTEGER PRIMARY KEY,
    camera_id       INTEGER NOT NULL REFERENCES cameras(camera_id),
    applied_at      TIMESTAMP NOT NULL,
    from_version    VARCHAR NOT NULL,
    to_version      VARCHAR NOT NULL,
    release_notes   VARCHAR
);
