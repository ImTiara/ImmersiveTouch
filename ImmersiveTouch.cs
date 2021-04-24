using MelonLoader;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using VRC.SDKBase;

namespace ImmersiveTouch
{
    public static class BuildInfo
    {
        public const string Name = "ImmersiveTouch";
        public const string Author = "ImTiara";
        public const string Company = null;
        public const string Version = "1.0.1";
        public const string DownloadLink = "https://github.com/ImTiara/ImmersiveTouch/releases";
    }

    public class ImmersiveTouch : MelonMod
    {
        private static bool m_Enable;
        private static bool m_IgnoreSelf;
        private static bool m_IsCapable;

        private static float m_HapticAmplitude;
        private static float m_HapticDistance;

        private static Vector3 m_PreviousLeftWristPosition;
        private static Vector3 m_PreviousRightWristPosition;

        private static DynamicBoneCollider m_LeftWristCollider;
        private static DynamicBoneCollider m_RightWristCollider;

        private static IntPtr m_LeftWristIntPtr;
        private static IntPtr m_RightWristIntPtr;

        private static ColliderPrioritization m_ColliderPrioritization = ColliderPrioritization.Wrist;
        private static string colliderPrioritization = "Wrist";

        private static Transform m_CurrentAvatarTransform;

        [ThreadStatic] static IntPtr m_CurrentDBI;

        private static List<IntPtr> m_LocalDynamicBonePointers = new List<IntPtr>();

        public override void VRChat_OnUiManagerInit()
        {
            Manager.RegisterUIExpansionKit();

            MelonPreferences.CreateCategory(GetType().Name, "Immersive Touch");
            MelonPreferences.CreateEntry(GetType().Name, "Enable", true, "Enable Immersive Touch");
            MelonPreferences.CreateEntry(GetType().Name, "HapticAmplitude", 100.0f, "Haptic Amplitude (%)");
            MelonPreferences.CreateEntry(GetType().Name, "ColliderPrioritization", colliderPrioritization, "Collider Prioritization");
            MelonPreferences.CreateEntry(GetType().Name, "IgnoreSelf", true, "Ignore Self");

            OnPreferencesSaved();

            Hooks.ApplyPatches();
        }

        public override void OnPreferencesSaved()
        {
            m_Enable = MelonPreferences.GetEntryValue<bool>(GetType().Name, "Enable");
            m_HapticAmplitude = MelonPreferences.GetEntryValue<float>(GetType().Name, "HapticAmplitude") / 100.0f;

            colliderPrioritization = MelonPreferences.GetEntryValue<string>(GetType().Name, "ColliderPrioritization");
            Manager.UIExpansionKit_RegisterSettingAsStringEnum(GetType().Name, "ColliderPrioritization", new[]
            {
                ("Wrist", "Wrist (Any hand collider)"),
                ("Thumb", "Thumb"),
                ("Index", "Index Finger"),
                ("Middle", "Middle Finger"),
                ("Ring", "Ring Finger"),
                ("Pinky", "Pinky Finger")
            });
            Enum.TryParse(colliderPrioritization, out m_ColliderPrioritization);

            m_IgnoreSelf = MelonPreferences.GetEntryValue<bool>(GetType().Name, "IgnoreSelf");

            TryCapability();
        }

        public static unsafe void OnAvatarChanged(IntPtr instance, IntPtr __0, IntPtr __1, IntPtr __2)
        {
            Hooks.avatarChangedDelegate(instance, __0, __1, __2);

            try
            {
                VRCAvatarManager avatarManager = new VRCAvatarManager(instance);
                if (avatarManager != null && avatarManager.GetInstanceID().Equals(Manager.GetLocalAvatarManager().GetInstanceID()))
                {
                    Animator animator = avatarManager.field_Private_Animator_0;
                    if (animator == null || !animator.isHuman) return;

                    float scale = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position, animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                    m_HapticDistance = scale / 785.0f;

                    m_CurrentAvatarTransform = avatarManager.prop_GameObject_0.transform;

                    TryCapability();
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Error checking when avatar changed:\n{e}");
            }
        }

        public static unsafe void OnUpdateParticles(IntPtr instance, bool __0)
        {
            m_CurrentDBI = instance;

            Hooks.updateParticlesDelegate(instance, __0);
        }

        public static unsafe void OnCollide(IntPtr instance, IntPtr particlePosition, float particleRadius)
        {
            void InvokeCollide() => Hooks.collideDelegate(instance, particlePosition, particleRadius);

            try
            {
                if (!m_Enable || !m_IsCapable || (!instance.Equals(m_LeftWristIntPtr) && !instance.Equals(m_RightWristIntPtr)))
                {
                    InvokeCollide();
                    return;
                }

                // Store the original particle position and invoke the original method.
                Vector3 prevParticlePos = Marshal.PtrToStructure<Vector3>(particlePosition);
                InvokeCollide();

                // If the particle position was changed after the invoke, we have a collision!
                if (!prevParticlePos.Equals(Marshal.PtrToStructure<Vector3>(particlePosition)))
                {
                    SendHaptic(instance);
                }
            }
            catch
            {
                InvokeCollide();
            }
        }

        private static void SendHaptic(IntPtr instance)
        {
            if (m_IgnoreSelf && m_LocalDynamicBonePointers.Contains(m_CurrentDBI)) return;

            DynamicBoneCollider dynamicBoneCollider = new DynamicBoneCollider(instance);

            Vector3 position = dynamicBoneCollider.transform.position;

            if (instance.Equals(m_LeftWristIntPtr) && Vector3.Distance(m_PreviousLeftWristPosition, position) > m_HapticDistance)
            {
                Manager.GetLocalVRCPlayerApi().PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.001f, m_HapticAmplitude, 0.001f);

                m_PreviousLeftWristPosition = position;
            }

            if (instance.Equals(m_RightWristIntPtr) && Vector3.Distance(m_PreviousRightWristPosition, position) > m_HapticDistance)
            {
                Manager.GetLocalVRCPlayerApi().PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.001f, m_HapticAmplitude, 0.001f);
                
                m_PreviousRightWristPosition = position;
            }
        }

