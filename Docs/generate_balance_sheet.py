#!/usr/bin/env python3
"""Генератор Google-Sheets-листа баланса ShellGame.

Создаёт Docs/BalanceModel_formulas_en.csv (локаль US: разделитель ',', точка)
и Docs/BalanceModel_formulas_ru.csv (локаль RU: разделитель ';', запятая).

Структура листа:
  1. БАЗОВЫЕ ПАРАМЕТРЫ        — общие константы модели (столбец B)
  2. ПРЕСЕТЫ СЛОЖНОСТИ        — 4 строки × ручки
  3. РАСЧЁТ IN-RUN            — p -> D_base -> D_минибосс / D_босс по пресетам
  4. СРАВНЕНИЕ                — общий D + метрики всех пресетов (ссылки на под-таблицы)
  5. ПОД-ТАБЛИЦЫ              — читаемая таблица (14 колонок) на каждый пресет

Все значения по умолчанию совпадают с текущими конфигами игры
(«Средний» = текущий баланс).
"""

import re

# ---------------------------------------------------------------------------
#  Значения по умолчанию
# ---------------------------------------------------------------------------

BASE_PARAMS = [
    ("TrackingPbase", 0.5),
    ("TrackingPmin", 0.05),
    ("MinCorrectChanceFloor", 0.05),
    ("SwapCountBase", 4),
    ("SwapCountPerDifficulty", 0.111),
    ("SwapCountMin", 3),
    ("SwapCountMax", 9),
    ("EnemySwapCount", 5),
    ("BetweenSwapDelay", 0.1),
    ("ShuffleMoveDurationBase", 0.6),
    ("ShuffleMoveDurationMin", 0.24),
    ("ShuffleRoundReduction", 0.0043),
    ("ShuffleLevelReduction", 0.0028),
    ("EnemyShuffleSpeedMultiplier", 0.8),
    ("EnemyShuffleMoveDurationMin", 0.16),
    ("DecisionDelayBase", 1.7),
    ("DecisionDelayMin", 0.474),
    ("DecisionDelayK", 0.02724),
    ("DecisionSpeedMultiplier", 0.9),
    ("NoItemDecisionSpeedMultiplier", 0.75),
    ("RunRampGamma", 1),
    ("EncountersTotal", 55),
    ("SweepDmin", 0),
    ("SweepDmax", 60),
    ("SweepDstep", 1),
    ("EncountersCleared", 30),
    ("RoundsInEncounter", 2),
    ("RoundsPerEncounter", 6),
]

# Пресет: (Имя, HPигр, HPвраг, MinCorrect, MaxCorrect, TrackingK, DiffAtRunEnd,
#          МБHP×, БоссHP×, МБспайк×, МБспайк+, Боссспайк×, Боссспайк+)
PRESETS = [
    ("Простой", 6, 4, 0.18, 0.65, 0.0058, 40, 1.5, 1.7, 1.08, 1, 1.15, 2),
    ("Средний", 5, 5, 0.25, 0.75, 0.0089, 45, 1.6, 1.8, 1.12, 2, 1.20, 3),
    ("Сложный", 4, 6, 0.32, 0.85, 0.0110, 50, 1.7, 2.0, 1.16, 3, 1.28, 4),
    ("Мученик", 3, 7, 0.40, 0.92, 0.0130, 55, 1.8, 2.2, 1.20, 4, 1.35, 5),
]

PRESET_COLS = [
    "HPигр", "HPвраг", "MinCorrect", "MaxCorrect", "TrackingK", "DiffAtRunEnd",
    "МБHP×", "БоссHP×", "МБспайк×", "МБспайк+", "Боссспайк×", "Боссспайк+",
]

# Столбцы под-таблицы (индекс -> буква столбца = индекс+1)
SUB_HEADER = ["D", "Plose", "TrackFrac", "Pcorrect", "Perror", "Cups", "EMarkers",
              "SwapsPlayer", "SwapsEnemy", "ShufflePlayer", "ShuffleEnemy",
              "DecisionDelay", "EnemyTurnTime", "ExpectedHit"]
