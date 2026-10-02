using System;
using System.Globalization;
using System.IO;
using System.Text;
using ShellGame.AI;
using ShellGame.Gameplay;
using ShellGame.Health;
using ShellGame.Shells;
using UnityEditor;
using UnityEngine;

namespace ShellGame.BalanceTools
{
    /// <summary>
    /// Живое окно баланса: ShellGame → Balance.
    ///
    /// Слева — редактируемые конфиги (правки сразу применяются к ассетам через
    /// SerializedObject, перерисовка в тот же кадр). Справа/ниже — графики и
    /// таблица, посчитанные ТЕМИ ЖЕ формулами, что и игра (Evaluate*-методы
    /// конфигов). Кнопки Copy/Export TSV дают готовый диапазон для вставки в
    /// Google Sheets — графики там строятся из того же диапазона.
    ///
    /// Зависимостей нет: графики рисуются Texture2D, matplotlib не нужен.
    /// </summary>
    public sealed class BalanceWindow : EditorWindow
    {
        private const string EnemyConfigPath = "Configs/EnemyAIConfig";
        private const string ShellConfigPath = "Configs/ShellConfig";
        private const string HealthConfigPath = "Configs/HealthProgressionConfig";
        private const string RunConfigPath = RunDifficultyConfig.ResourcesPath;

        private EnemyAIConfig _enemy;
        private ShellConfig _shell;
        private HealthProgressionConfig _health;
        private RunDifficultyConfig _run;

        private SerializedObject _enemySo;
        private SerializedObject _shellSo;
        private SerializedObject _healthSo;
        private SerializedObject _runSo;

        private Vector2 _scroll;

        // Настройки графика
        private float _dMin;
        private float _dMax = 60f;
        private int _sampleCount = 121;
        private float _spikeMultiplier = 1f;
        private float _spikeFlatBonus;

        private bool _showConfigs = true;
        private bool _showEnemy = true;
        private bool _showShell = true;
        private bool _showHealth;
        private bool _showRun = true;

        private Texture2D _graphTex;
        private int _graphW = 700;
        private int _graphH = 180;

        private GUIStyle _titleStyle;

        [MenuItem("ShellGame/Balance")]
        public static void Open()
        {
            var window = GetWindow<BalanceWindow>("Balance");
            window.minSize = new Vector2(760f, 560f);
            window.Reload();
            window.Show();
        }

        private void OnEnable() => Reload();

        private void Reload()
        {
            _enemy = Resources.Load<EnemyAIConfig>(EnemyConfigPath);
            _shell = Resources.Load<ShellConfig>(ShellConfigPath);
            _health = Resources.Load<HealthProgressionConfig>(HealthConfigPath);
            _run = Resources.Load<RunDifficultyConfig>(RunConfigPath);

            _enemySo = _enemy != null ? new SerializedObject(_enemy) : null;
            _shellSo = _shell != null ? new SerializedObject(_shell) : null;
            _healthSo = _health != null ? new SerializedObject(_health) : null;
            _runSo = _run != null ? new SerializedObject(_run) : null;

            if (_run != null)
                _dMax = Mathf.Max(10f, _run.DifficultySaturation);
        }

        private void OnDisable()
        {
            if (_graphTex != null)
            {
                DestroyImmediate(_graphTex);
                _graphTex = null;
            }
        }

        // ============================================================
        //  Модель — те же формулы, что и в игре.
        // ============================================================

        private struct Sample
        {
            public float D;
            public float Plose;
            public float TrackedFraction;
            public float Pcorrect;
            public float Perror;
            public int Cups;
            public float ExpectedMarkers;
            public int SwapsPlayer;
            public int SwapsEnemy;
            public float ShufflePlayer;
            public float ShuffleEnemy;
            public float DecisionDelay;
            public float EnemyTurnTime;
            public float ExpectedHit;
        }