        private static void TryCapability()
        {
            if (!m_Enable || Manager.GetLocalVRCPlayer() == null) return;

            try
            {
                Animator animator = Manager.GetLocalAvatarAnimator();
                if (animator == null || !animator.isHuman) NotCapable();

                HumanBodyBones leftBoneTarget = HumanBodyBones.LeftHand;
                HumanBodyBones rightBoneTarget = HumanBodyBones.RightHand;

                switch (m_ColliderPrioritization)
                {
                    case ColliderPrioritization.Wrist:
                        leftBoneTarget = HumanBodyBones.LeftHand;
                        rightBoneTarget = HumanBodyBones.RightHand;
                        break;
                    case ColliderPrioritization.Thumb:
                        leftBoneTarget = HumanBodyBones.LeftThumbProximal;
                        rightBoneTarget = HumanBodyBones.RightThumbProximal;
                        break;
                    case ColliderPrioritization.Index:
                        leftBoneTarget = HumanBodyBones.LeftIndexProximal;
                        rightBoneTarget = HumanBodyBones.RightIndexProximal;
                        break;
                    case ColliderPrioritization.Middle:
                        leftBoneTarget = HumanBodyBones.LeftMiddleProximal;
                        rightBoneTarget = HumanBodyBones.RightMiddleProximal;
                        break;
                    case ColliderPrioritization.Ring:
                        leftBoneTarget = HumanBodyBones.LeftRingProximal;
                        rightBoneTarget = HumanBodyBones.RightRingProximal;
                        break;
                    case ColliderPrioritization.Pinky:
                        leftBoneTarget = HumanBodyBones.LeftLittleProximal;
                        rightBoneTarget = HumanBodyBones.RightLittleProximal;
                        break;
                }

                var leftHandColliders = animator.GetDynamicBoneColliders(leftBoneTarget);
                var rightHandColliders = animator.GetDynamicBoneColliders(rightBoneTarget);

                if (m_ColliderPrioritization != ColliderPrioritization.Wrist && (leftHandColliders.Count == 0 || rightHandColliders.Count == 0))
                {
                    leftHandColliders = animator.GetDynamicBoneColliders(HumanBodyBones.LeftHand);
                    rightHandColliders = animator.GetDynamicBoneColliders(HumanBodyBones.RightHand);
                }

                m_LeftWristCollider = leftHandColliders.Count != 0 ? leftHandColliders[0] : null;
                m_RightWristCollider = rightHandColliders.Count != 0 ? rightHandColliders[0] : null;

                m_LeftWristIntPtr = IntPtr.Zero;
                m_RightWristIntPtr = IntPtr.Zero;

                m_IsCapable = m_LeftWristCollider != null && m_RightWristCollider != null;

                if (m_IsCapable)
                {
                    m_LeftWristIntPtr = m_LeftWristCollider.Pointer;
                    m_RightWristIntPtr = m_RightWristCollider.Pointer;

                    m_LocalDynamicBonePointers.Clear();
                    foreach (var db in m_CurrentAvatarTransform.gameObject.GetDynamicBones())
                        m_LocalDynamicBonePointers.Add(db.Pointer);

                    MelonLogger.Msg($"Listening for collisions on \"{m_LeftWristCollider.gameObject.name}\" and \"{m_RightWristCollider.gameObject.name}\".");
                }
                else
                    MelonLogger.Msg($"This avatar is not capable for Immersive Touch.");
            }
            catch(Exception e) {
                MelonLogger.Error($"Error when checking capability\n{e}");
                NotCapable();
            }

            void NotCapable()
            {
                m_IsCapable = false;
                return;
            }
        }

        private enum ColliderPrioritization
        {
            Wrist,
            Thumb,
            Index,
            Middle,
            Ring,
            Pinky
        }
    }
}