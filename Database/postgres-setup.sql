-- Orleans PostgreSQL Setup Script
-- Run this script to create the necessary database and tables for Orleans persistence

-- Create the database (if it doesn't exist)
-- Run as superuser or user with CREATEDB privilege
-- CREATE DATABASE orleans;

-- Connect to the orleans database
\c orleans

-- Note: Orleans uses the public schema by default for its clustering tables
-- If you want to use a custom schema, you need to configure it in the Orleans options

-- For simplicity, we'll use the default public schema for Orleans tables
-- and a custom schema for application-specific tables

-- =====================================================
-- Orleans Grain Storage Table
-- Stores grain state for persistence
-- =====================================================
CREATE TABLE IF NOT EXISTS OrleansStorage
(
    grainidhash INTEGER NOT NULL,
    grainidn0 BIGINT NOT NULL,
    grainidn1 BIGINT NOT NULL,
    grainidextensionstring VARCHAR(512),
    serviceid VARCHAR(150) NOT NULL,
    graintypehash INTEGER NOT NULL,
    graintypestring VARCHAR(512) NOT NULL,
    grainidstring VARCHAR(512),
    payloadbinary BYTEA,
    payloadjson JSONB,
    payloadxml XML,
    modifiedon TIMESTAMP NOT NULL,
    version INTEGER,
    CONSTRAINT OrleansStorage_PK PRIMARY KEY (grainidhash, graintypehash, serviceid)
);

CREATE INDEX IF NOT EXISTS OrleansStorage_GrainIdHash_Index 
ON OrleansStorage(grainidhash);

CREATE INDEX IF NOT EXISTS OrleansStorage_ServiceId_Index 
ON OrleansStorage(serviceid);

-- =====================================================
-- Orleans Event Log Storage Table
-- Stores event logs for event sourcing
-- =====================================================
CREATE TABLE IF NOT EXISTS OrleansEventLog
(
    grainidhash INTEGER NOT NULL,
    grainidn0 BIGINT NOT NULL,
    grainidn1 BIGINT NOT NULL,
    grainidextensionstring VARCHAR(512),
    serviceid VARCHAR(150) NOT NULL,
    graintypehash INTEGER NOT NULL,
    graintypestring VARCHAR(512) NOT NULL,
    grainidstring VARCHAR(512),
    payloadbinary BYTEA,
    payloadjson JSONB,
    payloadxml XML,
    modifiedon TIMESTAMP NOT NULL,
    version INTEGER,
    CONSTRAINT OrleansEventLog_PK PRIMARY KEY (grainidhash, graintypehash, serviceid)
);

CREATE INDEX IF NOT EXISTS OrleansEventLog_GrainIdHash_Index 
ON OrleansEventLog(grainidhash);

CREATE INDEX IF NOT EXISTS OrleansEventLog_ServiceId_Index 
ON OrleansEventLog(serviceid);

-- =====================================================
-- Orleans Clustering Tables (For multi-silo production)
-- These must be in the public schema (Orleans default)
-- =====================================================

-- Orleans Query table - stores Orleans schema metadata
CREATE TABLE IF NOT EXISTS OrleansQuery
(
    QueryKey VARCHAR(64) NOT NULL,
    QueryText VARCHAR(8000) NOT NULL,
    CONSTRAINT OrleansQuery_PK PRIMARY KEY (QueryKey)
);

-- Membership version table - tracks cluster membership changes
CREATE TABLE IF NOT EXISTS OrleansMembershipVersionTable
(
    DeploymentId VARCHAR(150) NOT NULL,
    Timestamp TIMESTAMP DEFAULT (CURRENT_TIMESTAMP AT TIME ZONE 'UTC') NOT NULL,
    Version INTEGER DEFAULT 0 NOT NULL,
    CONSTRAINT OrleansMembershipVersionTable_PK PRIMARY KEY (DeploymentId)
);

-- Membership table - tracks active silos in the cluster
CREATE TABLE IF NOT EXISTS OrleansMembershipTable
(
    DeploymentId VARCHAR(150) NOT NULL,
    Address VARCHAR(45) NOT NULL,
    Port INTEGER NOT NULL,
    Generation INTEGER NOT NULL,
    SiloName VARCHAR(150) NOT NULL,
    HostName VARCHAR(150) NOT NULL,
    Status INTEGER NOT NULL,
    ProxyPort INTEGER,
    SuspectTimes VARCHAR(8000),
    StartTime TIMESTAMP NOT NULL,
    IAmAliveTime TIMESTAMP NOT NULL,
    CONSTRAINT OrleansMembershipTable_PK PRIMARY KEY (DeploymentId, Address, Port, Generation)
);

CREATE INDEX IF NOT EXISTS OrleansMembershipTable_DeploymentId_Index 
ON OrleansMembershipTable(DeploymentId);

CREATE INDEX IF NOT EXISTS OrleansMembershipTable_Status_Index 
ON OrleansMembershipTable(Status);

-- Initialize OrleansQuery table with required queries
INSERT INTO OrleansQuery (QueryKey, QueryText)
VALUES 
('UpdateIAmAlivetimeKey', 
'UPDATE OrleansMembershipTable 
SET IAmAliveTime = @IAmAliveTime 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND Address = @Address AND @Address IS NOT NULL 
  AND Port = @Port AND @Port IS NOT NULL 
  AND Generation = @Generation AND @Generation IS NOT NULL;'),
  
('InsertMembershipVersionKey',
'INSERT INTO OrleansMembershipVersionTable (DeploymentId) 
SELECT @DeploymentId 
WHERE NOT EXISTS (SELECT 1 FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL);
SELECT Version FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;'),

('InsertMembershipKey',
'INSERT INTO OrleansMembershipTable 
(DeploymentId, Address, Port, Generation, SiloName, HostName, Status, ProxyPort, StartTime, IAmAliveTime) 
VALUES (@DeploymentId, @Address, @Port, @Generation, @SiloName, @HostName, @Status, @ProxyPort, @StartTime, @IAmAliveTime);
UPDATE OrleansMembershipVersionTable 
SET Timestamp = (CURRENT_TIMESTAMP AT TIME ZONE ''UTC''), Version = Version + 1 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND Version = @Version AND @Version IS NOT NULL 
  AND Timestamp = (SELECT Timestamp FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL);
SELECT Version FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;'),

('UpdateMembershipKey',
'UPDATE OrleansMembershipTable 
SET Status = @Status, SuspectTimes = @SuspectTimes, IAmAliveTime = @IAmAliveTime 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND Address = @Address AND @Address IS NOT NULL 
  AND Port = @Port AND @Port IS NOT NULL 
  AND Generation = @Generation AND @Generation IS NOT NULL;
UPDATE OrleansMembershipVersionTable 
SET Timestamp = (CURRENT_TIMESTAMP AT TIME ZONE ''UTC''), Version = Version + 1 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND Version = @Version AND @Version IS NOT NULL 
  AND Timestamp = (SELECT Timestamp FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL);
SELECT Version FROM OrleansMembershipVersionTable WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;'),

('GatewaysQueryKey',
'SELECT Address, ProxyPort, Generation 
FROM OrleansMembershipTable 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND Status = @Status AND @Status IS NOT NULL 
  AND ProxyPort > 0;'),

('MembershipReadRowKey',
'SELECT v.DeploymentId, m.Address, m.Port, m.Generation, m.SiloName, m.HostName, m.Status, m.ProxyPort, m.SuspectTimes, m.StartTime, m.IAmAliveTime, v.Version 
FROM OrleansMembershipVersionTable v 
LEFT OUTER JOIN OrleansMembershipTable m ON v.DeploymentId = m.DeploymentId 
WHERE v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND (m.Address = @Address AND @Address IS NOT NULL AND m.Port = @Port AND @Port IS NOT NULL AND m.Generation = @Generation AND @Generation IS NOT NULL OR @Address IS NULL AND @Port IS NULL AND @Generation IS NULL);'),

('MembershipReadAllKey',
'SELECT v.DeploymentId, m.Address, m.Port, m.Generation, m.SiloName, m.HostName, m.Status, m.ProxyPort, m.SuspectTimes, m.StartTime, m.IAmAliveTime, v.Version 
FROM OrleansMembershipVersionTable v 
LEFT OUTER JOIN OrleansMembershipTable m ON v.DeploymentId = m.DeploymentId 
WHERE v.DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;'),

('DeleteMembershipTableEntriesKey',
'DELETE FROM OrleansMembershipTable 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;
DELETE FROM OrleansMembershipVersionTable 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL;'),

('CleanupDefunctSiloEntriesKey',
'DELETE FROM OrleansMembershipTable 
WHERE DeploymentId = @DeploymentId AND @DeploymentId IS NOT NULL 
  AND IAmAliveTime < @IAmAliveTime AND Status != 3;')
ON CONFLICT (QueryKey) DO NOTHING;

-- Function to insert membership entry (kept for backwards compatibility)
CREATE OR REPLACE FUNCTION InsertMembership(
    deployment_id VARCHAR(150),
    member_address VARCHAR(45),
    member_port INTEGER,
    member_generation INTEGER,
    member_siloname VARCHAR(150),
    member_hostname VARCHAR(150),
    member_status INTEGER,
    member_proxyport INTEGER,
    member_starttime TIMESTAMP,
    member_iamalivetime TIMESTAMP
)
RETURNS TABLE(version_number INTEGER) AS $$
BEGIN
    INSERT INTO OrleansMembershipTable
    (DeploymentId, Address, Port, Generation, SiloName, HostName, Status, ProxyPort, StartTime, IAmAliveTime)
    VALUES
    (deployment_id, member_address, member_port, member_generation, member_siloname, member_hostname, 
     member_status, member_proxyport, member_starttime, member_iamalivetime);
    
    UPDATE OrleansMembershipVersionTable
    SET Timestamp = (CURRENT_TIMESTAMP AT TIME ZONE 'UTC'),
        Version = Version + 1
    WHERE DeploymentId = deployment_id;
    
    RETURN QUERY
    SELECT Version FROM OrleansMembershipVersionTable WHERE DeploymentId = deployment_id;
END;
$$ LANGUAGE plpgsql;

-- Function to update membership IAmAlive (kept for backwards compatibility)
CREATE OR REPLACE FUNCTION UpdateIAmAlive(
    deployment_id VARCHAR(150),
    member_address VARCHAR(45),
    member_port INTEGER,
    member_generation INTEGER,
    member_iamalivetime TIMESTAMP
)
RETURNS VOID AS $$
BEGIN
    UPDATE OrleansMembershipTable
    SET IAmAliveTime = member_iamalivetime
    WHERE DeploymentId = deployment_id
      AND Address = member_address
      AND Port = member_port
      AND Generation = member_generation;
END;
$$ LANGUAGE plpgsql;

-- =====================================================
-- Orleans Reminders Tables (Optional)
-- =====================================================
CREATE TABLE IF NOT EXISTS OrleansRemindersTable
(
    serviceid VARCHAR(150) NOT NULL,
    grainhash INTEGER NOT NULL,
    grainid VARCHAR(150) NOT NULL,
    remindername VARCHAR(150) NOT NULL,
    starttime TIMESTAMP NOT NULL,
    period BIGINT,
    version INTEGER NOT NULL,
    CONSTRAINT OrleansRemindersTable_PK PRIMARY KEY (serviceid, grainhash, grainid, remindername)
);

-- =====================================================
-- Grant permissions (adjust user as needed)
-- =====================================================
-- GRANT ALL PRIVILEGES ON SCHEMA orleans TO your_user;
-- GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA orleans TO your_user;

-- =====================================================
-- Verification queries
-- =====================================================
-- Check tables created
SELECT table_name 
FROM information_schema.tables 
WHERE table_schema = 'public' AND table_name LIKE 'Orleans%'
ORDER BY table_name;

-- Verify Orleans storage is ready
SELECT 'PostgreSQL Orleans storage setup complete!' as status;