        private Sample Evaluate(float D)
        {
            var s = new Sample { D = D };

            if (_enemy != null)
            {
                s.Plose = _enemy.EvaluateTrackingLossProbability(D);
                s.Pcorrect = _enemy.EvaluateCorrectChoiceProbability(D);
                s.Perror = _enemy.EvaluateDecisionErrorProbability(D, 1f, false);
                s.DecisionDelay = _enemy.EvaluateDecisionDelay(D, 0.9f, 0.75f);
            }
            else
            {
                s.Plose = 0f;
                s.Pcorrect = 0f;
                s.Perror = 1f;
                s.DecisionDelay = 0f;
            }

            s.Cups = RoundProgressionConfig.EvaluateCupCount(D);
            s.ExpectedMarkers = RoundProgressionConfig.EvaluateExpectedMarkerCount(D);

            if (_shell != null)
            {
                s.SwapsPlayer = _shell.EvaluateShuffleSwapCount(D, false);
                s.SwapsEnemy = _shell.EvaluateShuffleSwapCount(D, true);
                s.ShufflePlayer = _shell.EvaluateShuffleMoveDuration(D, false);
                s.ShuffleEnemy = _shell.EvaluateShuffleMoveDuration(D, true);
            }

            if (_enemy != null)
                s.TrackedFraction = _enemy.EvaluateTrackedKnowledgeFraction(D, Mathf.Max(1, s.SwapsEnemy));
            else
                s.TrackedFraction = 1f;

            // E[hit] = T*((1-Per) + Per*r) + (1-T)*r, где r = E[M]/C.
            float r = s.Cups > 0 ? s.ExpectedMarkers / s.Cups : 0f;
            s.ExpectedHit = s.TrackedFraction * ((1f - s.Perror) + s.Perror * r) + (1f - s.TrackedFraction) * r;

            // Ход врага: перемешивание (все обмены идут ПАРАЛЛЕЛЬНО двумя
            // чашками) + паузы между обменами + раздумье.
            float between = _shell != null ? _shell.BetweenSwapDelay : 0.1f;
            int swaps = Mathf.Max(1, s.SwapsEnemy);
            s.EnemyTurnTime = swaps * s.ShuffleEnemy
                              + Mathf.Max(0, swaps - 1) * between
                              + s.DecisionDelay;

            return s;
        }

        private Sample[] SampleRange(float spikeMul, float spikeFlat)
        {
            int n = Mathf.Max(2, _sampleCount);
            var arr = new Sample[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                float d = Mathf.Lerp(_dMin, _dMax, t);
                float spiked = d * spikeMul + spikeFlat;
                arr[i] = Evaluate(spiked);
                arr[i].D = d;
            }
            return arr;
        }

        // ============================================================
        //  GUI
        // ============================================================

