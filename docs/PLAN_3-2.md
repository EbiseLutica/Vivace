# 3-2 公開 API と CLI：plan

## Context

IMPLEMENTATION 3-2 では、いまの生活（固定予定と Decider）を外から動かして読めるようにする。3-3 以降の PR は、ここで作る `vivace dry-run` を使って振る舞いの変化を見せる。3-1c までは main にマージ済み（#8、#12、#13）。

公開 API で型やメンバーの名前が外に見える前に、名前を見直す機会を取る。API と CLI を1つの PR にすると 1,500 行を超えそうなので、全体を3つに分ける。

- **3-2a**：名前の見直し（リネーム）
- **3-2b**：公開 API と投入の窓口
- **3-2c**：CLI の `validate` と `dry-run`

このファイルは `docs/PLAN_3-2.md` として残し、あとで参照する。

## ユーザーと合意したこと

1. 投入の窓口は、ルールの表にある型（`world.created`、`persona.created`）だけを検査して受け付ける。ほかの型は「未対応」で拒否する。各型の検査は、その型のルールを作る PR（3-3〜3-8）で足す。3-2b で作る検査は、そのときの書き方の手本になる。
2. `persona.created` の検査で、生成先のワールドが生きているかを見るとき、同じ時刻以前の `world.created` がキューにあれば、生成済みとみなす。これで連番が変わらず、既存のスナップショットもそのまま残る。
3. dry-run のレポートは、日ごと・人ごとの行動表と、末尾の件数の集計にする。
4. PR は 3-2a、3-2b、3-2c に分ける。

---

## 3-2a 名前の見直し

ブランチ：`impl/phase-3-2a-renames`

振る舞いを変えないリネームだけを入れる。

### 進め方

- どの名前をどう変えるかは、ユーザーが見直して決める
- 決まった対応を下の表に書き足し、「相談中」が空になってから実装に入る

### 決まっていること

型の名前：

- `Ingress` → `InjectionValidator`。名前空間は `Vivace.Core`
- `Definitions`（`Vivace.Core.Definitions` の record）→ `DefinitionPack`。`Set` だと `HashSet` の仲間に見えるため
- `EventOutcome` と `EventStatus` → `EventStatus` に1つにまとめる。名前空間は `Vivace.Core`
- `NewEvent` → `EventDraft`
- `LocalClock` → `LocalTimeHelper`
- `TimeText` → `TimeTextHelper`

メンバーの名前：

- `NewEvent`（`EventDraft`）の `World` / `Persona` → `WorldId` / `PersonaId`（`SimEvent` に揃える）
- `BatchKey` → `BatchSeq` に揃える（シグナル、エンジンの中の `Output` など）

シグナル：

- `EngineSignal` とその派生を、名前空間 `Vivace.Core.Signals` に移す
- シグナルの型名は、すべて `Signal` で終える：`StartDecider` → `StartDeciderSignal`、`StartPlanner` → `StartPlannerSignal`、`CancelHandler` → `CancelHandlerSignal`

- `Position` → `ApplyCursor`。`SimState` のプロパティも `Position` → `Cursor`（`IRandom.Position` と区別する）
- `InboxOutcome` は record のまま残す。enum の `InboxResult` を改名する（名前は相談中を参照）

名前空間とフォルダ：

- 名前空間とフォルダを一致させる。ファイルも、名前空間に合わせて置き直す
- `Vivace.Core.Engine` の型を `Vivace.Core` に移す
- `Vivace.Core.Rules` の型を `Vivace.Core` に移す
- `Vivace.Core.Rules.EventRules` → `Vivace.Core.EventRules`
- 前からずれていた2か所も揃える：`Definitions/Model/` → `Vivace.Core.Definitions.Model`、`Domain/State/` → `Vivace.Core.Domain.State`
- 名前空間は、そのグループの性質を表すものにする。子の名前空間には、基底の型とその派生だけを置く。グループをまたいで使う型は、親の名前空間に置く。1つの型からしか使わない型は、その型の入れ子にする
- `Vivace.Core.Events.Payloads`：`EventPayload` とその派生だけ
- `Vivace.Core.Events.Guards`：`GuardCheck` とその派生だけ
- `Vivace.Core.Events` に残すもの：`Guard`、`RejectAction`、`RngDraws`、`DecisionReason`。どれも、複数のグループから使われている
- 入れ子にするもの：
  - `InboxOutcome` と、その enum の `InboxOutcomeKind`（旧 `InboxResult`）→ `InboxHandledPayload` の入れ子
  - `DisruptionChoice` → `BlockRespondedPayload` の入れ子
  - `OutboundPostKind` → `OutboundSpeechPayload` の入れ子
  - `PostSkipReason` → `PostSkippedPayload` の入れ子
  - `VitalAdjustKind` → `VitalAdjustedPayload` の入れ子

