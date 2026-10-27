using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Yazlab.Core
{
    /// <summary>
    /// Son kapıdan geçilince (<see cref="GameEvents.FinalGatePassed"/>) ilerlemeyi <see cref="GameProgress"/>'e kaydeder ve
    /// sonraki sahneyi asenkron yükler (ana thread bloklanmaz). Sahne yüklenemezse oyun takılmaz: hata loglanır ve
    /// oyun bu sahnede kazanılmış sayılır. Oyun başında (ilk sahne) ilerlemeyi sıfırlar.
    /// </summary>
    [DisallowMultipleComponent]
    public class SceneTransition : MonoBehaviour
    {
        [Tooltip("Son kapıdan sonra yüklenecek sahne (Build Settings'te olmalı).")]
        [SerializeField] private string _nextScene = "Bolum3_Koy";
        [SerializeField] private GameProgress _progress;
        [Tooltip("Bu sahne oyunun başlangıcıysa ilerlemeyi sıfırla.")]
        [SerializeField] private bool _resetProgressOnAwake = true;
        [SerializeField] private string _loadingMessage = "Köye gidiliyor...";

        private bool _loading;
        private bool _lockHeld;

        /// <summary>Yükleme sürüyor mu.</summary>
        public bool IsLoading => _loading;

        private void Awake()
        {
            if (_resetProgressOnAwake && _progress != null)
            {
                _progress.ResetRun();
            }
        }

        private void OnEnable() => GameEvents.FinalGatePassed += OnFinalGatePassed;

        private void OnDisable()
        {
            GameEvents.FinalGatePassed -= OnFinalGatePassed;
            ReleaseLock();
        }

        private void OnFinalGatePassed()
        {
            if (!_loading)
            {
                _ = LoadNextAsync();
            }
        }

        private async Awaitable LoadNextAsync()
        {
            _loading = true;
            try
            {
                if (_progress != null && ServiceRegistry.TryGet(out IGameStateProvider state))
                {
                    _progress.CaptureForTransition(state.Capture());
                }

                if (ServiceRegistry.TryGet(out IMessageService messages) && !string.IsNullOrEmpty(_loadingMessage))
                {
                    messages.Show(_loadingMessage, 3f);
                }

                InputLock.Acquire();
                _lockHeld = true;

                AsyncOperation operation = string.IsNullOrEmpty(_nextScene) ? null : SceneManager.LoadSceneAsync(_nextScene);
                if (operation == null)
                {
                    throw new InvalidOperationException($"'{_nextScene}' yüklenemedi (Build Settings'te mi?).");
                }

                await operation;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SceneTransition] {e.Message} Oyun bu sahnede bitirildi.");
                ReleaseLock();
                _loading = false;
                GameEvents.RaiseGameWon();
            }
        }

        private void ReleaseLock()
        {
            if (_lockHeld)
            {
                InputLock.Release();
                _lockHeld = false;
            }
        }
    }
}
