using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.Companion;
using Yazlab.Core;
using Yazlab.World;

namespace Yazlab.Testing
{
    /// <summary>
    /// Köpek refakatçinin emir görevini (şalteri çekip geçidi açma) farklı başlangıç noktalarından ve
    /// (<see cref="RandomizedPlacement"/> bağlıysa) farklı şalter konumlarından tekrar tekrar otomatik çalıştırır; başarı, süre, yol uzunluğu ve recover sayısını ölçer.
    /// Sonuçları <c>Application.persistentDataPath/companion_results.csv</c> dosyasına yazar ve Console'a
    /// Markdown özet tablosu basar. Hedef sabit koordinatla verilmez; emir oyundaki gibi
    /// <see cref="CompanionAgent.CommandActivateNearest"/> (sensör + <see cref="InteractableRegistry"/>) ile çözülür.
    /// </summary>
    [DisallowMultipleComponent]
    public class CompanionTestHarness : MonoBehaviour
    {
        private const string LogPrefix = "[CompanionTest]";
        private const float SpawnSampleRadius = 2f;

        private static readonly string[] CsvHeader =
        {
            "run_id", "trial", "target_index", "target_name", "target_x", "target_z",
            "spawn_index", "spawn_name", "spawn_x", "spawn_z",
            "straight_dist_m", "optimal_path_m", "success", "duration_s", "path_length_m",
            "recover_count", "result", "time_scale", "timestamp"
        };

        [Header("Bağlantılar")]
        [Tooltip("Test edilecek köpek.")]
        [SerializeField] private CompanionAgent _companion;

        [Tooltip("Köpeğin her denemede yerleştirileceği başlangıç noktaları (en az 5).")]
        [SerializeField] private Transform[] _spawnPoints;

        [Tooltip("Şalterin aday konumları. Atanırsa her aday için tüm başlangıç noktaları denenir.")]
        [SerializeField] private RandomizedPlacement _targetPlacement;

        [Tooltip("Her deneme öncesi sıfırlanacak IResettable nesneler (Gate, Lever). Boşsa sahnede aranır.")]
        [SerializeField] private MonoBehaviour[] _resettables;

        [Header("Deneme Ayarları")]
        [Tooltip("Her başlangıç noktası için deneme sayısı.")]
        [SerializeField, Min(1)] private int _trialsPerSpawn = 10;

        [Tooltip("Bir denemenin başarısız sayılacağı en uzun oyun süresi (sn).")]
        [SerializeField, Min(5f)] private float _trialTimeout = 60f;

        [Tooltip("Warp sonrası emirden önce beklenen oyun süresi (sn); kapının NavMesh oyuğu bu sürede yenilenir.")]
        [SerializeField, Min(0f)] private float _settleTime = 0.3f;

        [Tooltip("Testi hızlandırmak için zaman ölçeği (ör. 4). Süreler oyun saniyesi olarak ölçülür.")]
        [SerializeField, Range(1f, 8f)] private float _timeScale = 1f;

        [Tooltip("Emir hedefinin aranacağı en büyük mesafe (m).")]
        [SerializeField, Min(1f)] private float _commandSearchRadius = 100f;

        [Header("Koşullar")]
        [Tooltip("Test boyunca kurtları devre dışı bırakır (tekrarlanabilir koşul).")]
        [SerializeField] private bool _disableEnemiesDuringTest = true;

        [Tooltip("Test boyunca oyuncu girdisini kilitler (yanlışlıkla E/hareket olmasın).")]
        [SerializeField] private bool _lockPlayerInput = true;

        [Tooltip("Play başlar başlamaz testi otomatik çalıştırır.")]
        [SerializeField] private bool _runOnStart;

        [Header("Çıktı")]
        [SerializeField] private string _csvFileName = "companion_results.csv";
        [SerializeField] private string _summaryFileName = "companion_summary.md";

        private readonly List<CompanionTrialResult> _results = new List<CompanionTrialResult>();
        private readonly List<IResettable> _resetTargets = new List<IResettable>();
        private readonly List<GameObject> _disabledEnemies = new List<GameObject>();
        private readonly List<string> _spawnNames = new List<string>();
        private readonly List<Vector3> _spawnPositions = new List<Vector3>();
        private readonly List<int> _targetIndices = new List<int>();
        private readonly List<string> _targetNames = new List<string>();
        private readonly string[] _row = new string[CsvHeader.Length];

        private NavMeshPath _pathBuffer;
        private Coroutine _runRoutine;
        private CsvLogger _csv;
        private string _runId;
        private float _previousTimeScale = 1f;
        private bool _inputLockHeld;
        private bool _stopRequested;
        private Vector3 _dogStartPosition;
        private int _initialTargetIndex = -1;
        private int _currentTargetIndex = -1;
        private string _currentTargetName;
        private Vector3 _currentTargetPosition;

