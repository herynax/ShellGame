# Баланс ShellGame — модель рана

Документ сгенерирован окном **ShellGame → Balance** (`Assets/Editor/Balance/BalanceWindow.cs`).
Формулы совпадают с `Evaluate*`-методами конфигов — это единственный источник истины, дублирования нет.

## Структура рана

- 5 карт × 10 энкаунтеров = 50 обычных боёв
- 4 минибосса
- 1 финальный босс (после его смерти ран заканчивается)
- Итого **55 энкаунтеров** (`EncountersTotal` в `RunDifficultyConfig.asset`)

## Ось сложности D

```
p        = EncountersClearedInRun / EncountersTotal        # прогресс рана 0..1
D_base   = DifficultyAtRunEnd * p^RunRampGamma             # 45 * p
D        = D_base + IntraEncounterRamp * (Rounds / RoundsPerEncounter)   # +до 3 внутри боя
```
На входе в `ObserveMarkers` особый враг применяет свой спайк:

```
D_врага = D * EnemyAIConfig.DifficultyMultiplier + DifficultyFlatBonus
```

| Враг | DifficultyMultiplier | DifficultyFlatBonus | HealthMultiplier | Ассеты |
|---|---|---|---|---|
| Обычный | 1.00 | +0 | 1.0 | `EnemyAIConfig.asset` |
| Минибосс | 1.12 | +2 | 1.6 (HP 8) | `EnemyAIConfig_MiniBoss.asset` + `Encounters/MiniBoss.asset` |
| Финальный босс | 1.20 | +3 | 1.8 (HP 9) | `EnemyAIConfig_Boss.asset` + `Encounters/Boss.asset` |

> Заготовки созданы, но **не подключены**: `EncounterCatalog` — явный список, поэтому минибосс/босс не появятся в ране, пока не добавишь их туда. Структура карты/рана не тронута.
> HP-множитель энкаунтера живёт в `EnemyAIConfig.HealthMultiplier` и применяется в `GameManager.EnsureHealthInitializedForLevel` поверх базовой HP-кривой.

## Ключевые значения

| Параметр | Где | Значение |
|---|---|---|
| DifficultyAtRunEnd | RunDifficultyConfig | 45 |
| RunRampGamma (ровная линия) | RunDifficultyConfig | 1.0 |
| IntraEncounterRamp | RunDifficultyConfig | 3 |
| DifficultySaturation | RunDifficultyConfig | 60 |
| Tracking Pbase / Pmin / k | EnemyAIConfig | 0.5 / 0.05 / 0.0089 |
| Correct 25%→75% на D | EnemyAIConfig | D=45 |
| DecisionDelay 1.7→0.474 | EnemyAIConfig | k=0.02724 |
| HealthPenaltyMaxReduction | EnemyAIConfig | 0.3 |
| Shuffle base/min/снижения | ShellConfig | 0.6 / 0.24 / 0.0043+0.0028 |
| EnemyShuffle ×/min | ShellConfig | 0.8 / 0.16 |
| SwapCount base/perD/min/max | ShellConfig | 4 / 0.111 / 3 / 9 |
| EnemySwapCount | ShellConfig | 5 (фиксировано) |
| BetweenSwapDelay | ShellConfig | 0.1 |
| HP игрока | HealthProgressionConfig | 5 (статично) |
| HP обычного врага | HealthProgressionConfig | 5 (статично) |
| HP минибосса / босса | EnemyAIConfig ×1.6 / ×1.8 | 8 / 9 |

## Пресеты сложности

Четыре пресета задают рамки рана. «Средний» = текущий баланс выше; остальные — сдвиги по HP и точности.

| Пресет | HP игрока | HP врага | MinCorrect | MaxCorrect | TrackingK | DifficultyAtRunEnd | МБ HP× | Босс HP× | МБ спайк | Босс спайк |
|---|---|---|---|---|---|---|---|---|---|---|
| Простой | 6 | 4 | 0.18 | 0.65 | 0.0058 | 40 | 1.5 | 1.7 | ×1.08 / +1 | ×1.15 / +2 |
| Средний | 5 | 5 | 0.25 | 0.75 | 0.0089 | 45 | 1.6 | 1.8 | ×1.12 / +2 | ×1.20 / +3 |
| Сложный | 4 | 6 | 0.32 | 0.85 | 0.0110 | 50 | 1.7 | 2.0 | ×1.16 / +3 | ×1.28 / +4 |
| Мученик | 3 | 7 | 0.40 | 0.92 | 0.0130 | 55 | 1.8 | 2.2 | ×1.20 / +4 | ×1.35 / +5 |

Замечания:
- `TrackingK` — скорость, с которой враг забывает цель: **больше k → ниже `Plose` → выше `TrackFrac` → враг бьёт точнее = сложнее**. Поэтому k растёт от «Простоя» к «Мученику».
- `DiffAtRunEnd` одновременно нормирует точность врага до `MaxCorrect` к концу рана (точка насыщения = конец рана).
- HP статичны внутри пресета (кривая `HealthProgressionConfig` — фолбэк, если пресет не задан); особые бои получают HP через множитель энкаунтера из пресета.

## Почему обмены не растут у врага

Точность врага ограничена долей сохранённой цели `T = (1 - Plose)^n`.
Если растить `n` у врага, `T` падает и враг становится **слабее** — две оси сложности начали бы бороться.
Поэтому число обменов растёт только у игрока (нагрузка на память), у врага фиксировано на 5.

