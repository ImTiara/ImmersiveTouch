using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using VRC.SDKBase;

namespace ImmersiveTouch
{
    public static class Manager
    {
        public static VRCPlayer GetLocalVRCPlayer() => VRCPlayer.field_Internal_Static_VRCPlayer_0;

        public static VRCPlayerApi GetLocalVRCPlayerApi() => GetLocalVRCPlayer().prop_VRCPlayerApi_0;

        public static GameObject GetLocalAvatarObject() => GetLocalVRCPlayer().prop_VRCAvatarManager_0.prop_GameObject_0;
    }
}