        private void OnGUI()
        {
            if (_titleStyle == null)
                _titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.LabelField("Баланс рана", _titleStyle);
            EditorGUILayout.LabelField(
                "Формула: p = пройденные энкаунтеры / EncountersTotal; D = DifficultyAtRunEnd · p^Gamma + IntraRamp · (раунды / RoundsPerEncounter). " +
                "Минибосс/босс домножают D через EnemyAIConfig.DifficultyMultiplier.",
                EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(4f);

            DrawGraphSettings();
            EditorGUILayout.Space(6f);

            _showConfigs = EditorGUILayout.Foldout(_showConfigs, "Конфиги (правки сразу пишутся в ассет)", true);
            if (_showConfigs)
            {
                EditorGUI.indentLevel++;
                DrawConfig(ref _showEnemy, "EnemyAIConfig", _enemySo);
                DrawConfig(ref _showShell, "ShellConfig", _shellSo);
                DrawConfig(ref _showHealth, "HealthProgressionConfig", _healthSo);
                DrawConfig(ref _showRun, "RunDifficultyConfig", _runSo);
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(6f);
            DrawGraphs();
            EditorGUILayout.Space(6f);
            DrawTable();
            EditorGUILayout.Space(6f);
            DrawExportButtons();

            EditorGUILayout.EndScrollView();

            if (_enemySo != null) _enemySo.ApplyModifiedProperties();
            if (_shellSo != null) _shellSo.ApplyModifiedProperties();
            if (_healthSo != null) _healthSo.ApplyModifiedProperties();
            if (_runSo != null) _runSo.ApplyModifiedProperties();
        }

        private void DrawGraphSettings()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("D от", GUILayout.Width(34f));
            _dMin = EditorGUILayout.FloatField(_dMin, GUILayout.Width(50f));
            EditorGUILayout.LabelField("до", GUILayout.Width(20f));
            _dMax = EditorGUILayout.FloatField(_dMax, GUILayout.Width(50f));
            EditorGUILayout.LabelField("точек", GUILayout.Width(40f));
            _sampleCount = Mathf.Clamp(EditorGUILayout.IntField(_sampleCount, GUILayout.Width(46f)), 8, 1001);

            if (_run != null && GUILayout.Button("Сброс к насыщению", GUILayout.Width(140f)))
                _dMax = Mathf.Max(10f, _run.DifficultySaturation);

            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Спайк босса: D·", GUILayout.Width(96f));
            _spikeMultiplier = EditorGUILayout.FloatField(_spikeMultiplier, GUILayout.Width(46f));
            EditorGUILayout.LabelField("+", GUILayout.Width(12f));
            _spikeFlatBonus = EditorGUILayout.FloatField(_spikeFlatBonus, GUILayout.Width(40f));
            EditorGUILayout.EndHorizontal();
        }

        private void DrawConfig(ref bool expanded, string label, SerializedObject so)
        {
            if (so == null)
            {
                EditorGUILayout.HelpBox($"{label}: ассет не найден в Resources.", MessageType.Warning);
                return;
            }

            expanded = EditorGUILayout.Foldout(expanded, label, true);
            if (!expanded) return;

            EditorGUI.indentLevel++;
            so.Update();
            var prop = so.GetIterator();
            bool enterChildren = true;
            while (prop.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (prop.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(prop, true);
            }
            so.ApplyModifiedProperties();
            EditorGUI.indentLevel--;
        }

        private void DrawGraphs()
        {
            var normal = SampleRange(1f, 0f);
            bool spike = !Mathf.Approximately(_spikeMultiplier, 1f) || !Mathf.Approximately(_spikeFlatBonus, 0f);
            var spiked = spike ? SampleRange(_spikeMultiplier, _spikeFlatBonus) : null;

            EditorGUILayout.LabelField("Кривые по D    (синяя — обычная линия, оранжевая — спайк босса)", EditorStyles.boldLabel);

            DrawPlot("Вероятность попадания врага E[hit]   (0..1)",
                normal, s => s.ExpectedHit, spiked, s => s.ExpectedHit, 0f, 1f);
            DrawPlot("Шанс ошибки врага Perror   (0..1)",
                normal, s => s.Perror, spiked, s => s.Perror, 0f, 1f);
            DrawPlot("Plose — потеря метки за один обмен   (0..1)",
                normal, s => s.Plose, spiked, s => s.Plose, 0f, 1f);
            DrawPlot("Доля сохранённой цели после всего перемешивания   (0..1)",
                normal, s => s.TrackedFraction, spiked, s => s.TrackedFraction, 0f, 1f);
            DrawPlot("Время хода врага, сек   (перемешивание + паузы + раздумье)",
                normal, s => s.EnemyTurnTime, spiked, s => s.EnemyTurnTime, 0f, -1f);
            DrawPlot("Пауза врага перед атакой, сек",
                normal, s => s.DecisionDelay, spiked, s => s.DecisionDelay, 0f, -1f);
            DrawPlot("Чашек на столе (C) и ожидаемых меток E[M]",
                normal, s => s.Cups, spiked, s => s.Cups, 0f, 9f,
                secondary: s => s.ExpectedMarkers);
        }

        private void DrawPlot(string title, Sample[] a, Func<Sample, float> fa,
            Sample[] b, Func<Sample, float> fb, float min, float max,
            Func<Sample, float> secondary = null)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
            var rect = GUILayoutUtility.GetRect(_graphW, _graphH, GUILayout.ExpandWidth(true));

            float lo = min, hi = max;
            if (hi < 0f)
            {
                hi = float.MinValue;
                for (int i = 0; i < a.Length; i++)
                {
                    hi = Mathf.Max(hi, fa(a[i]));
                    if (secondary != null) hi = Mathf.Max(hi, secondary(a[i]));
                    if (b != null) hi = Mathf.Max(hi, fb(b[i]));
                }
                hi = Mathf.Max(1f, hi) * 1.15f;
            }
            if (hi - lo < 0.0001f) hi = lo + 1f;

            EnsureGraphTexture();
            var px = new Color[_graphW * _graphH];
            var bg = new Color(0.16f, 0.16f, 0.18f, 1f);
            for (int i = 0; i < px.Length; i++) px[i] = bg;

            // Горизонтальная сетка.
            for (int g = 0; g <= 4; g++)
            {
                int y = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(0f, _graphH - 1, g / 4f)), 0, _graphH - 1);
                var grid = new Color(1f, 1f, 1f, 0.08f);
                for (int x = 0; x < _graphW; x++)
                    px[y * _graphW + x] = grid;
            }