## Про время хода врага

Все обмены идут **параллельно** двумя чашками (`ShuffleSystem.PerformNextSwap`), поэтому:
```
EnemyTurnTime = n*ShuffleEnemy + (n-1)*BetweenSwapDelay + DecisionDelay(D, 0.9, 0.75)
```
Множители `0.9` (`_decisionSpeedMultiplier`) и `0.75` (`_noItemDecisionSpeedMultiplier`) живут на `EnemyAIController`
и раньше в таблицу не попадали — теперь учтены.

## Ключевые точки

| D | Plose | TrackFrac | Pcorrect | Cups | E[M] | EnemyTurn, с | E[hit] |
|---|---|---|---|---|---|---|---|
| 0 | 0.500 | 0.031 | 0.250 | 3 | 1.00 | 3.95 | 0.339 |
| 5 | 0.456 | 0.048 | 0.306 | 5 | 1.75 | 3.71 | 0.360 |
| 10 | 0.411 | 0.071 | 0.361 | 7 | 2.85 | 3.48 | 0.422 |
| 15 | 0.366 | 0.102 | 0.417 | 8 | 3.85 | 3.25 | 0.503 |
| 20 | 0.322 | 0.143 | 0.472 | 8 | 3.85 | 3.01 | 0.516 |
| 25 | 0.277 | 0.197 | 0.528 | 8 | 3.85 | 2.78 | 0.535 |
| 30 | 0.233 | 0.265 | 0.583 | 8 | 3.85 | 2.54 | 0.562 |
| 35 | 0.189 | 0.352 | 0.639 | 8 | 3.85 | 2.31 | 0.598 |
| 40 | 0.144 | 0.460 | 0.694 | 8 | 3.85 | 2.08 | 0.647 |
| 45 | 0.099 | 0.592 | 0.750 | 8 | 3.85 | 1.84 | 0.712 |
| 50 | 0.055 | 0.754 | 0.750 | 8 | 3.85 | 1.70 | 0.774 |
| 55 | 0.050 | 0.774 | 0.750 | 8 | 3.85 | 1.56 | 0.782 |
| 60 | 0.050 | 0.774 | 0.750 | 8 | 3.85 | 1.52 | 0.782 |

## Проверка симуляцией рана

Симуляция (точность игрока 0.70→0.90, **без** предметов и лечения); HP обычного врага статично 5, без «туши»:

- обычный бой: ~5–7 ходов, ~89% побед, игрок заканчивает в среднем с ~2 HP из 5 — «на волоске»;
- минибосс (HP 8): ~32% побед;
- финальный босс (HP 9): ~4% побед — без предметов почти смертелен, с предметами реален.

HP врага больше **не растёт** по ходу рана: сложность наращивается точностью/памятью (`Plose`, `Pcorrect`), а не жирностью. Особые бои получают HP через множитель энкаунтера.

## Файлы

- `Docs/BalanceModel.tsv` — диапазон D=0..60 с шагом 1.0, **готовые значения** для вставки в Google Sheets (Ctrl+V) и построения графиков.
- `Docs/BalanceModel_formulas_en.csv` / `_ru.csv` — **с формулами**, структура: «Базовые параметры» → «Пресеты сложности» → «Расчёт in-run» → «Сравнение» (метрики всех 4 пресетов на общей оси D) → «Под-таблицы» (полная таблица на каждый пресет). `_en` — локаль с точкой `.` (US), `_ru` — локаль с запятой `,` и разделителем `;` (Россия). Импорт: Файл → Импорт → выбрать файл (включить «Преобразовывать текст в числа, даты и формулы»).
- `Docs/generate_balance_sheet.py` — генератор обоих CSV из значений по умолчанию (`python3 Docs/generate_balance_sheet.py`); меняешь константы/пресеты вверху файла — таблица пересобирается.
- Окно `ShellGame → Balance` — live-правки конфигов, пересчёт графиков в тот же кадр, кнопки Copy/Export TSV и MD.
- `EnemyAIConfig.asset` — обычная линия; `EnemyAIConfig_MiniBoss.asset` / `EnemyAIConfig_Boss.asset` — спайки и HP особых боёв.
- `Encounters/MiniBoss.asset` / `Encounters/Boss.asset` — заготовки энкаунтеров; подключены в `EncounterCatalog` (Kind `MiniBoss`/`Boss`).
- `Assets/Scripts/Gameplay/Difficulty/DifficultyPreset.cs` / `DifficultyCatalog.cs` — модель пресета и каталог (`Resources/Configs/Difficulty/DifficultyCatalog`).
- `Assets/Resources/Configs/Difficulty/Difficulty_{Easy,Medium,Hard,Martyr}.asset` — значения пресетов; `DifficultyCatalog.asset` — список + `DefaultPresetId=Medium` (сложность по умолчанию не запоминается между запусками).
- UI выбора: панель в главном меню (`MainMenuController.difficultyPanel`), кнопки пресетов зовут `SelectDifficulty("Easy"|"Medium"|"Hard"|"Martyr")`. Рестарт из паузы сохраняет выбранный пресет; «Продолжить попытку» восстанавливает его из чекпоинта.

## Ограничения окружения

- `matplotlib` не установлен — графики в окне рисуются через `Texture2D`, экспорт графиков в PNG не делается.
- Прямое создание Google Doc невозможно — используем TSV + вставку через `Ctrl+V`.

