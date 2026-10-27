using System;
using System.Collections.Generic;
using UnityEngine;
using Yazlab.UI;

namespace Yazlab.Core
{
    /// <summary>
    /// Oyun durumunu (<see cref="GameState"/>), canlı senaryo özetini (<see cref="IGameStateProvider"/>)
    /// ve oyun bitiş akışını yöneten merkezi yönetici bileşen.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-80)]
    public class GameManager : MonoBehaviour, IGameStateProvider
    {
        [SerializeField] private PlayerHealth playerHealth;
        [SerializeField] private PlayerCombat playerCombat;
        [SerializeField] private EndScreen endScreen;
        [SerializeField] private bool triggerVictoryWhenAllEnemiesCleared = true;
        [Tooltip("Son kapıdan geçiş oyunu bitirsin (B21'de köy sahnesine geçiş bağlanınca kapatılır).")]
        [SerializeField] private bool finalGateEndsGame = true;
        [Tooltip("Şef (boss) ölünce oyun kazanılsın (köy sahnesindeki Reis için).")]
        [SerializeField] private bool bossDefeatEndsGame;
        [Tooltip("bossDefeatEndsGame açıkken şef ölümünden zafer ekranına kadar bekleme (gerçek sn).")]
        [SerializeField] private float bossVictoryDelay = 2.5f;
        [Tooltip("Önceki sahneden taşınan ilerleme (köy sahnesi: geçit/son kapı/balta özetine yansır). Boş olabilir.")]
        [SerializeField] private GameProgress gameProgress;
        [SerializeField] private bool debugLog = true;

        private readonly List<EnemyController> _trackedWolves = new List<EnemyController>(8);

        private GameState _currentState = GameState.Exploring;
        private bool _bossDefeated;
        private bool _hasAxe;
        private bool _companionGateOpened;
        private bool _finalDoorOpened;

        /// <summary>
        /// Oyun durumu değiştiğinde tetiklenir.
        /// </summary>
        public event Action<GameState> StateChanged;

        /// <inheritdoc />
        public GameState CurrentState => _currentState;

        /// <summary>
        /// Oyuncu sağlık bileşeni referansı.
        /// </summary>
        public PlayerHealth PlayerHealthRef
        {
            get => playerHealth;
            set => playerHealth = value;
        }

        /// <summary>
        /// Oyuncu savaş bileşeni referansı.
        /// </summary>
        public PlayerCombat PlayerCombatRef
        {
            get => playerCombat;
            set => playerCombat = value;
        }

        /// <summary>
        /// Oyun sonu (Victory) ekranı referansı.
        /// </summary>
        public EndScreen EndScreenRef
        {
            get => endScreen;
            set => endScreen = value;
        }

        private void Awake()
        {
            ServiceRegistry.Register<IGameStateProvider>(this);
            ServiceRegistry.Register(this);
        }

        private void OnEnable()
        {
            GameEvents.BossDefeated += OnBossDefeated;
            GameEvents.AxePickedUp += OnAxePickedUp;
            GameEvents.GateOpened += OnGateOpened;
            GameEvents.DialogueStarted += OnDialogueStarted;
            GameEvents.DialogueEnded += OnDialogueEnded;
            GameEvents.GameWon += OnGameWon;
            GameEvents.FinalGatePassed += OnFinalGatePassed;
        }

        private void Start()
        {
            if (gameProgress != null && gameProgress.ArrivedFromGate)
            {
                // Son kapıdan gelindi: önceki bölümlerin kazanımları bu sahnenin özetinde de görünsün.
                _companionGateOpened = true;
                _finalDoorOpened = true;
                _hasAxe = gameProgress.HasAxe;
            }

            ResolveReferencesOnce();
        }