            if (b != null)
                RasterCurve(px, b, fb, lo, hi, new Color(1f, 0.6f, 0.15f, 1f));
            RasterCurve(px, a, fa, lo, hi, new Color(0.35f, 0.7f, 1f, 1f));
            if (secondary != null)
                RasterCurve(px, a, secondary, lo, hi, new Color(0.5f, 0.9f, 0.5f, 1f));

            _graphTex.SetPixels(px);
            _graphTex.Apply(false);
            GUI.DrawTexture(rect, _graphTex, ScaleMode.StretchToFill);

            // Подписи min/max поверх.
            GUI.Label(new Rect(rect.x + 3f, rect.y + 1f, 120f, 16f),
                hi.ToString("F2", CultureInfo.InvariantCulture), EditorStyles.miniLabel);
            GUI.Label(new Rect(rect.x + 3f, rect.yMax - 16f, 120f, 16f),
                lo.ToString("F2", CultureInfo.InvariantCulture), EditorStyles.miniLabel);

            EditorGUILayout.LabelField($"  min {lo:F2}   max {hi:F2}   (D от {_dMin:F1} до {_dMax:F1})",
                EditorStyles.miniLabel);
        }

        private void EnsureGraphTexture()
        {
            if (_graphTex != null && _graphTex.width == _graphW && _graphTex.height == _graphH)
                return;
            if (_graphTex != null)
                DestroyImmediate(_graphTex);
            _graphTex = new Texture2D(_graphW, _graphH, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private void RasterCurve(Color[] px, Sample[] a, Func<Sample, float> f, float lo, float hi, Color color)
        {
            if (a == null || a.Length < 2) return;
            float range = Mathf.Max(0.0001f, hi - lo);
            float prevY = -1f;
            for (int x = 0; x < _graphW; x++)
            {
                float t = _graphW <= 1 ? 0f : x / (float)(_graphW - 1);
                int idx = Mathf.Clamp(Mathf.RoundToInt(t * (a.Length - 1)), 0, a.Length - 1);
                float v = Mathf.Clamp(f(a[idx]), lo, hi);
                float ty = (v - lo) / range;
                float y = (_graphH - 1) - ty * (_graphH - 1);

                int y0 = Mathf.Clamp(Mathf.RoundToInt(prevY < 0f ? y : Mathf.Min(prevY, y)), 0, _graphH - 1);
                int y1 = Mathf.Clamp(Mathf.RoundToInt(prevY < 0f ? y : Mathf.Max(prevY, y)), 0, _graphH - 1);
                for (int yy = y0; yy <= y1; yy++)
                {
                    SetPixelSafe(px, x, yy, color);
                    if (x > 0) SetPixelSafe(px, x - 1, yy, color);
                }
                prevY = y;
            }
        }

        private void SetPixelSafe(Color[] px, int x, int y, Color c)
        {
            if (x < 0 || x >= _graphW || y < 0 || y >= _graphH) return;
            px[y * _graphW + x] = c;
        }

        private void DrawTable()
        {
            var a = SampleRange(1f, 0f);
            EditorGUILayout.LabelField("Таблица по D  (то же, что уходит в TSV / Google Sheets)", EditorStyles.boldLabel);

            int step = Mathf.Max(1, a.Length / 14);
            var header = new[]
            {
                "D", "Plose", "Trk", "Pcorr", "Perr", "C", "E[M]",
                "swП", "swВ", "шП", "шВ", "пауза", "ходВ", "E[hit]"
            };

            EditorGUILayout.BeginHorizontal();
            foreach (var h in header)
                GUILayout.Label(h, EditorStyles.miniBoldLabel, GUILayout.Width(52f));
            EditorGUILayout.EndHorizontal();

            for (int i = 0; i < a.Length; i += step)
            {
                var s = a[i];
                EditorGUILayout.BeginHorizontal();
                Cell(s.D, "F1"); Cell(s.Plose, "F3"); Cell(s.TrackedFraction, "F2"); Cell(s.Pcorrect, "F3");
                Cell(s.Perror, "F3"); CellInt(s.Cups); Cell(s.ExpectedMarkers, "F2");
                CellInt(s.SwapsPlayer); CellInt(s.SwapsEnemy);
                Cell(s.ShufflePlayer, "F2"); Cell(s.ShuffleEnemy, "F2"); Cell(s.DecisionDelay, "F2");
                Cell(s.EnemyTurnTime, "F2"); Cell(s.ExpectedHit, "F3");
                EditorGUILayout.EndHorizontal();
            }
        }

        private static void Cell(float v, string fmt) =>
            GUILayout.Label(v.ToString(fmt, CultureInfo.InvariantCulture), EditorStyles.miniLabel, GUILayout.Width(52f));

        private static void CellInt(int v) =>
            GUILayout.Label(v.ToString(CultureInfo.InvariantCulture), EditorStyles.miniLabel, GUILayout.Width(52f));

        private void DrawExportButtons()
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Copy TSV (в Google Sheets через Ctrl+V)", GUILayout.Height(24f)))
                EditorGUIUtility.systemCopyBuffer = BuildTsv();

            if (GUILayout.Button("Export .tsv в проект", GUILayout.Height(24f)))
                ExportFile("BalanceModel.tsv", BuildTsv());

            if (GUILayout.Button("Export .md в проект", GUILayout.Height(24f)))
                ExportFile("Balance.md", BuildMarkdown());

            EditorGUILayout.EndHorizontal();
        }

        private void ExportFile(string fileName, string content)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string dir = Path.Combine(root, "Docs");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllText(path, content, new UTF8Encoding(false));
            AssetDatabase.Refresh();
            Debug.Log($"[Balance] Экспортировано: {path}");
            EditorUtility.RevealInFinder(path);
        }

