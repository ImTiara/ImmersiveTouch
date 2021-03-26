using MelonLoader;
using System;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ImmersiveTouch
{
    public class Hooks
    {
        public delegate void AvatarChangedDelegate(IntPtr instance, IntPtr _, IntPtr __, IntPtr ___);
        public static AvatarChangedDelegate avatarChangedDelegate;

        public delegate void CollideDelegate(IntPtr instance, IntPtr particlePosition, IntPtr particleRadius);
        public static CollideDelegate collideDelegate;

        unsafe public static void ApplyPatches()
        {
            string avatarChangedField = "NativeMethodInfoPtr_Method_Private_Void_ApiAvatar_GameObject_MulticastDelegateNPublicSealedVoGaVRBoUnique_0";
            string collideField = "NativeMethodInfoPtr_Method_Public_Void_byref_Vector3_Single_0";

            try
            {
                var avatarChangedTarget = *(IntPtr*)(IntPtr)typeof(VRCAvatarManager).GetField(avatarChangedField, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                MelonUtils.NativeHookAttach((IntPtr)(&avatarChangedTarget), Marshal.GetFunctionPointerForDelegate(new Action<IntPtr, IntPtr, IntPtr, IntPtr>(ImmersiveTouch.OnAvatarChanged)));
                avatarChangedDelegate = Marshal.GetDelegateForFunctionPointer<AvatarChangedDelegate>(avatarChangedTarget);
            }
            catch (Exception e) { MelonLogger.Error($"Failed to patch: VRCAvatarManager.{avatarChangedField}\n{e}"); }

            try
            {
                var collideTarget = *(IntPtr*)(IntPtr)typeof(DynamicBoneCollider).GetField(collideField, BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
                MelonUtils.NativeHookAttach((IntPtr)(&collideTarget), Marshal.GetFunctionPointerForDelegate(new Action<IntPtr, IntPtr, IntPtr>(ImmersiveTouch.OnCollide)));
                collideDelegate = Marshal.GetDelegateForFunctionPointer<CollideDelegate>(collideTarget);
            }
            catch (Exception e) { MelonLogger.Error($"Failed to patch: DynamicBoneCollider.{collideField}\n{e}"); }
        }
    }
}
