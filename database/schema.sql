-- MicroEraser Database Schema
-- PostgreSQL 14+
-- This schema is also managed by EF Core migrations

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Users table
CREATE TABLE IF NOT EXISTS "Users" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Email" VARCHAR(256) NOT NULL UNIQUE,
    "Name" VARCHAR(256) NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "AvatarUrl" VARCHAR(2048),
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_users_email ON "Users"("Email");

-- Refresh Tokens table
CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Token" VARCHAR(512) NOT NULL UNIQUE,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "RevokedAt" TIMESTAMP WITH TIME ZONE,
    "ReplacedByToken" VARCHAR(512),
    "ReasonRevoked" TEXT
);

CREATE INDEX idx_refresh_tokens_token ON "RefreshTokens"("Token");
CREATE INDEX idx_refresh_tokens_user ON "RefreshTokens"("UserId");

-- Workspaces table
CREATE TABLE IF NOT EXISTS "Workspaces" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "Name" VARCHAR(256) NOT NULL,
    "Description" VARCHAR(2048),
    "OwnerId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE RESTRICT,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_workspaces_owner ON "Workspaces"("OwnerId");

-- Workspace Members (junction table)
CREATE TABLE IF NOT EXISTS "WorkspaceMembers" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "WorkspaceId" UUID NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE,
    "UserId" UUID NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "Role" INTEGER NOT NULL DEFAULT 0, -- 0=Viewer, 1=Editor, 2=Admin
    "JoinedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    UNIQUE("WorkspaceId", "UserId")
);

CREATE INDEX idx_workspace_members_workspace ON "WorkspaceMembers"("WorkspaceId");
CREATE INDEX idx_workspace_members_user ON "WorkspaceMembers"("UserId");

-- Canvases table
CREATE TABLE IF NOT EXISTS "Canvases" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "WorkspaceId" UUID NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE,
    "Name" VARCHAR(256) NOT NULL,
    "Description" VARCHAR(2048),
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_canvases_workspace ON "Canvases"("WorkspaceId");

-- Canvas Snapshots table (Yjs document state persistence)
-- Stores binary CRDT state for recovery and late-join scenarios
CREATE TABLE IF NOT EXISTS "CanvasSnapshots" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "CanvasId" UUID NOT NULL REFERENCES "Canvases"("Id") ON DELETE CASCADE,
    "State" BYTEA NOT NULL, -- Yjs encoded document state
    "Version" BIGINT NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_canvas_snapshots_canvas_version ON "CanvasSnapshots"("CanvasId", "Version" DESC);

-- Invites table
CREATE TABLE IF NOT EXISTS "Invites" (
    "Id" UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    "WorkspaceId" UUID NOT NULL REFERENCES "Workspaces"("Id") ON DELETE CASCADE,
    "Email" VARCHAR(256), -- NULL for link-based invites
    "Token" VARCHAR(256) NOT NULL UNIQUE,
    "Permission" INTEGER NOT NULL DEFAULT 0, -- 0=View, 1=Edit
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW(),
    "IsUsed" BOOLEAN NOT NULL DEFAULT FALSE,
    "UsedAt" TIMESTAMP WITH TIME ZONE,
    "UsedByUserId" UUID,
    "MaxUses" INTEGER, -- NULL = unlimited
    "UseCount" INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX idx_invites_token ON "Invites"("Token");
CREATE INDEX idx_invites_workspace ON "Invites"("WorkspaceId");
CREATE INDEX idx_invites_email ON "Invites"("Email") WHERE "Email" IS NOT NULL;

-- Function to update UpdatedAt timestamp
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

-- Triggers for auto-updating UpdatedAt
CREATE TRIGGER update_users_updated_at
    BEFORE UPDATE ON "Users"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_workspaces_updated_at
    BEFORE UPDATE ON "Workspaces"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_canvases_updated_at
    BEFORE UPDATE ON "Canvases"
    FOR EACH ROW
    EXECUTE FUNCTION update_updated_at_column();

-- Comments for documentation
COMMENT ON TABLE "Users" IS 'User accounts for authentication';
COMMENT ON TABLE "RefreshTokens" IS 'JWT refresh tokens for token rotation';
COMMENT ON TABLE "Workspaces" IS 'Collaborative workspaces containing canvases';
COMMENT ON TABLE "WorkspaceMembers" IS 'User membership and roles within workspaces';
COMMENT ON TABLE "Canvases" IS 'Individual diagram canvases within workspaces';
COMMENT ON TABLE "CanvasSnapshots" IS 'Yjs CRDT document snapshots for persistence';
COMMENT ON TABLE "Invites" IS 'Workspace invite links and email invitations';

COMMENT ON COLUMN "CanvasSnapshots"."State" IS 'Binary Yjs document state (Y.encodeStateAsUpdate)';
COMMENT ON COLUMN "CanvasSnapshots"."Version" IS 'Monotonically increasing version for ordering';