        // Aktif deneme durumu (olay işleyicileri tarafından doldurulur).
        private bool _trialActive;
        private bool _commandFinished;
        private bool _commandSucceeded;
        private string _abortedStateName;

        /// <summary>Test koşusu devam ediyorsa <c>true</c>.</summary>
        public bool IsRunning => _runRoutine != null;

        /// <summary>Son koşunun sonuçları.</summary>
        public IReadOnlyList<CompanionTrialResult> Results => _results;

        /// <summary>CSV dosyasının tam yolu.</summary>
        public string CsvPath => Path.Combine(Application.persistentDataPath, _csvFileName);

        /// <summary>Koşu bitince (veya durdurulunca) tüm sonuçlarla tetiklenir.</summary>
        public event Action<IReadOnlyList<CompanionTrialResult>> RunCompleted;

        private void Start()
        {
            if (_runOnStart)
            {
                StartRun();
            }
        }

        private void OnDisable()
        {
            if (_runRoutine != null)
            {
                StopCoroutine(_runRoutine);
                _runRoutine = null;
                EndRun("bileşen devre dışı kaldı");
            }
        }

        /// <summary>Tüm başlangıç noktaları × deneme sayısı kadar testi başlatır (yalnızca Play modunda).</summary>
        [ContextMenu("Companion Testini Başlat")]
        public void StartRun()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning($"{LogPrefix} Test yalnızca Play modunda çalışır.", this);
                return;
            }

            if (IsRunning)
            {
                Debug.LogWarning($"{LogPrefix} Test zaten çalışıyor.", this);
                return;
            }

            if (!Prepare())
            {
                return;
            }

