using System;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.World;

namespace Yazlab.Companion
{
    /// <summary>
    /// Durum sınıflarının ortak kullandığı köpek verisi: agent, config, sensör, oyuncu, görev hedefi ve durum makinesi.
    /// Düz C# sınıfıdır; MonoBehaviour'a bağımlılığı yoktur.
    /// </summary>
    public sealed class CompanionContext
    {
        /// <summary>Köpeğin NavMeshAgent bileşeni.</summary>
        public NavMeshAgent Agent { get; }

        /// <summary>Hız ve mesafe parametreleri.</summary>
        public CompanionConfig Config { get; }

        /// <summary>Durumlar arası geçişi yöneten makine.</summary>
        public CompanionStateMachine Machine { get; }

        /// <summary>Engel ve etkileşim hedefi algılayıcı sensör (varsa).</summary>
        public IObstacleSensor Sensor { get; set; }

        /// <summary>En son üretilen sensör okuma verisi.</summary>
        public SensorReading LastReading { get; set; }

        /// <summary>Takip edilecek oyuncu (yoksa null).</summary>
        public Transform Player { get; set; }

        /// <summary>Emirle atanmış aktif etkileşim hedefi (yoksa null).</summary>
        public IInteractable CurrentTarget { get; set; }

        /// <summary>Mevcut emir için yapılan kurtarma (Recover) denemesi sayısı.</summary>
        public int RecoverAttemptCount { get; set; }

        /// <summary>Oyun/test süresince toplam yapılan kurtarma (Recover) sayısı.</summary>
        public int TotalRecoverCount { get; set; }

        /// <summary>Boşta bekleme durumu.</summary>
        public ICompanionState Idle { get; private set; }

        /// <summary>Oyuncuyu takip durumu.</summary>
        public ICompanionState Follow { get; private set; }

        /// <summary>Emir hedefine ilerleme durumu.</summary>
        public ICompanionState MoveToTarget { get; private set; }

        /// <summary>Hedefi etkinleştirme (şalter/plaka) durumu.</summary>
        public ICompanionState Activate { get; private set; }

        /// <summary>Takılma sonrası kurtarma durumu.</summary>
        public ICompanionState Recover { get; private set; }

        /// <summary>Savaş desteği durumu (yoksa <c>null</c>).</summary>
        public ICompanionState AssistCombat { get; private set; }

        /// <summary>Son taramada oyuncunun yakınında bulunan saldırgan hedef (yoksa <c>null</c>).</summary>
        public Yazlab.Core.IHostile CurrentThreat { get; set; }

        /// <summary>
        /// Savaş desteğine geçilebilir mi: özellik açık, durum bağlı ve geçerli bir tehdit var.
        /// Idle/Follow bu kontrolle <see cref="AssistCombat"/>'a geçer.
        /// </summary>
        public bool ShouldAssist =>
            AssistCombat != null && Config.CombatAssist && CurrentThreat != null && CurrentThreat.IsHostile;

        /// <summary>Görev sonrası oyuncuya dönüş durumu.</summary>
        public ICompanionState ReturnToPlayer { get; private set; }

        /// <summary>Emir görevi tamamlandığında veya başarısız olduğunda tetiklenir (<c>true</c>: başarılı).</summary>
        public event Action<bool> CommandCompleted;

        public CompanionContext(
            NavMeshAgent agent,
            CompanionConfig config,
            CompanionStateMachine machine,
            IObstacleSensor sensor = null)
        {
            Agent = agent;
            Config = config;
            Machine = machine;
            Sensor = sensor;
        }

        /// <summary>Geçişlerde kullanılacak durum örneklerini bağlar.</summary>
        public void BindStates(
            ICompanionState idle,
            ICompanionState follow,
            ICompanionState moveToTarget = null,
            ICompanionState activate = null,
            ICompanionState recover = null,
            ICompanionState returnToPlayer = null,
            ICompanionState assistCombat = null)
        {
            Idle = idle;
            Follow = follow;
            MoveToTarget = moveToTarget;
            Activate = activate;
            Recover = recover;
            ReturnToPlayer = returnToPlayer;
            AssistCombat = assistCombat;
        }

        /// <summary>Emir sonucunu dinleyicilere bildirir.</summary>
        public void NotifyCommandCompleted(bool success)
        {
            CommandCompleted?.Invoke(success);
        }

        /// <summary>Agent geçerli ve NavMesh üzerinde mi (yol komutu verilebilir mi).</summary>
        public bool CanNavigate => Agent != null && Agent.isActiveAndEnabled && Agent.isOnNavMesh;

        /// <summary>Oyuncu referansı geçerli mi.</summary>
        public bool HasPlayer => Player != null;

        /// <summary>Aktif ve etkileşime uygun bir görev hedefi var mı.</summary>
        public bool HasValidTarget =>
            CurrentTarget != null &&
            CurrentTarget.InteractionPoint != null &&
            !CurrentTarget.IsActivated;

        /// <summary>Köpek ile oyuncu arasındaki yatay mesafe (m). Oyuncu yoksa sonsuz.</summary>
        public float PlanarDistanceToPlayer()
        {
            if (!HasPlayer)
            {
                return float.PositiveInfinity;
            }

            return PlanarDistanceTo(Player.position);
        }

        /// <summary>Köpek ile verilen dünya noktası arasındaki yatay (XZ) mesafe (m).</summary>
        public float PlanarDistanceTo(Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - Agent.transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        /// <summary>Köpek ile aktif hedefin etkileşim noktası arasındaki yatay mesafe (m).</summary>
        public float PlanarDistanceToTarget()
        {
            if (CurrentTarget == null || CurrentTarget.InteractionPoint == null)
            {
                return float.PositiveInfinity;
            }

            float interactionDist = PlanarDistanceTo(CurrentTarget.InteractionPoint.position);
            if (CurrentTarget is Component comp && comp != null)
            {
                float rootDist = PlanarDistanceTo(comp.transform.position);
                return Mathf.Min(interactionDist, rootDist);
            }

            return interactionDist;
        }

        /// <summary>
        /// Verilen dünya konumunu köpeğin agent tipine uygun en yakın NavMesh noktasına oturtur.
        /// </summary>
        public bool TrySampleDestination(Vector3 worldPosition, float sampleRadius, out Vector3 destination)
        {
            destination = default;
            if (Agent == null)
            {
                return false;
            }

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = Agent.agentTypeID,
                areaMask = Agent.areaMask
            };

            if (NavMesh.SamplePosition(worldPosition, out NavMeshHit hit, Mathf.Max(0.5f, sampleRadius), filter))
            {
                destination = hit.position;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Oyuncunun konumunu köpeğin agent tipine uygun en yakın NavMesh noktasına oturtur.
        /// Oyuncu NavMesh dışındaysa (ör. ağaç dibi) yakındaki yürünebilir noktayı bulur.
        /// </summary>
        public bool TrySamplePlayerDestination(out Vector3 destination)
        {
            destination = default;
            if (!HasPlayer)
            {
                return false;
            }

            return TrySampleDestination(Player.position, Config.DestinationSampleRadius, out destination);
        }
    }
}
