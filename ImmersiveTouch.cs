using MelonLoader;
using System;
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
        public const string Version = "1.0.0";
        public const string DownloadLink = "https://github.com/ImTiara/ImmersiveTouch/releases";
    }

    public class ImmersiveTouch : MelonMod
    {

        private static bool m_Enable;
        private static bool m_IsCapable;

        private static float m_HapticDuration = 0.001f;
        private static float m_HapticAmplitude = 100.0f;
        private static float m_HapticFrequency = 0.001f;
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

            OnPreferencesSaved();

            Hooks.ApplyPatches();
        }

        public override void OnPreferencesSaved()
        {
            m_Enable = MelonPreferences.GetEntryValue<bool>(GetType().Name, "Enable");

            if (m_Enable) TryCapability();
        }

        unsafe public static void OnAvatarChanged(IntPtr instance, IntPtr _, IntPtr __, IntPtr ___)
        {
            Hooks.avatarChangedDelegate(instance, _, __, ___);

            VRCAvatarManager avatarManager = new VRCAvatarManager(instance);
            if (avatarManager != null && avatarManager.field_Private_VRCPlayer_0.Equals(VRCPlayer.field_Internal_Static_VRCPlayer_0)) TryCapability();
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
                VRCPlayer.field_Internal_Static_VRCPlayer_0.prop_VRCPlayerApi_0.PlayHapticEventInHand(VRC_Pickup.PickupHand.Left, m_HapticDuration, m_HapticAmplitude, m_HapticFrequency);

                m_PreviousLeftWristPosition = position;
            }

            if (instance.Equals(m_RightWristCollider) && Vector3.Distance(m_PreviousRightWristPosition, position) > m_HapticDistance)
            {
                VRCPlayer.field_Internal_Static_VRCPlayer_0.prop_VRCPlayerApi_0.PlayHapticEventInHand(VRC_Pickup.PickupHand.Right, m_HapticDuration, m_HapticAmplitude, m_HapticFrequency);
                
                m_PreviousRightWristPosition = position;
            }
        }

        private static void TryCapability()
        {
            try
            {
                m_LeftWristIntPtr = IntPtr.Zero;
                m_RightWristIntPtr = IntPtr.Zero;

                GameObject avatarObject = VRCPlayer.field_Internal_Static_VRCPlayer_0.prop_VRCAvatarManager_0.prop_GameObject_0;
                Animator animator = avatarObject.GetComponent<Animator>();

                // TODO Make distance local scale dependent instead of world.
                m_HapticDistance = 0.001f;

                // TODO: Find a better method to do this.
                m_LeftWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.LeftHand).GetInstanceID());
                if (m_LeftWristCollider == null)
                    m_LeftWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.LeftIndexDistal).GetInstanceID());
                if (m_LeftWristCollider == null)
                    m_LeftWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate).GetInstanceID());
                if (m_LeftWristCollider == null)
                    m_LeftWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.LeftIndexProximal).GetInstanceID());

                m_RightWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.RightHand).GetInstanceID());
                if (m_RightWristCollider == null)
                    m_RightWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.RightIndexDistal).GetInstanceID());
                if (m_RightWristCollider == null)
                    m_RightWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.RightIndexIntermediate).GetInstanceID());
                if (m_RightWristCollider == null)
                    m_RightWristCollider = avatarObject.GetComponentsInChildren<DynamicBoneCollider>().FirstOrDefault(x => x.transform.GetInstanceID() == animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).GetInstanceID());

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