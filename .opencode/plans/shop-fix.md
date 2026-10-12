# Починка магазина (ShellGame)

Контекст: магазин выбирается корректно (`ResolveEncounter ... result=Shop`), но падает на `SpawnSellMarker` и дальше не работает. Язык правок — минимальные, без переписывания.

## Найденные баги

1. **NRE в `SpawnSellMarker`** — `ShopEncounterController.cs:176`: `new GameObject("SellMarker")` без коллайдера; `ItemPickupView` имеет `[RequireComponent(typeof(Collider))]` (абстрактный → Unity не автодобавляет) → `AddComponent` = `null` → NRE на строке 181 → `InitializeShop` прерывается (нет Exit-кнопки, бесплатного предмета, диалога).
2. **Предметы не кликабельны** — в магазине `GameManager._state == Idle` (`StartRound` выходит рано, `_roundGenerator == null`) → `IsItemUsageAllowedNow` (GameManager.cs:601) всегда false; `RoundInputSystem._itemInteractionEnabled` никогда не включается.
3. **Выход из магазина мёртв** — `RunManager.cs:249-261`: отписка от `OnExitRequested` сразу после `EnterRoutine`, который для Shop завершается за ~1 кадр.
4. **Exit-кнопка не ловит клики** — `IPointerClickHandler` без `PhysicsRaycaster` ни в одной сцене (есть только `GraphicRaycaster` + `InputSystemUIInputModule`).
5. **Sell-режим не закрывается** — `ItemPickupView.TryUse:74` в sell-режиме весь клик уводит в `OnSellModeClick`, `SellMarkerBehavior.OnUsed` (вызывающий `ExitSellMode`) недостижим.
6. **Крестик невидим** — `SellMarker.asset`: `WorldPrefab: {fileID: 0}`.
7. Мелочь: `ExitButtonView._originalScale` не инициализируется; `_buttonVisual` в префабе = null.

## Шаги реализации

### 1. Префаб креста (согласовано с пользователем)
- Создать `Assets/Prefabs/Propses/SellMarkerCross.prefab` + `.meta` (guid: `2fc172977b4243c4a86c3c848b429e62`):
  - Root `SellMarkerCross`, layer 6: `BoxCollider` size {0.32, 0.08, 0.32}, без скриптов.
  - Дети `ArmA`/`ArmB`: куб mesh `10202`, scale {0.3, 0.03, 0.05}, rotation Y ±45° (`y: ±0.3826834, w: 0.9238795`), материал `M_RedTest` (guid `9d9c147294941ed478038ed13856c199`).
- `Assets/Resources/Configs/Items/SellMarker.asset`: `WorldPrefab: {fileID: 100002, guid: 2fc172977b4243c4a86c3c848b429e62, type: 3}`.
  - Уточнение: main object fileID в prefab без префаб-инстансов — задать корректный fileID корневого GameObject (если 100002 не подойдёт — сверить при импорте; иначе инстанцировать по ссылке на префаб из контроллера через `Resources`/поле).

### 2. `ShopEncounterController.cs`
- `SpawnSellMarker()` (172-186): если `WorldPrefab != null` — `Instantiate(WorldPrefab, pos, rot, transform)`, затем `GetComponent<ItemPickupView>() ?? AddComponent`; иначе fallback: `new GameObject` + `AddComponent<BoxCollider>()` (size 0.35) до `AddComponent<ItemPickupView>`. Guard: при неудаче — `Debug.LogError` и `return` (не бросать), порядок: **сначала `SpawnExitButton()`, потом `SpawnSellMarker()`** в `InitializeShop` (строки 105/108).
- `OnPlayerItemSellClicked` (354): `if (pickup != null && pickup.GetComponent<SellMarkerBehavior>() != null) { ExitSellMode(); return; }` дальше существующая логика.

### 3. `GameManager.cs`
- Поле `private bool _shopMode;`
- `BeginEncounter` (355): после определения контекста — `bool isShop = ShellGame.Run.RunManager.Instance?.CurrentEncounterKind == ShellGame.Run.EncounterKind.Shop; if (isShop) EnterShopMode();`
- `public void EnterShopMode()`: `_shopMode = true; _inputSystem?.SetEnabled(true); _inputSystem?.SetItemInteractionEnabled(true);`
- `public void EndShopMode()`: `_shopMode = false; _inputSystem?.SetEnabled(false); _inputSystem?.SetItemInteractionEnabled(false);`
- `IsItemUsageAllowedNow` (601): после `item == null` — `if (_shopMode) return true;`
- `StopEncounter` (262): добавить `_shopMode = false;`

### 4. `EncounterHost.cs`
- `Awake` (59): после `_interactCamera == null → Camera.main`: `if (_interactCamera != null && _interactCamera.GetComponent<UnityEngine.EventSystems.PhysicsRaycaster>() == null) _interactCamera.gameObject.AddComponent<UnityEngine.EventSystems.PhysicsRaycaster>();`
- `ExitRoutine` (190): в ветке `if (_shopRig != null)` перед `Destroy(_shopRig.gameObject)` — `_gameManager.EndShopMode();`

### 5. `RunManager.cs` (249-261)
- Убрать подписку/отписку вокруг `yield return _host.EnterRoutine(def)` для Shop.
- `OnShopExitRequested`: `if (State != RunState.EncounterActive) return; if (_host != null) _host.OnExitRequested -= OnShopExitRequested; StartCoroutine(ReturnToMapRoutine());`
- Подписку `_host.OnExitRequested += OnShopExitRequested` оставить перед `EnterRoutine` (как сейчас), но отписку из корутины убрать для Shop (для не-Shop оставить как было, их подписки нет — фактически просто удалить отписку из корутины и перенести в обработчик).

### 6. `ExitButtonView.cs`
- `Awake`: `_buttonVisual ??= transform; _originalScale = _buttonVisual.localScale;` (после поиска `_host`).

## Верификация
- guid нового префаба уникален (`2fc172977b4243c4a86c3c848b429e62` — сгенерирован).
- Проверить YAML по образцу `ExitButton.prefab`.
- Компиляция: `dotnet build`/открытие в Unity; затем ручной прогон: заход в магазин → ховер/покупка предмета → режим продажи → выход из режима кликом по крестику → кнопка «Продолжить путь» возвращает на карту.
