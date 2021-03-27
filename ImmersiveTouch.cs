using MelonLoader;
using System;
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
        private static bool m_IsCapable;

        private static float m_HapticAmplitude = 100.0f;
        private static float m_HapticDistance;

        private static Vector3 m_PreviousLeftWristPosition;
        private static Vector3 m_PreviousRightWristPosition;

        private static DynamicBoneCollider m_LeftWristCollider;
        private static DynamicBoneCollider m_RightWristCollider;

        private static IntPtr m_LeftWristIntPtr;
        private static IntPtr m_RightWristIntPtr;

        public override void OnApplicationStart()
        {
            MelonPreferences.CreateCategory(GetType().Name, "Immersive Touch");
            MelonPreferences.CreateEntry(GetType().Name, "Enable", true, "Enable Immersive Touch");
            MelonPreferences.CreateEntry(GetType().Name, "HapticAmplitude", 100.0f, "Haptic Amplitude (%)");

            OnPreferencesSaved();

            Hooks.ApplyPatches();
        }

        public override void OnPreferencesSaved()
        {
            m_Enable = MelonPreferences.GetEntryValue<bool>(GetType().Name, "Enable");
            m_HapticAmplitude = MelonPreferences.GetEntryValue<float>(GetType().Name, "HapticAmplitude") / 100.0f;

            if (m_Enable) TryCapability();
        }

        unsafe public static void OnAvatarChanged(IntPtr instance, IntPtr _, IntPtr __, IntPtr ___)
        {
            Hooks.avatarChangedDelegate(instance, _, __, ___);

            VRCAvatarManager avatarManager = new VRCAvatarManager(instance);
            if (avatarManager != null && avatarManager.field_Private_VRCPlayer_0.Equals(VRCPlayer.field_Internal_Static_VRCPlayer_0))
            {
                Animator animator = Manager.GetLocalAvatarObject().GetComponent<Animator>();
                if (animator == null) return;

                float scale = Vector3.Distance(animator.GetBoneTransform(HumanBodyBones.LeftHand).position, animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                m_HapticDistance = scale / 785.0f;

                TryCapability();
            }
        }

        unsafe public static void OnCollide(IntPtr instance, IntPtr particlePosition, IntPtr particleRadius)
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
                    SendHaptic(new DynamicBoneCollider(instance));
                }
            }
            catch { InvokeCollide(); }
        }

        private static void SendHaptic(DynamicBoneCollider instance)
        {
            Vector3 position = instance.transform.position;

            if (instance.Equals(m_LeftWristCollider) && Vector3.Distance(m_PreviousLeftWristPosition, position) > m_HapticDistance)
            {
                Manager.GetLocalVRCPlayerApi().PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, 0.001f, m_HapticAmplitude, 0.001f);

                m_PreviousLeftWristPosition = position;
            }

            if (instance.Equals(m_RightWristCollider) && Vector3.Distance(m_PreviousRightWristPosition, position) > m_HapticDistance)
            {
                Manager.GetLocalVRCPlayerApi().PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, 0.001f, m_HapticAmplitude, 0.001f);
                
                m_PreviousRightWristPosition = position;
            }
        }

        private static void TryCapability()
        {
            try
            {
                m_LeftWristIntPtr = IntPtr.Zero;
                m_RightWristIntPtr = IntPtr.Zero;

                Animator animator = Manager.GetLocalAvatarObject().GetComponent<Animator>();

                int leftCount = 6;
                while(m_LeftWristCollider == null && leftCount >= 0)
                {
                    switch(leftCount)
                    {
                        case 6:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftHand).GetComponent<DynamicBoneCollider>();
                            break;
                        case 5:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 4:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate).GetComponent<DynamicBoneCollider>();
                            break;
                        case 3:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 2:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftMiddleDistal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 1:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftMiddleIntermediate).GetComponent<DynamicBoneCollider>();
                            break;
                        case 0:
                            m_LeftWristCollider = animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal).GetComponent<DynamicBoneCollider>();
                            break;
                    }
                    leftCount--;
                }

                int rightCount = 6;
                while (m_RightWristCollider == null && rightCount >= 0)
                {
                    switch (rightCount)
                    {
                        case 6:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightHand).GetComponent<DynamicBoneCollider>();
                            break;
                        case 5:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightIndexDistal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 4:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate).GetComponent<DynamicBoneCollider>();
                            break;
                        case 3:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 2:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightMiddleDistal).GetComponent<DynamicBoneCollider>();
                            break;
                        case 1:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightMiddleIntermediate).GetComponent<DynamicBoneCollider>();
                            break;
                        case 0:
                            m_RightWristCollider = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).GetComponent<DynamicBoneCollider>();
                            break;
                    }
                    rightCount--;
                }

                m_IsCapable = m_LeftWristCollider != null && m_RightWristCollider != null;

                if (m_IsCapable)
                {
                    m_LeftWristIntPtr = m_LeftWristCollider.Pointer;
                    m_RightWristIntPtr = m_RightWristCollider.Pointer;
                }
            }
            catch { NotCapable(); }

            void NotCapable()
            {
                m_IsCapable = false;
                return;
            }
        }
    }
}