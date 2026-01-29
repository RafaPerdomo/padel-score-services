CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TYPE match_status AS ENUM ('LIVE', 'FINISHED', 'ABANDONED');

CREATE TABLE IF NOT EXISTS users (
    id TEXT PRIMARY KEY,
    name TEXT NULL,
    email TEXT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_lower 
ON users (LOWER(email)) 
WHERE email IS NOT NULL;

CREATE TABLE IF NOT EXISTS matches (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id TEXT NOT NULL REFERENCES users(id),
    played_at TIMESTAMPTZ DEFAULT NOW(),
    status match_status DEFAULT 'LIVE',
    won BOOLEAN NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT status_won_consistency CHECK (
        (status = 'FINISHED' AND won IS NOT NULL) OR 
        (status != 'FINISHED')
    )
);

CREATE INDEX IF NOT EXISTS idx_matches_user_status ON matches(user_id, status);
CREATE INDEX IF NOT EXISTS idx_matches_user_played ON matches(user_id, played_at DESC);

CREATE TABLE IF NOT EXISTS match_state (
    match_id UUID PRIMARY KEY REFERENCES matches(id) ON DELETE CASCADE,
    version BIGINT DEFAULT 0,
    state_json JSONB NOT NULL,
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

CREATE TABLE IF NOT EXISTS match_events (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    match_id UUID NOT NULL REFERENCES matches(id) ON DELETE CASCADE,
    seq BIGINT NOT NULL,
    event_type TEXT NOT NULL,
    payload JSONB NOT NULL,
    created_at TIMESTAMPTZ DEFAULT NOW(),
    CONSTRAINT unique_match_seq UNIQUE (match_id, seq)
);

CREATE INDEX IF NOT EXISTS idx_events_match_seq ON match_events(match_id, seq DESC);