        // ============================================================
        //  Экспорт
        // ============================================================

        private string BuildTsv()
        {
            var a = SampleRange(1f, 0f);
            var sb = new StringBuilder();
            sb.Append("D\tPlose\tTrackFrac\tPcorrect\tPerror\tCups\tEMarkers\tSwapsPlayer\tSwapsEnemy\tShufflePlayer\tShuffleEnemy\tDecisionDelay\tEnemyTurnTime\tExpectedHit\n");
            foreach (var s in a)
            {
                sb.Append(s.D.ToString("F2", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.Plose.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.TrackedFraction.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.Pcorrect.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.Perror.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.Cups).Append('\t')
                  .Append(s.ExpectedMarkers.ToString("F3", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.SwapsPlayer).Append('\t')
                  .Append(s.SwapsEnemy).Append('\t')
                  .Append(s.ShufflePlayer.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.ShuffleEnemy.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.DecisionDelay.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.EnemyTurnTime.ToString("F4", CultureInfo.InvariantCulture)).Append('\t')
                  .Append(s.ExpectedHit.ToString("F4", CultureInfo.InvariantCulture)).Append('\n');
            }
            return sb.ToString();
        }

        private string BuildMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Balance (сгенерировано окном ShellGame → Balance)");
            sb.AppendLine();
            sb.AppendLine($"Диапазон D: {_dMin:F1} .. {_dMax:F1}.");
            sb.AppendLine();
            sb.AppendLine("| D | Plose | Trk | Pcorr | Perr | C | E[M] | swП | swВ | шП | шВ | пауза | ходВ | E[hit] |");
            sb.AppendLine("|---|---|---|---|---|---|---|---|---|---|---|---|---|---|");

            var a = SampleRange(1f, 0f);
            int step = Mathf.Max(1, a.Length / 30);
            for (int i = 0; i < a.Length; i += step)
            {
                var s = a[i];
                sb.AppendLine($"| {s.D:F1} | {s.Plose:F3} | {s.TrackedFraction:F2} | {s.Pcorrect:F3} | {s.Perror:F3} | {s.Cups} | " +
                              $"{s.ExpectedMarkers:F2} | {s.SwapsPlayer} | {s.SwapsEnemy} | {s.ShufflePlayer:F2} | {s.ShuffleEnemy:F2} | " +
                              $"{s.DecisionDelay:F2} | {s.EnemyTurnTime:F2} | {s.ExpectedHit:F3} |");
            }
            return sb.ToString();
        }
    }
}