            _runRoutine = StartCoroutine(RunRoutine());
        }

        /// <summary>Çalışan testi mevcut deneme bitmeden durdurur; o ana kadarki sonuçlar raporlanır.</summary>
        [ContextMenu("Companion Testini Durdur")]
        public void StopRun()
        {
            if (IsRunning)
            {
                _stopRequested = true;
            }
        }

        private bool Prepare()
        {
            if (_companion == null || _companion.Context == null || _companion.Machine == null)
            {
                Debug.LogError($"{LogPrefix} CompanionAgent atanmamış veya başlatılmamış.", this);
                return false;
            }

            _spawnNames.Clear();
            _spawnPositions.Clear();
            if (_spawnPoints != null)
            {
                foreach (Transform spawn in _spawnPoints)
                {
                    if (spawn == null)
                    {
                        continue;
                    }

                    if (_companion.Context.TrySampleDestination(spawn.position, SpawnSampleRadius, out Vector3 onMesh))
                    {
                        _spawnNames.Add(spawn.name);
                        _spawnPositions.Add(onMesh);
                    }
                    else
                    {
                        Debug.LogWarning($"{LogPrefix} '{spawn.name}' NavMesh üzerinde değil; atlandı.", spawn);
                    }
                }
            }

            if (_spawnPositions.Count == 0)
            {
                Debug.LogError($"{LogPrefix} Geçerli başlangıç noktası yok.", this);
                return false;
            }

            if (_spawnPositions.Count < 5)
            {
                Debug.LogWarning($"{LogPrefix} {_spawnPositions.Count} başlangıç noktası var; isterler en az 5 ister.", this);
            }

            CollectTargets();
            CollectResettables();
            _pathBuffer ??= new NavMeshPath();
            _results.Clear();
            _stopRequested = false;
            _runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _csv = new CsvLogger(CsvPath, CsvHeader);
            return true;
        }

        private void CollectTargets()
        {
            _targetIndices.Clear();
            _targetNames.Clear();
            if (_targetPlacement == null || _targetPlacement.CandidateCount == 0)
            {
                _targetIndices.Add(-1);
                _targetNames.Add("sabit");
                return;
            }

            _initialTargetIndex = _targetPlacement.CurrentIndex;
            for (int i = 0; i < _targetPlacement.CandidateCount; i++)
            {
                if (_targetPlacement.TryPlace(i))
                {
                    _targetIndices.Add(i);
                    _targetNames.Add(_targetPlacement.CurrentName);
                }
            }

            if (_targetIndices.Count == 0)
            {
                _targetIndices.Add(-1);
                _targetNames.Add("sabit");
            }
        }

        private void CollectResettables()
        {
            _resetTargets.Clear();
            if (_resettables != null)
            {
                foreach (MonoBehaviour behaviour in _resettables)
                {
                    if (behaviour is IResettable resettable)
                    {
                        _resetTargets.Add(resettable);
                    }
                    else if (behaviour != null)
                    {
                        Debug.LogWarning($"{LogPrefix} '{behaviour.name}' IResettable uygulamıyor.", behaviour);
                    }
                }
            }

            if (_resetTargets.Count > 0)
            {
                return;
            }

            // Yedek: Inspector boşsa sahnedeki tüm IResettable bileşenlerini (koşu başında bir kez) topla.
            foreach (MonoBehaviour behaviour in FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (behaviour is IResettable resettable)
                {
                    _resetTargets.Add(resettable);
                }
            }
        }

        private IEnumerator RunRoutine()
        {
            BeginRun();

            int total = _targetIndices.Count * _spawnPositions.Count * _trialsPerSpawn;
            Debug.Log($"{LogPrefix} Başladı: {_targetIndices.Count} şalter konumu × {_spawnPositions.Count} başlangıç × " +
                      $"{_trialsPerSpawn} deneme = {total} koşu, timeScale {_timeScale:F1}, CSV: {CsvPath}");

            int trialNumber = 0;
            for (int k = 0; k < _targetIndices.Count && !_stopRequested; k++)
            {
                SelectTarget(k);
                for (int s = 0; s < _spawnPositions.Count && !_stopRequested; s++)
                {
                    for (int t = 0; t < _trialsPerSpawn && !_stopRequested; t++)
                    {
                        trialNumber++;
                        yield return RunTrial(trialNumber, total, s);
                    }
                }
            }

            _runRoutine = null;
            EndRun(_stopRequested ? "kullanıcı durdurdu" : null);
        }

        private void SelectTarget(int k)
        {
            _currentTargetIndex = _targetIndices[k];
            _currentTargetName = _targetNames[k];
            if (_currentTargetIndex >= 0)
            {
                _targetPlacement.TryPlace(_currentTargetIndex);
            }

            _currentTargetPosition = _targetPlacement != null ? _targetPlacement.transform.position : Vector3.zero;
        }

        private IEnumerator RunTrial(int trialNumber, int total, int spawnIndex)
        {
            Time.timeScale = _timeScale;
            ResetWorld();

            Vector3 spawn = _spawnPositions[spawnIndex];
            if (!_companion.ResetForTrial(spawn))
            {
                Record(trialNumber, total, spawnIndex, -1f, -1f, false, 0f, 0f, 0, "warp_failed");
                yield break;
            }

            if (_settleTime > 0f)
            {
                yield return new WaitForSeconds(_settleTime);
            }

            CompanionContext context = _companion.Context;
            Transform dog = _companion.transform;
            int recoverBefore = context.TotalRecoverCount;

            _commandFinished = false;
            _commandSucceeded = false;
            _abortedStateName = null;
            _trialActive = true;

            float startTime = Time.time;
            Vector3 lastPosition = dog.position;
            float pathLength = 0f;

            if (!_companion.CommandActivateNearest(_commandSearchRadius))
            {
                _trialActive = false;
                Record(trialNumber, total, spawnIndex, -1f, -1f, false, 0f, 0f, 0, "command_rejected");
                yield break;
            }

            IInteractable target = context.CurrentTarget;
            float straight = PlanarDistance(dog.position, target.InteractionPoint.position);
            float optimal = ComputeOptimalPathLength(context, dog.position, target.InteractionPoint.position);

            while (!_commandFinished && _abortedStateName == null && !_stopRequested &&
                   Time.time - startTime < _trialTimeout)
            {
                yield return null;
                Vector3 position = dog.position;
                pathLength += PlanarDistance(lastPosition, position);
                lastPosition = position;
            }

            _trialActive = false;
            if (_stopRequested && !_commandFinished)
            {
                yield break; // Yarım kalan deneme kaydedilmez.
            }

            float duration = Time.time - startTime;
            bool success = _commandFinished && _commandSucceeded && target.IsActivated;
            string outcome = success ? "success"
                : _commandFinished ? "recover_limit"
                : _abortedStateName != null ? "aborted_" + _abortedStateName
                : "timeout";

            Record(trialNumber, total, spawnIndex, straight, optimal, success, duration, pathLength,
                context.TotalRecoverCount - recoverBefore, outcome);
        }

        private void BeginRun()
        {
            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            _dogStartPosition = _companion.transform.position;

            if (_lockPlayerInput && !_inputLockHeld)
            {
                InputLock.Acquire();
                _inputLockHeld = true;
            }

            _disabledEnemies.Clear();
            if (_disableEnemiesDuringTest)
            {
                foreach (EnemyController enemy in FindObjectsByType<EnemyController>(FindObjectsSortMode.None))
                {
                    if (enemy != null && enemy.gameObject.activeSelf)
                    {
                        enemy.gameObject.SetActive(false);
                        _disabledEnemies.Add(enemy.gameObject);
                    }
                }
            }

            _companion.CommandCompleted += OnCommandCompleted;
            _companion.Machine.StateChanged += OnStateChanged;
        }

        private void EndRun(string interruptReason)
        {
            _trialActive = false;

            if (_companion != null)
            {
                _companion.CommandCompleted -= OnCommandCompleted;
                if (_companion.Machine != null)
                {
                    _companion.Machine.StateChanged -= OnStateChanged;
                }
            }

            ResetWorld();
            if (_targetPlacement != null && _initialTargetIndex >= 0)
            {
                _targetPlacement.TryPlace(_initialTargetIndex);
            }

            if (_companion != null)
            {
                _companion.ResetForTrial(_dogStartPosition);
            }

            foreach (GameObject enemy in _disabledEnemies)
            {
                if (enemy != null)
                {
                    enemy.SetActive(true);
                }
            }

            _disabledEnemies.Clear();

            if (_inputLockHeld)
            {
                InputLock.Release();
                _inputLockHeld = false;
            }

            Time.timeScale = _previousTimeScale;

            string summary = CompanionTestReport.BuildMarkdown(_results, _spawnNames, _targetNames, _timeScale);
            WriteSummaryFile(summary);
            string header = interruptReason != null ? $"{LogPrefix} Yarıda kesildi ({interruptReason})." : $"{LogPrefix} Bitti.";
            Debug.Log($"{header} CSV: {CsvPath}\n\n{summary}");

            RunCompleted?.Invoke(_results);
        }

        private void ResetWorld()
        {
            for (int i = 0; i < _resetTargets.Count; i++)
            {
                try
                {
                    _resetTargets[i]?.ResetState();
                }
                catch (Exception e)
                {
                    Debug.LogException(e, this);
                }
            }
        }

        private void OnCommandCompleted(bool success)
        {
            if (!_trialActive)
            {
                return;
            }

            _commandFinished = true;
            _commandSucceeded = success;
        }

        private void OnStateChanged(ICompanionState previous, ICompanionState next)
        {
            if (!_trialActive || _commandFinished || next == null || _companion.Context == null)
            {
                return;
            }

            // Görev sonucu bildirilmeden takip/boşta durumuna düşmek = görev yarıda bırakıldı.
            CompanionContext context = _companion.Context;
            if (next == context.Idle || next == context.Follow || next == context.ReturnToPlayer)
            {
                _abortedStateName = next.Name;
            }
        }

        private float ComputeOptimalPathLength(CompanionContext context, Vector3 from, Vector3 to)
        {
            if (!context.TrySampleDestination(to, context.Config.DestinationSampleRadius, out Vector3 end))
            {
                return -1f;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = context.Agent.agentTypeID,
                areaMask = context.Agent.areaMask
            };

            if (!NavMesh.CalculatePath(from, end, filter, _pathBuffer) || _pathBuffer.status != NavMeshPathStatus.PathComplete)
            {
                return -1f;
            }

            Vector3[] corners = _pathBuffer.corners;
            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }

        private void Record(int trialNumber, int total, int spawnIndex, float straight, float optimal,
            bool success, float duration, float pathLength, int recoverCount, string outcome)
        {
            Vector3 spawn = _spawnPositions[spawnIndex];
            var result = new CompanionTrialResult(trialNumber, spawnIndex, _spawnNames[spawnIndex], spawn,
                straight, optimal, success, duration, pathLength, recoverCount, outcome, _currentTargetIndex, _currentTargetName);
            _results.Add(result);

            _row[0] = _runId;
            _row[1] = trialNumber.ToString();
            _row[2] = _currentTargetIndex.ToString();
            _row[3] = _currentTargetName;
            _row[4] = CsvLogger.Format(_currentTargetPosition.x);
            _row[5] = CsvLogger.Format(_currentTargetPosition.z);
            _row[6] = spawnIndex.ToString();
            _row[7] = result.SpawnName;
            _row[8] = CsvLogger.Format(spawn.x);
            _row[9] = CsvLogger.Format(spawn.z);
            _row[10] = CsvLogger.Format(straight);
            _row[11] = CsvLogger.Format(optimal);
            _row[12] = success ? "1" : "0";
            _row[13] = CsvLogger.Format(duration);
            _row[14] = CsvLogger.Format(pathLength);
            _row[15] = recoverCount.ToString();
            _row[16] = outcome;
            _row[17] = CsvLogger.Format(_timeScale, "F1");
            _row[18] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _csv?.AppendRow(_row);

            string mark = success ? "✓" : "✗";
            Debug.Log($"{LogPrefix} {trialNumber}/{total} şalter {_currentTargetName}, {result.SpawnName} {mark} {outcome} — " +
                      $"{duration:F2} sn, {pathLength:F1} m yol, recover {recoverCount}");
        }

        private void WriteSummaryFile(string summary)
        {
            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, _summaryFileName), summary);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"{LogPrefix} Özet dosyası yazılamadı: {e.Message}");
            }
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
