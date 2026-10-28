using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Yazlab.EditorTools
{
    /// <summary>
    /// Oyuncu Animator'ünü tek bir "Locomotion" 1D blend tree'ye çevirir (Idle 0 → Walk → Sprint, parametre Speed).
    /// Klip hızları ayağın yere bastığı anda kaymayacağı şekilde ölçülerek kalibre edildi (yürüme 4.5, koşu 12 m/s;
    /// karakter ölçeği 2.29). Eski sabit hızlı Walk/Sprint durumları ve aralarındaki ani geçişler kaldırılır;
    /// Punch/Death durumları ve tetikleyicileri korunur (koşarken de saldırı yapılabilir). Idempotent.
    /// </summary>
    public static class PlayerLocomotionSetup
    {
        private const string ControllerPath = "Assets/PlayerAnimator.controller";
        private const string AnimFbx = "Assets/Universal Animation Library[Standard]/Universal Animation Library[Standard]/Unity/UAL1_Standard.fbx";
        private const string LocomotionName = "Locomotion";

        // Oyun hızlarıyla aynı (PlayerConfig: walk 4.5, run 12).
        private const float WalkThreshold = 4.5f;
        private const float RunThreshold = 12f;

        /// <summary>Yürüme klibinin oynatma hızı (ölçülen doğal hız ≈ 2.0 m/s → 4.5 m/s).</summary>
        public static float WalkTimeScale = 2.2f;

        /// <summary>Sprint klibinin oynatma hızı (12 m/s'de ayak kaymasın).</summary>
        public static float RunTimeScale = 0.87f;

        [MenuItem("Yazlab/Gorsel/Oyuncu Kosu Animasyonu")]
        public static void Run()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(AnimFbx).OfType<AnimationClip>().ToArray();
            AnimationClip idle = clips.FirstOrDefault(c => c.name == "Armature|Idle_Loop");
            AnimationClip walk = clips.FirstOrDefault(c => c.name == "Armature|Walk_Loop");
            AnimationClip run = clips.FirstOrDefault(c => c.name == "Armature|Sprint_Loop");
            if (controller == null || idle == null || walk == null || run == null)
            {
                Debug.LogError("[Gorsel] PlayerAnimator veya Idle/Walk/Sprint klipleri bulunamadı.");
                return;
            }

            AnimatorStateMachine sm = controller.layers[0].stateMachine;
            AnimatorState punch = Find(sm, "Punch");
            AnimatorState death = Find(sm, "Death");

            // Eski hareket durumlarını (ve önceki Locomotion'ı) kaldır; blend tree alt varlıkları da silinir.
            foreach (ChildAnimatorState child in sm.states.ToArray())
            {
                AnimatorState s = child.state;
                if (s != punch && s != death)
                {
                    if (s.motion is BlendTree oldTree)
                    {
                        Object.DestroyImmediate(oldTree, true);
                    }

                    sm.RemoveState(s);
                }
            }

            AnimatorState locomotion = controller.CreateBlendTreeInController(LocomotionName, out BlendTree tree, 0);
            tree.name = "LocomotionTree";
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(walk, WalkThreshold);
            tree.AddChild(run, RunThreshold);
            ChildMotion[] children = tree.children;
            children[1].timeScale = WalkTimeScale;
            children[2].timeScale = RunTimeScale;
            tree.children = children;
            sm.defaultState = locomotion;
            SetPosition(sm, locomotion, new Vector3(300f, 100f, 0f));

            if (punch != null)
            {
                ClearTransitions(punch);
                AnimatorStateTransition toPunch = locomotion.AddTransition(punch);
                toPunch.hasExitTime = false;
                toPunch.duration = 0.08f;
                toPunch.AddCondition(AnimatorConditionMode.If, 0f, "Attack");

                AnimatorStateTransition back = punch.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.8f;
                back.duration = 0.15f;
            }

            if (death != null)
            {
                ClearTransitions(death);
                AnimatorStateTransition toDeath = locomotion.AddTransition(death);
                toDeath.hasExitTime = false;
                toDeath.duration = 0.15f;
                toDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");

                if (punch != null)
                {
                    AnimatorStateTransition punchDeath = punch.AddTransition(death);
                    punchDeath.hasExitTime = false;
                    punchDeath.duration = 0.15f;
                    punchDeath.AddCondition(AnimatorConditionMode.If, 0f, "Die");
                }

                AnimatorStateTransition revive = death.AddTransition(locomotion);
                revive.hasExitTime = false;
                revive.duration = 0.1f;
                revive.AddCondition(AnimatorConditionMode.If, 0f, "Revive");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Gorsel] Oyuncu Locomotion blend tree kuruldu (walk ×{WalkTimeScale}, sprint ×{RunTimeScale}).");
        }

        private static AnimatorState Find(AnimatorStateMachine sm, string name) =>
            sm.states.Select(s => s.state).FirstOrDefault(s => s.name == name);

        private static void ClearTransitions(AnimatorState state)
        {
            foreach (AnimatorStateTransition t in state.transitions.ToArray())
            {
                state.RemoveTransition(t);
            }
        }

        private static void SetPosition(AnimatorStateMachine sm, AnimatorState state, Vector3 position)
        {
            ChildAnimatorState[] states = sm.states;
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].state == state)
                {
                    states[i].position = position;
                }
            }

            sm.states = states;
        }
    }
}