ファクトリ：

- `Ingress` の `WorldCreated` と `PersonaCreated` を、`EventDraft` の static のファクトリにする

3-2b に関わること：

- 購読では `AppliedEvent` を作らず、`LoggedEvent`（`Status = Applied`）を流す
- `InjectRequest` は作る。ファクトリが `EventDraft` に移ったので、`InjectRequest` と `EventDraft` の関係は 3-2b で決める

保留：

- 公開範囲（必要になってから public にする）は、今は決めない。バージョン 0 で、API を変えてよい段階のため

CLAUDE.md：

- 「1ファイルに1つの型」に、入れ子の型は親のファイルに置く、という例外を書き足す。入れ子にするコミットと同じコミットに入れる

### 相談中

（なし）

### 完了条件

- 既存のテストがすべて通る
- `.verified.txt` の差分がない
- 設計書（D4 4.6 の部品の表など）とコードの名前が一致している

---

## 3-2b 公開 API と投入の窓口

ブランチ：`impl/phase-3-2b-public-api`（3-2a のマージ後に `main` から切る）

### 作るもの

**投入の要求と結果**（名前空間 `Vivace.Core`）

- `InjectRequest`：投入するイベントと、時刻の指定を持つ
  - 時刻は、指定するか「いま」にする。「いま」は、受け付けた時点の時計の時刻を使う
  - 形は `InjectRequest(EventType, Payload, At?, Origin)`。`EventDraft` には依存させない（要求 → 検査 → 下書き、の一方向）
  - `EventDraft` の `WorldCreated` / `PersonaCreated` は、`InjectRequest` の static ファクトリ（`at` は省略可）に移す
  - `InjectionValidator` が時刻を決め、`EventDraft` を作る
- `InjectResult`：受け付けたら連番、拒否なら理由を持つ。`Accepted` も持つ

**`InjectionValidator`**（いまの `Ingress` を置き換える）

- 入力は、定義、いまの状態、未適用のイベント、要求、いまの時刻
- 出力は、拒否の理由か、キューに入れる `EventDraft`
- 確かめること：
  - 過去の時刻は不可
  - 未対応の型は拒否する
  - `world.created`：定義があること
  - `persona.created`：定義があること。生成先のワールドが生きているか、同じ時刻以前の `world.created` がキューにあること
- 型ごとの検査は、その型のルール（`EventRule`）に、投入の検査のメソッドとして持たせる。既定は「投入できない」で、`world.created` と `persona.created` のルールだけが上書きする
- `InjectionValidator` は、共通の検査（過去の時刻、ルールがあるか）をしてから、ルールの検査を呼ぶだけの薄い入口にする
- 3-3 以降で型を足すときは、ルールのクラス1つに、適用と投入の検査を並べて書く
- 変えないもの：
  - `EventTypes` の定数の表：定数は1か所で管理する
  - guard の評価：`EventApplier.CheckGuard` に置いたままにする。guard はイベント型に紐づかず、検査の種類も増える見込みがないため

**`SimEngine`**

- 同期の `Inject` を削除し、`Task<InjectResult> InjectAsync(InjectRequest, CancellationToken)` に置き換える
  - コマンドは `Channel` に入れる
  - `RunUntilAsync` の外にいるときは、`System.Threading.Lock` の下でその場で処理する
  - `RunUntilAsync` の中にいるときは、ループの各周の先頭でコマンドを取り出す。ループが待つ場面（`WhenAny`）では、コマンドが届いたときにも起きる
  - 検査は、取り出した時点の状態とキューで行う
  - `RunUntilAsync` を抜けるとき、残ったコマンドをすべて処理してから、「ループの外」の印に戻す