COL = {name: chr(ord('A') + i) for i, name in enumerate(SUB_HEADER)}

# Метрики сравнения и соответствующие им столбцы под-таблицы
CMP_METRICS = ["Plose", "TrackFrac", "Perror", "ExpectedHit", "EnemyTurnTime"]


def col_letter(one_based):
    s = ""
    n = one_based
    while n > 0:
        n, r = divmod(n - 1, 26)
        s = chr(ord('A') + r) + s
    return s


class Sheet:
    def __init__(self):
        self.rows = []
        self.ref = {}       # базовый параметр -> $B$n
        self.presets = []   # dict: name + ссылки на ячейки
        self.sub_row0 = []  # [preset][i] = абсолютная строка данных i

    def add(self, cells=None):
        self.rows.append(list(cells or []))
        return len(self.rows)

    # ------------------------------------------------------------------ build
    def build(self):
        self.add(["ShellGame — балансовая модель (все вычисления формулами)"])
        self.add()

        # 1. БАЗОВЫЕ ПАРАМЕТРЫ
        self.add(["БАЗОВЫЕ ПАРАМЕТРЫ (меняй значения в столбце B)", "Значение"])
        for name, val in BASE_PARAMS:
            r = self.add([name, val])
            self.ref[name] = f"$B${r}"
        self.add()

        # 2. ПРЕСЕТЫ
        self.add(["ПРЕСЕТЫ СЛОЖНОСТИ"])
        self.add(["Пресет"] + PRESET_COLS)
        for p in PRESETS:
            r = self.add(list(p))
            refs = {"name": p[0]}
            for i, col in enumerate(PRESET_COLS):
                refs[col] = f"${col_letter(i + 2)}${r}"
            self.presets.append(refs)
        self.add()

        # 3. РАСЧЁТ IN-RUN
        self.add(["РАСЧЁТ IN-RUN (общий)"])
        pr = self.add(["p (прогресс рана)",
                       f"={self.ref['EncountersCleared']}/{self.ref['EncountersTotal']}"])
        self.ref["p"] = f"$B${pr}"
        self.add(["Пресет", "p", "D_base", "D_минибосс", "D_босс"])
        for P in self.presets:
            dbase = f"{P['DiffAtRunEnd']}*{self.ref['p']}^{self.ref['RunRampGamma']}"
            self.add([
                P["name"],
                f"={self.ref['p']}",
                f"={dbase}",
                f"={dbase}*{P['МБспайк×']}+{P['МБспайк+']}",
                f"={dbase}*{P['Боссспайк×']}+{P['Боссспайк+']}",
            ])
        self.add()

        # Размеры блоков (в строках)
        sweep = self._sweep_count()
        cmp_len = 2 + sweep + 1          # заголовок + шапка + строки + пустая
        sub_len = 2 + sweep + 1
        first_sub = len(self.rows) + cmp_len + 1
        for k in range(len(self.presets)):
            self.sub_row0.append([first_sub + k * sub_len + 2 + i for i in range(sweep)])

        # 4. СРАВНЕНИЕ (ссылается на под-таблицы)
        self.add(["СРАВНЕНИЕ — метрики по пресетам на общей оси D (без спайков)"])
        header = ["D"]
        for P in self.presets:
            header += [f"{P['name']}:{m}" for m in CMP_METRICS]
            header += [f"{P['name']}:HPигр", f"{P['name']}:HPвраг"]
        self.add(header)
        for i in range(sweep):
            d = self._d_expr(i)
            row = [f"={d}"]
            for k, P in enumerate(self.presets):
                r = self.sub_row0[k][i]
                for m in CMP_METRICS:
                    row.append(f"={COL[m]}{r}")
                row += [f"={P['HPигр']}", f"={P['HPвраг']}"]
            self.add(row)
        self.add()

        # 5. ПОД-ТАБЛИЦЫ
        for k, P in enumerate(self.presets):
            self.add([f"ПОД-ТАБЛИЦА — {P['name']} (без спайка)"])
            self.add(SUB_HEADER)
            for i in range(sweep):
                r = self.add()  # резервируем строку
                self.rows[r - 1] = self._sub_row(r, i, P)
            self.add()

    # ------------------------------------------------------------- фрагменты
    def _sweep_count(self):
        return round((base_val("SweepDmax") - base_val("SweepDmin"))
                     / base_val("SweepDstep")) + 1

    def _d_expr(self, i):
        return f"({self.ref['SweepDmin']}+{i}*{self.ref['SweepDstep']})"

    def _sub_row(self, r, i, P):
        b = self.ref
        A, B, C, D, E, F, G, H, I, J, K, L, M, N = \
            (f"{c}{r}" for c in "ABCDEFGHIJKLMN")
        return [
            f"={self._d_expr(i)}",
            f"=MAX({b['TrackingPmin']},{b['TrackingPbase']}-{P['TrackingK']}*{A})",
            f"=(1-{B})^{b['EnemySwapCount']}",
            f"={P['MinCorrect']}+({P['MaxCorrect']}-{P['MinCorrect']})"
            f"*MIN(1,MAX(0,{A}/{P['DiffAtRunEnd']}))",
            f"=1-MIN(1,MAX({b['MinCorrectChanceFloor']},{D}))",
            f"=MIN(8,MAX(3,3+INT({A}/2.2)))",
            f"=MIN(0.15*{A},0.85)*(1+INT(({F}-2)/2))"
            f"+(1-MIN(0.15*{A},0.85))*MAX(1,INT(({F}-2)/2))",
            f"=MIN({b['SwapCountMax']},MAX({b['SwapCountMin']},"
            f"ROUND({b['SwapCountBase']}+{b['SwapCountPerDifficulty']}*MAX(0,{A}),0)))",
            f"=MAX(0,{b['EnemySwapCount']})",
            f"=MAX({b['ShuffleMoveDurationMin']},{b['ShuffleMoveDurationBase']}"
            f"-({b['ShuffleRoundReduction']}+{b['ShuffleLevelReduction']})*MAX(0,{A}))",
            f"=MAX(MAX(0.01,{b['EnemyShuffleMoveDurationMin']}),"
            f"{J}*MIN(1,MAX(0.05,{b['EnemyShuffleSpeedMultiplier']})))",
            f"=MAX({b['DecisionDelayMin']},{b['DecisionDelayBase']}"
            f"-{b['DecisionDelayK']}*{A})"
            f"*({b['DecisionSpeedMultiplier']}*{b['NoItemDecisionSpeedMultiplier']})",
            f"={I}*{K}+MAX(0,{I}-1)*{b['BetweenSwapDelay']}+{L}",
            f"={C}*((1-{E})+{E}*({G}/{F}))+(1-{C})*({G}/{F})",
        ]


def base_val(name):
    for n, v in BASE_PARAMS:
        if n == name:
            return v
    raise KeyError(name)


def to_csv(rows, sep, decimal_comma):
    out = []
    for row in rows:
        cells = []
        for c in row:
            c = "" if c is None else str(c)
            if c.startswith("="):
                if decimal_comma:
                    c = c.replace(",", ";")
                    c = re.sub(r"(?<=\d)\.(?=\d)", ",", c)
            elif decimal_comma and re.fullmatch(r"-?\d+\.\d+", c):
                c = c.replace(".", ",")
            if sep in c or '"' in c or "\n" in c:
                c = '"' + c.replace('"', '""') + '"'
            cells.append(c)
        out.append(sep.join(cells))
    return "\n".join(out) + "\n"


def main():
    sheet = Sheet()
    sheet.build()
    for suffix, sep, dc in [("_en.csv", ",", False), ("_ru.csv", ";", True)]:
        path = f"Docs/BalanceModel_formulas{suffix}"
        with open(path, "w", encoding="utf-8-sig", newline="") as f:
            f.write(to_csv(sheet.rows, sep, dc))
        print("wrote", path, "rows:", len(sheet.rows))


if __name__ == "__main__":
    main()