        private void OnDisable()
        {
            GameEvents.BossDefeated -= OnBossDefeated;
            GameEvents.AxePickedUp -= OnAxePickedUp;
            GameEvents.GateOpened -= OnGateOpened;
            GameEvents.DialogueStarted -= OnDialogueStarted;
            GameEvents.DialogueEnded -= OnDialogueEnded;
            GameEvents.GameWon -= OnGameWon;
            GameEvents.FinalGatePassed -= OnFinalGatePassed;

            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
                playerHealth.Revived -= OnPlayerRevived;
            }

            for (int i = 0; i < _trackedWolves.Count; i++)
            {
                if (_trackedWolves[i] != null)
                {
                    _trackedWolves[i].Died -= OnWolfDied;
                }
            }
        }

        private void OnDestroy()
        {
            ServiceRegistry.Unregister<IGameStateProvider>(this);
            ServiceRegistry.Unregister(this);
        }

        /// <inheritdoc />
        public GameStateSnapshot Capture()
        {
            bool axeEquipped = _hasAxe || (playerCombat != null && playerCombat.HasAxe);
            int currentHp = playerHealth != null ? playerHealth.CurrentHealth : 100;
            int aliveWolves = CountAliveWolves();

            return new GameStateSnapshot(
                bossDefeated: _bossDefeated,
                hasAxe: axeEquipped,
                companionGateOpened: _companionGateOpened,
                finalDoorOpened: _finalDoorOpened,
                playerHp: currentHp,
                wolvesAliveCount: aliveWolves);
        }

        /// <summary>
        /// Oyun durumunu günceller ve <see cref="StateChanged"/> olayını tetikler.
        /// </summary>
        /// <param name="newState">Yeni oyun durumu.</param>
        public void SetState(GameState newState)
        {
            if (_currentState == newState)
            {
                return;
            }

            _currentState = newState;
            if (debugLog)
            {
                Debug.Log($"[GameManager] Durum değişti -> {_currentState}");
            }

            StateChanged?.Invoke(_currentState);
        }

        /// <summary>
        /// Inspector sağ-tık menüsünden zafer ekranını test etmek için <see cref="GameEvents.RaiseGameWon"/> tetikler.
        /// </summary>
        [ContextMenu("Zafer Ekranini Test Et (RaiseGameWon)")]
        public void TriggerVictoryForTest()
        {
            GameEvents.RaiseGameWon();
        }

