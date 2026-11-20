-- キューとログ（VISION 2章「イベント」）
CREATE TABLE events (
  seq            INTEGER PRIMARY KEY,      -- 連番 = ID
  time_utc       INTEGER NOT NULL,         -- 絶対時刻（Unix ミリ秒）
  event_type     TEXT    NOT NULL,         -- イベントタイプ（ステージは保存せず、ここから引く）
  world_id       TEXT,
  persona_id     TEXT,
  status         TEXT    NOT NULL,         -- pending / processing / applied / skipped / rejected
  status_reason  TEXT,
  apply_no       INTEGER UNIQUE,           -- 適用番号
  batch_seq      INTEGER,
  cause_seq      INTEGER,
  payload        TEXT    NOT NULL,         -- JSON。決めた結果もここ
  guard          TEXT,                     -- JSON
  origin         TEXT    NOT NULL,         -- JSON
  trace          TEXT,                     -- JSON
  real_time_utc  INTEGER,                  -- 実時刻（送信完了、元の到着）
  enqueued_utc   INTEGER NOT NULL,         -- 積んだ時刻（遅れの計測用）
  applied_utc    INTEGER                   -- 適用した実時刻
);
CREATE INDEX ix_events_open    ON events(status) WHERE status IN ('pending', 'processing');   -- 起動時にキューを作る
CREATE INDEX ix_events_persona ON events(persona_id, apply_no);
CREATE INDEX ix_events_type    ON events(persona_id, event_type, time_utc);

-- 状態のテーブル（エンジンが書く。状態を戻すときは state の JSON だけを読む）
CREATE TABLE worlds (
  id TEXT PRIMARY KEY, def_id TEXT NOT NULL,
  status TEXT NOT NULL,                    -- alive / destroyed
  state TEXT NOT NULL,                     -- WorldState の全体の JSON
  created_seq INTEGER NOT NULL
);
CREATE TABLE personas (
  id TEXT PRIMARY KEY, def_id TEXT NOT NULL, world_id TEXT NOT NULL,
  status TEXT NOT NULL,                    -- alive / gap / destroyed
  state TEXT NOT NULL,                     -- PersonaState の全体の JSON
  created_seq INTEGER NOT NULL, updated_seq INTEGER NOT NULL
);

-- 写しのテーブル（ビューアと一覧用。書くが、状態を戻すときは読まない）
CREATE TABLE world_places (                -- 生えた場所
  world_id TEXT NOT NULL, place_id TEXT NOT NULL,
  name TEXT NOT NULL, tags TEXT NOT NULL, links TEXT NOT NULL, grp TEXT,
  status TEXT NOT NULL,                    -- candidate / adopted / retired
  created_seq INTEGER NOT NULL, updated_seq INTEGER NOT NULL,
  PRIMARY KEY (world_id, place_id)
);
CREATE TABLE world_terms (                 -- 変換の辞書の実行中の記録
  world_id TEXT NOT NULL, term TEXT NOT NULL,
  verdict TEXT NOT NULL, mapped TEXT, category TEXT, created_seq INTEGER NOT NULL, -- pass / convert / reject
  PRIMARY KEY (world_id, term)
);
CREATE TABLE incidents (                   -- 発生中の出来事
  id INTEGER PRIMARY KEY,                  -- incident.occurred の seq
  world_id TEXT NOT NULL, persona_id TEXT, def_id TEXT,
  effects TEXT NOT NULL,                   -- 解決済みの効果（移動時間、閉鎖、状態。対象の場所を含む。J-119）
  started_utc INTEGER NOT NULL, ends_utc INTEGER
);
CREATE TABLE inbox (                       -- 未処理の受信の写し（処理済みの行は消す）
  id INTEGER PRIMARY KEY,                  -- notification.arrived の seq
  persona_id TEXT NOT NULL, channel TEXT NOT NULL, kind TEXT NOT NULL, -- message / reaction / follow / other
  sender TEXT, audience TEXT NOT NULL, body TEXT NOT NULL, -- public / dm / no_memory
  original_arrived_utc INTEGER,
  status TEXT NOT NULL,                    -- unread（写しなので unread だけを書く）
  handled_seq INTEGER, outcome TEXT
);
CREATE TABLE outbound (                    -- 未送信の操作の写し（送信済みの行は消す）
  id INTEGER PRIMARY KEY,                  -- outbound.speech / operation の seq
  persona_id TEXT NOT NULL, channel TEXT NOT NULL, op_kind TEXT NOT NULL,
  is_speech INTEGER NOT NULL, payload TEXT NOT NULL,
  status TEXT NOT NULL,                    -- pending（写しなので pending だけを書く）
  external_id TEXT, result_seq INTEGER, sent_utc INTEGER, error TEXT
);
