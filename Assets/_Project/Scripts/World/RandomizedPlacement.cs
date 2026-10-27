using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Yazlab.World
{
    /// <summary>
    /// Nesneyi (ör. köpeğin çekeceği şalter) aday noktalardan birine yerleştirir. Oyun başında rastgele seçer;
    /// yalnızca <see cref="_reachableFrom"/> noktasından verilen NavMesh agent tipiyle tam yol (<c>PathComplete</c>)
    /// bulunan adaylar kabul edilir. Böylece köpeğin hedefi her oyunda farklı yerde olur ve köpek yolu NavMesh ile
    /// kendisi bulur; hedef konumu hiçbir yerde sabit yazılı değildir. Test harness <see cref="TryPlace"/> ile
    /// her adayı sırayla dener.
    /// </summary>
    [DisallowMultipleComponent]
    public class RandomizedPlacement : MonoBehaviour
    {
        [Tooltip("Aday konumlar (konum + yön). Nesne bunlardan birine taşınır.")]
        [SerializeField] private Transform[] _candidates;

        [Tooltip("Ulaşılabilirlik kontrolünün başlangıç noktası (ör. emir alanı).")]
        [SerializeField] private Transform _reachableFrom;

        [Tooltip("Ulaşılabilirlik kontrolündeki NavMesh agent tipi (köpek). Etkileşim noktası bu ağda aranır.")]
        [SerializeField] private int _agentTypeId;

        [Tooltip("Yol kontrolünde hedef olarak kullanılacak nokta (ör. şalterin InteractionPoint'i). Boşsa nesnenin kendisi.")]
        [SerializeField] private Transform _probePoint;

        [SerializeField, Min(0.1f)] private float _sampleRadius = 2f;

        [Tooltip("Oyun başında rastgele aday seçilir. Kapalıysa sahnedeki konum korunur.")]
        [SerializeField] private bool _randomizeOnStart = true;

        [Tooltip("0 = her oyunda farklı; başka değer = tekrarlanabilir seçim.")]
        [SerializeField] private int _seed;

        [SerializeField] private bool _debugLog = true;

        private readonly List<int> _order = new List<int>();
        private NavMeshPath _path;

        /// <summary>Aday sayısı.</summary>
        public int CandidateCount => _candidates != null ? _candidates.Length : 0;

        /// <summary>Şu anki adayın sırası; hiçbirine yerleşmediyse -1.</summary>
        public int CurrentIndex { get; private set; } = -1;

        /// <summary>Şu anki adayın adı (yerleşmediyse nesnenin adı).</summary>
        public string CurrentName => CurrentIndex >= 0 ? _candidates[CurrentIndex].name : name;

        /// <summary>Yerleşim değişince (aday sırası ile) tetiklenir.</summary>
        public event Action<int> Placed;

        private void Start()
        {
            if (_randomizeOnStart)
            {
                PlaceRandom();
            }
        }

        /// <summary>Adayları karışık sırayla dener, ulaşılabilen ilkine yerleşir.</summary>
        public bool PlaceRandom()
        {
            var random = _seed != 0 ? new System.Random(_seed) : new System.Random();
            _order.Clear();
            for (int i = 0; i < CandidateCount; i++)
            {
                _order.Add(i);
            }

            // Fisher–Yates karıştırma.
            for (int i = _order.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (_order[i], _order[j]) = (_order[j], _order[i]);
            }

            foreach (int index in _order)
            {
                if (TryPlace(index))
                {
                    return true;
                }
            }

            Debug.LogWarning($"[Yerlesim] {name}: ulaşılabilir aday yok; sahnedeki konum korunuyor.", this);
            return false;
        }

        /// <summary>Nesneyi <paramref name="index"/> sıralı adaya taşır; aday NavMesh'ten ulaşılamıyorsa taşımaz.</summary>
        public bool TryPlace(int index)
        {
            if (index < 0 || index >= CandidateCount || _candidates[index] == null)
            {
                return false;
            }

            Transform candidate = _candidates[index];
            Vector3 previousPosition = transform.position;
            Quaternion previousRotation = transform.rotation;
            transform.SetPositionAndRotation(candidate.position, candidate.rotation);

            if (!IsReachable(out float pathLength))
            {
                transform.SetPositionAndRotation(previousPosition, previousRotation);
                if (_debugLog)
                {
                    Debug.LogWarning($"[Yerlesim] {name}: '{candidate.name}' NavMesh'ten ulaşılamıyor; atlandı.", this);
                }

                return false;
            }

            Physics.SyncTransforms();
            CurrentIndex = index;
            if (_debugLog)
            {
                Debug.Log($"[Yerlesim] {name} → {candidate.name} ({candidate.position.x:F1}, {candidate.position.z:F1}), " +
                          $"NavMesh yolu {pathLength:F1} m.");
            }

            Placed?.Invoke(index);
            return true;
        }

        private bool IsReachable(out float pathLength)
        {
            pathLength = 0f;
            if (_reachableFrom == null)
            {
                return true;
            }

            var filter = new NavMeshQueryFilter { agentTypeID = _agentTypeId, areaMask = NavMesh.AllAreas };
            Vector3 probe = _probePoint != null ? _probePoint.position : transform.position;
            if (!NavMesh.SamplePosition(_reachableFrom.position, out NavMeshHit from, _sampleRadius * 2f, filter) ||
                !NavMesh.SamplePosition(probe, out NavMeshHit to, _sampleRadius, filter))
            {
                return false;
            }

            _path ??= new NavMeshPath();
            if (!NavMesh.CalculatePath(from.position, to.position, filter, _path) || _path.status != NavMeshPathStatus.PathComplete)
            {
                return false;
            }

            Vector3[] corners = _path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                pathLength += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return true;
        }
    }
}