        private void ResolveReferencesOnce()
        {
            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Exclude);
            }

            if (playerCombat == null && playerHealth != null)
            {
                playerCombat = playerHealth.GetComponent<PlayerCombat>();
            }

            if (playerCombat == null)
            {
                playerCombat = FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Exclude);
            }

            if (endScreen == null && !ServiceRegistry.TryGet(out endScreen))
            {
                endScreen = FindAnyObjectByType<EndScreen>(FindObjectsInactive.Include);
            }

            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
                playerHealth.Died += OnPlayerDied;
                playerHealth.Revived -= OnPlayerRevived;
                playerHealth.Revived += OnPlayerRevived;
            }

            CacheSceneEnemies();
        }

        /// <summary>
        /// Sahnedeki düşmanları başlangıçta bir kez tarayarak kurt listesini önbelleğe alır (`Update` içinde `Find` yapılmaz).
        /// </summary>
        public void CacheSceneEnemies()
        {
            for (int i = 0; i < _trackedWolves.Count; i++)
            {
                if (_trackedWolves[i] != null)
                {
                    _trackedWolves[i].Died -= OnWolfDied;
                }
            }

            _trackedWolves.Clear();
            EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyController enemy = enemies[i];
                // Kapalı bileşenler sayılmaz: kapı bekçisinin savaş bileşeni yalnızca 'attack' aksiyonunda açılır.
                if (enemy == null || !enemy.enabled)
                {
                    continue;
                }

                if (enemy.IsBoss)
                {
                    if (enemy.IsDead)
                    {
                        _bossDefeated = true;
                    }
                }
                else
                {
                    enemy.Died -= OnWolfDied;
                    enemy.Died += OnWolfDied;
                    _trackedWolves.Add(enemy);
                }
            }
        }

        private int CountAliveWolves()
        {
            if (_trackedWolves.Count == 0)
            {
                CacheSceneEnemies();
            }

            int alive = 0;
            for (int i = _trackedWolves.Count - 1; i >= 0; i--)
            {
                EnemyController wolf = _trackedWolves[i];
                if (wolf == null)
                {
                    _trackedWolves.RemoveAt(i);
                    continue;
                }

                if (!wolf.IsDead)
                {
                    alive++;
                }
            }

            return alive;
        }

        private void OnWolfDied(IDamageable _)
        {
            int remaining = CountAliveWolves();
            if (debugLog)
            {
                Debug.Log($"[GameManager] Kurt öldü — kalan kurt: {remaining}");
            }

            CheckAllEnemiesClearedVictory();
        }

        private void OnBossDefeated()
        {
            _bossDefeated = true;
            if (debugLog)
            {
                Debug.Log("[GameManager] Kabile şefi yenildi (BossDefeated = true).");
            }

            if (bossDefeatEndsGame && _currentState != GameState.Victory)
            {
                StartCoroutine(BossVictoryRoutine());
                return;
            }

            CheckAllEnemiesClearedVictory();
        }

        private System.Collections.IEnumerator BossVictoryRoutine()
        {
            // Ölüm mesajı ve animasyonu görünsün; gerçek zamanla (timeScale'den bağımsız).
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, bossVictoryDelay));
            if (_currentState != GameState.Victory)
            {
                GameEvents.RaiseGameWon();
            }
        }

        private void CheckAllEnemiesClearedVictory()
        {
            if (!triggerVictoryWhenAllEnemiesCleared || _currentState == GameState.Victory)
            {
                return;
            }

            if (_bossDefeated && CountAliveWolves() == 0)
            {
                GameEvents.RaiseGameWon();
            }
        }

        private void OnAxePickedUp()
        {
            _hasAxe = true;
            if (debugLog)
            {
                Debug.Log("[GameManager] Balta alındı (HasAxe = true).");
            }
        }

        private void OnGateOpened()
        {
            _companionGateOpened = true;
            if (debugLog)
            {
                Debug.Log("[GameManager] Fiziksel engel kapısı açıldı (CompanionGateOpened = true).");
            }
        }

        private void OnDialogueStarted()
        {
            if (_currentState != GameState.Victory)
            {
                SetState(GameState.InDialogue);
            }
        }

        private void OnDialogueEnded()
        {
            if (_currentState == GameState.InDialogue)
            {
                SetState(GameState.Exploring);
            }
        }

        private void OnFinalGatePassed()
        {
            _finalDoorOpened = true;
            if (debugLog)
            {
                Debug.Log($"[GameManager] Son kapıdan geçildi (oyunu bitirir: {finalGateEndsGame}).");
            }

            if (finalGateEndsGame && _currentState != GameState.Victory)
            {
                GameEvents.RaiseGameWon();
            }
        }

        private void OnGameWon()
        {
            _finalDoorOpened = true;
            SetState(GameState.Victory);

            GameStateSnapshot snapshot = Capture();
            if (debugLog)
            {
                Debug.Log($"[GameManager] Oyun kazanıldı! Özet: {snapshot}");
            }

            if (endScreen == null)
            {
                ServiceRegistry.TryGet(out endScreen);
            }

            if (endScreen != null)
            {
                endScreen.ShowVictory(snapshot);
            }
        }

        private void OnPlayerDied(IDamageable _)
        {
            if (_currentState != GameState.Victory)
            {
                SetState(GameState.GameOver);
            }
        }

        private void OnPlayerRevived()
        {
            if (_currentState == GameState.GameOver)
            {
                SetState(GameState.Exploring);
            }
        }
    }
}