- `IAsyncEnumerable<LoggedEvent> Subscribe(CancellationToken)`
  - 購読ごとに容量つきの `Channel` を持つ
  - 適用したイベント（`Status = Applied`）だけを、`CommitAsync` のあとで流す
  - 容量があふれた購読は、例外で完了させて切断する。エンジンは止めない
- `View`：いまの `SimState` を返す（`State` の別名として足す）

**`VivaceEngine` とビルダー**

- `VivaceEngine.Create()` は `VivaceEngineBuilder` を返す
- ビルダーのメソッド：
  - `UseDefinitions(string dir)` と `UseDefinitions(Definitions)`
  - `UseStorage`、`UseClock`、`UseDecider`、`UseHolidays`、`UseRandom`
  - `DryRun()`：チャンネルはまだないので、印だけ持つ
- `Build()` は、定義のエラー、保存先なし、時計なしで例外にする（`SystemClock` は段階5 で作る）
- `VivaceEngine` の公開メンバー：`InjectAsync`、`View`、`Subscribe`、`RunUntilAsync`、`DisposeAsync`。中で `SimEngine` に委ねる
- `UseLlm`、`AddPlugin`、`UseLoggerFactory`、`StartAsync`、`Status`、`Events`、`ReloadDefinitionsAsync` などは作らない

**`Scenario`**

- `Create` を `CreateAsync` にして、`InjectAsync` を使う
- テストの呼び出し側を合わせる（`EngineFailureTests` ほか）
- 投入の順と連番が変わらないので、`.verified.txt` の差分は出ない

### テスト

- 窓口の検査：
  - 過去の時刻
  - 未対応の型
  - 定義がない
  - ワールドがない
  - キューの `world.created` で通る
  - キューの `world.created` が要求より後の時刻なら拒否
- 実行中の `InjectAsync`：kaori だけで始め、`Subscribe` で最初の `day.started` を見たら、shiori を `At = null` で投入する。受け付けられて、その連番の `persona.created` が投入した時刻以降に適用されることを確かめる
- `Subscribe` で流れた順と、`Log` の Applied の順（連番と ApplyNo）が一致すること
- 遅い購読者が切断され、エンジンは最後まで回ること
- ビルダーが、時計なし、保存先なし、定義のエラーで例外を投げること
- 既存のテストがすべて通り、`.verified.txt` の差分がないこと

### 設計書

- J-154〜：
  - 窓口は、ルールの表にある型だけを受け付ける
  - キューにある生成も、生成済みとみなす
  - `View` は `SimState` を返す（D4 の `StateView` は、今は `SimState` そのもの）
  - 購読の容量の値（調整前提）
  - ビルダーで、時計と保存先を必須にする
- 本文に書く場所は、D2 2.8 と D4 4.6。`index.html` の「枠の外の判断」にも載せ、「合意」の印を付ける
- IMPLEMENTATION.html の 3-2 を 3-2a、3-2b、3-2c に分ける。「`Ingress` の全体」の項目は、合意 1 の形に書き直す

### 範囲外

ルールのない型の検査、`SystemClock`、デーモン、LLM、プラグイン、チャンネル。

---

## 3-2c CLI の validate と dry-run

ブランチ：`impl/phase-3-2c-cli`（3-2b のマージ後に `main` から切る）

### 作るもの

**CLI の構成**

- `Vivace.Cli` のコマンドの中身は、テストできるクラスに分ける
  - `ValidateCommand.Run(string dir, TextWriter output) -> int`
  - `DryRunCommand.RunAsync(...)`
  - `Program.cs` は、System.CommandLine の配線だけにする
- `Vivace.Cli` は `Vivace.Testing` を参照する（`InMemoryStorage` を使うため。CLAUDE.md の想定どおり）

**`vivace validate <dir>`**

- `DefinitionLoader.LoadDirectory` で読み、問題を1行ずつ `DefinitionIssue.ToString()` で出す（並びは読み込みの結果の順のまま）
- 最後に「エラー N 件、警告 M 件」と出す
- エラーがあれば 1 で終わり、なければ 0 で終わる

**`vivace dry-run <dir> --days N [--seed S] [--start ISO8601]`**

