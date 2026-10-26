using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Yazlab.Companion;
using Yazlab.Player;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Sahnedeki 'Kopek' objesini yeni NavMesh tabanlı companion yapısına geçirir (idempotent):
    /// CompanionConfig asset'i, CompanionAgent, PressurePlate için tetik collider + kinematic Rigidbody.
    /// </summary>
    public static class CompanionSetup
    {
        private const string DogObjectName = "Kopek";
        private const string ConfigPath = "Assets/_Project/ScriptableObjects/CompanionConfig.asset";
        private const float DogBodyRadius = 0.3f;
        private const float DogBodyHeight = 1.0f;

        /// <summary>Köpeği CompanionAgent ile donatır ve sahneyi kaydeder.</summary>
        [MenuItem("Yazlab/Kurulum/Companion (Kopek)")]
        public static void Run()
        {
            GameObject dog = GameObject.Find(DogObjectName);
            if (dog == null)
            {
                CompanionAgent existing = Object.FindFirstObjectByType<CompanionAgent>();
                if (existing != null)
                {
                    dog = existing.gameObject;
                    dog.name = DogObjectName;
                }
            }

            if (dog == null)
            {
                Debug.LogError("[Kurulum] 'Kopek' bulunamadı; önce 'Yazlab/Dusmanlari Kur' çalıştırılmalı.");
                return;
            }

            CompanionConfig config = LoadOrCreateConfig();

            // Eski sarmalayıcı artık gereksiz (içinde hareket kodu yok).
            if (dog.TryGetComponent(out CompanionController legacy))
            {
                Object.DestroyImmediate(legacy);
            }

            if (dog.TryGetComponent(out CharacterController cc))
            {
                cc.enabled = false;
            }

            if (!dog.TryGetComponent(out NavMeshAgent _))
            {
                Debug.LogError("[Kurulum] 'Kopek' üzerinde NavMeshAgent yok; önce 'Yazlab/Kurulum/NavMesh ve Kopek Agent' çalıştırılmalı.");
                return;
            }

            if (!dog.TryGetComponent(out CompanionAgent companion))
            {
                companion = dog.AddComponent<CompanionAgent>();
            }

            if (!dog.TryGetComponent(out RaycastSensor sensor))
            {
                sensor = dog.AddComponent<RaycastSensor>();
            }

            var sensorSo = new SerializedObject(sensor);
            sensorSo.FindProperty("_config").objectReferenceValue = config;
            sensorSo.FindProperty("_showGizmos").boolValue = true;
            sensorSo.ApplyModifiedPropertiesWithoutUndo();

            GameObject player = GameObject.FindWithTag("Player");
            var so = new SerializedObject(companion);
            so.FindProperty("_config").objectReferenceValue = config;
            so.FindProperty("_player").objectReferenceValue = player != null ? player.transform : null;
            so.FindProperty("_sensor").objectReferenceValue = sensor;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (player != null)
            {
                if (!player.TryGetComponent(out CompanionCommander commander))
                {
                    commander = player.AddComponent<CompanionCommander>();
                }

                PlayerInputReader inputReader = player.GetComponent<PlayerInputReader>();
                var commanderSo = new SerializedObject(commander);
                commanderSo.FindProperty("_companion").objectReferenceValue = companion;
                commanderSo.FindProperty("_inputReader").objectReferenceValue = inputReader;
                commanderSo.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(player);
            }

            GameObject commandZoneGo = GameObject.Find("CommandZone");
            if (commandZoneGo != null && !commandZoneGo.TryGetComponent(out CommandZone _))
            {
                commandZoneGo.AddComponent<CommandZone>();
                EditorUtility.SetDirty(commandZoneGo);
            }

            SetupPressurePlateTrigger(dog);

            // Köpeğin kendi collider'ları NavMesh bake'ine girip kendi altında delik açmasın.
            NavMeshModifier modifier = dog.GetComponent<NavMeshModifier>();
            if (modifier == null)
            {
                modifier = dog.AddComponent<NavMeshModifier>();
            }

            modifier.ignoreFromBuild = true;

            EditorUtility.SetDirty(dog);
            EditorSceneManager.MarkSceneDirty(dog.scene);
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Kurulum] Companion hazır — config={(config != null ? "OK" : "YOK")}, " +
                      $"oyuncu={(player != null ? player.name : "bulunamadı (Play'de etiketle aranır)")}. Sahne kaydedildi.");
        }

        private static CompanionConfig LoadOrCreateConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<CompanionConfig>(ConfigPath);
            if (config != null)
            {
                return config;
            }

            config = ScriptableObject.CreateInstance<CompanionConfig>();
            AssetDatabase.CreateAsset(config, ConfigPath);
            AssetDatabase.SaveAssets();
            return config;
        }

        /// <summary>
        /// PressurePlate'in OnTrigger olaylarını alabilmesi için köpeğe kinematic Rigidbody ve tetik collider ekler.
        /// </summary>
        private static void SetupPressurePlateTrigger(GameObject dog)
        {
            if (!dog.TryGetComponent(out Rigidbody body))
            {
                body = dog.AddComponent<Rigidbody>();
            }

            body.isKinematic = true;
            body.useGravity = false;

            if (!dog.TryGetComponent(out CapsuleCollider capsule))
            {
                capsule = dog.AddComponent<CapsuleCollider>();
            }

            // Kök ölçeğine böl: dünya boyutu sabit kalsın.
            float scale = Mathf.Max(0.0001f, dog.transform.lossyScale.x);
            capsule.isTrigger = true;
            capsule.direction = 1;
            capsule.radius = DogBodyRadius / scale;
            capsule.height = DogBodyHeight / scale;
            capsule.center = new Vector3(0f, DogBodyHeight * 0.5f / scale, 0f);
        }
    }
}