- 既定の開始は `2026-11-01T23:00+09:00`、既定の種は `20261101`。どちらも Scenario と同じ値で、J に載せる
- `VivaceEngine` を、`VirtualClock`、`InMemoryStorage`、`DryRun()` で組む
- ワールドの定義を ID の序数順に、すべて生成する
- ペルソナの定義を ID の序数順に、それぞれの `home` に生成する
- 開始 + N 日まで回す

**`DryRunReport`**

- 保存先の記録から、日ごと（ワールドの現地の日付）、人ごと（ID の序数順）に行動表を作る
- 行の形：
  - `07:00-07:30 [予定] eat_breakfast @kaori_room`
  - `08:10-08:25 移動 kaori_room → school`
- 終わりは、同じ人の `action.ended` か `travel.arrived` から取る
- 曜日は配列で引く。カルチャに依る書式は使わない
- 末尾に、適用、捨てた、却下の件数を出す

### テスト

- validate：
  - テスト用の定義（`TestWorld.Files()` を一時フォルダに書き出す）で、問題 0 件、終了コード 0
  - 壊した定義で、位置つきのエラーと終了コード 1
- dry-run：テスト用の定義で 2 日回したレポートのスナップショット（新しい `.verified.txt`。中身を読んで確かめる）
- `vivace dry-run --days 7` を手で回し、2人の1週間が読めること

### 範囲外

デーモン、`status` などのデーモンと話すコマンド、`report`、`SystemClock`、`ScaledClock`。

---

## 進め方（CLAUDE.md の「実装の進め方」）

各 PR（3-2a、3-2b、3-2c）で、次のループを回す。

1. `main` を最新にして、その PR のブランチを切る（`fix/issue-5-lanes` は #12 でマージ済み）
2. Haiku のサブエージェントに、下の依頼文で実装を頼む
3. Opus のサブエージェントに、レビューを頼む（設計書との照合、再現、変異。重大度を付け、最後に `APPROVED` か `CHANGES_REQUESTED` を返す）
4. 承認まで 2 と 3 を繰り返す。2回目からは、同じエージェントに `SendMessage` で続きを送る
5. メインも `dotnet build`（警告 0）と `dotnet test` を回し、差分を読んでから PR を作る。PR の本文には J の番号と「マージをもって合意とする」を書く
6. マージされたら、次の PR に進む

### Haiku への依頼文の型（原始人モード）

```
Vivace 3-2x 実装。リポ E:\Repos\Vivace、ブランチ impl/phase-3-2x-…（作成済み）。
必読: CLAUDE.md（全部従う）、docs/PLAN_3-2.md の 3-2x 節、docs/IMPLEMENTATION.html #phase3-rules と #phase3-2、
参照の章（3-2b: docs/design/4-components.html #api、2-events.html #injection、1-execution.html #clock。
3-2c: 7-operations.html #validation #cli）。
制約:
- 1ファイル1型。ドキュメントコメントは役割だけ。設計書の番号は書かない
- 決定性の規則を守る。錠は System.Threading.Lock
- 既存の .verified.txt は変えない。変わったら原因を直す（更新しない）
- 期待値は手計算できる小例。実装から作らない
- 関心ごとにコミット。メッセージは日本語、feat:/refactor:/test:/docs: の形。末尾に
  Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
- 各コミットで dotnet build（警告0）と dotnet test が通ること
- プッシュしない。PR を作らない
範囲外・完了条件: PLAN_3-2.md の該当節どおり。
報告: 原始人モード。コミットの一覧、各テストの狙い、迷った点、未完了の点。
```

## 確かめること

- `dotnet build`（警告 0）と `dotnet test` がすべて通ること
- `git diff main -- '*.verified.txt'` が、3-2a と 3-2b では空であること。3-2c では、新しいスナップショットの追加だけであること
- 3-2c のあとに `dotnet run --project Vivace.Cli -- dry-run Vivace.Testing/Defs --days 7` を回し、1週間の表を読むこと。`validate` を、テスト用の定義と壊した定義で回すこと
- 差分が、それぞれの PR で 1,500 行以内であること

## この plan の承認のあとにすること

- このファイルを `docs/PLAN_3-2.md` にコピーする（コミットは頼まれたときだけ）
- 3-2a の名前の見直しは、ユーザーの見直しを待つ
