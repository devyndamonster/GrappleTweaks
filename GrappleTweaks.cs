using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using FistVR;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;

namespace GrappleTweaks
{
    [BepInPlugin("devyndamonster.h3vr.grappletweaks", "Grapple Tweaks", "0.1.0")]
    public class GrappleTweaks : BaseUnityPlugin
    {

        public static ManualLogSource OurLogger;
        public static ConfigEntry<float> MaxGrappleRange;

        private static float grappleRange;

        public void Awake()
        {
            OurLogger = BepInEx.Logging.Logger.CreateLogSource("GrappleTweaks");

            LoadConfigFile();

            Harmony.CreateAndPatchAll(typeof(GrappleTweaks));
        }


        private void LoadConfigFile()
        {
            MaxGrappleRange = Config.Bind(
                                    "General",
                                    "MaxGrappleRange",
                                    15.0f,
                                    "Sets how far the grapple gun can fire it's first bolt from"
                                    );

            grappleRange = MaxGrappleRange.Value;
        }


            public static void CopyFields(Component copyComp, Component origComp, bool allowMismatch = false)
        {
            Type type = origComp.GetType();
            if (!allowMismatch && type != copyComp.GetType())
            {
                return;
            }

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {

                if (pinfo.CanWrite)
                {
                    try
                    {
                        pinfo.SetValue(copyComp, pinfo.GetValue(origComp, null), null);
                    }
                    catch {
                        
                    } 
                }
            }
            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                finfo.SetValue(copyComp, finfo.GetValue(origComp));
            }
        }



        [HarmonyPatch(typeof(FVRHandGrabPoint), "GetGrabMoveDelta")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool SlideDeltaPatch(FVRHandGrabPoint __instance, ref Vector3 __result)
        {
            //If we are not sliding, just return zero
            if (!__instance.m_isSliding)
            {
                __result = Vector3.zero;
                return true;
            }


            //Determine if the player is power sliding by pressing a button
            bool isPoweredSlide = false;
            FVRViveHand hand = __instance.m_hand;
            if (hand != null)
            {
                if (hand.IsInStreamlinedMode)
                {
                    if (hand.Input.AXButtonPressed)
                    {
                        isPoweredSlide = true;
                    }
                }
                else
                {
                    if (hand.Input.TouchpadSouthPressed)
                    {
                        isPoweredSlide = true;
                    }
                }
            }


            //If powered sliding buttun is pressed, we move in direction of the players head
            if (isPoweredSlide)
            {
                Vector3 slideDir = __instance.SlidePoint1.position - __instance.SlidePoint0.position;
                
                //Calculate sliding speed
                float slideAccel = 1f;
                if (Vector3.Angle(GM.CurrentPlayerBody.Head.transform.forward, slideDir) > 90) __instance.m_slideSpeed -= slideAccel * Time.deltaTime;
                else __instance.m_slideSpeed += slideAccel * Time.deltaTime;
                __instance.m_slideSpeed = Mathf.Clamp(__instance.m_slideSpeed, -20f, 20f);

                //Return the delta
                __result = slideDir.normalized * __instance.m_slideSpeed * Time.deltaTime;
                return false;
            }


            //Otherwise, perform regular sliding
            else
            {
                Vector3 slideDir = __instance.SlidePoint1.position - __instance.SlidePoint0.position;

                float angle;
                if (slideDir.y > 0)
                {
                    angle = Vector3.Angle(-slideDir, Vector3.down);
                }
                else
                {
                    angle = Vector3.Angle(slideDir, Vector3.down);
                }

                //Calculate Accel
                float num2 = Mathf.InverseLerp(90f, 0f, angle);
                num2 = Mathf.Clamp(num2, 0.2f, 1f);
                float accel = 5f * num2;

                //Calculate sliding speed
                if (slideDir.y > 0) __instance.m_slideSpeed -= accel * Time.deltaTime;
                else __instance.m_slideSpeed += accel * Time.deltaTime;
                __instance.m_slideSpeed = Mathf.Clamp(__instance.m_slideSpeed, -20f, 20f);

                //Return the delta
                __result = slideDir.normalized * __instance.m_slideSpeed * Time.deltaTime;
                return false;
            }
        }



        [HarmonyPatch(typeof(FVRHandGrabPoint), "UpdateInteraction")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool UpdateInteractionPatch(FVRHandGrabPoint __instance, FVRViveHand hand)
        {
            //Since we can't call the base method UpdateInteraction, we have the contents of that method here
            __instance.IsHeld = true;
            __instance.m_hand = hand;
            if (!__instance.m_hasTriggeredUpSinceBegin && __instance.m_hand.Input.TriggerFloat < 0.15f)
            {
                __instance.m_hasTriggeredUpSinceBegin = true;
            }
            if (__instance.triggerCooldown > 0f)
            {
                __instance.triggerCooldown -= Time.deltaTime;
            }


            //Now we rework sliding so that you can move to next rope in either direction
            if (__instance.CanSlideOn)
            {
                if (hand.OtherHand.CurrentInteractable != null && hand.OtherHand.CurrentInteractable.DoesFunctionAsGrabPoint())
                {
                    __instance.m_isSliding = false;
                    __instance.m_slideSpeed = 0f;
                }
                else
                {
                    bool flag = false;
                    if (hand.IsInStreamlinedMode)
                    {
                        if (hand.Input.BYButtonPressed)
                        {
                            flag = true;
                        }
                    }
                    else if (hand.Input.TouchpadPressed)
                    {
                        flag = true;
                    }
                    if (flag)
                    {
                        __instance.m_isSliding = true;
                        Vector3 handPosition = hand.Input.Pos;
                        Vector3 basePointOffset = handPosition - __instance.SlidePoint0.position;
                        basePointOffset = Vector3.ProjectOnPlane(basePointOffset, __instance.SlidePoint0.up);
                        basePointOffset = Vector3.ProjectOnPlane(basePointOffset, __instance.SlidePoint0.right);
                        handPosition = __instance.SlidePoint0.position + basePointOffset;
                        float distToStart = Vector3.Distance(handPosition, __instance.SlidePoint0.position);
                        float distToEnd = Vector3.Distance(handPosition, __instance.SlidePoint1.position);
                        float totalDist = Vector3.Distance(__instance.SlidePoint0.position, __instance.SlidePoint1.position);
                        if (distToStart > totalDist)
                        {
                            //Remove the check for endpoint position comparison
                            if (__instance.ConnectedGrabPoint_End != null)
                            {
                                float slideSpeed = __instance.m_slideSpeed;
                                __instance.ForceBreakInteraction();
                                __instance.ConnectedGrabPoint_End.BeginInteraction(hand);
                                hand.ForceSetInteractable(__instance.ConnectedGrabPoint_End);
                                __instance.ConnectedGrabPoint_End.SetSlideSpeed(slideSpeed);
                            }

                            else
                            {
                                __instance.ForceBreakInteraction();
                            }

                        }
                        else if (distToEnd > totalDist)
                        {
                            //Remove the check for endpoint position comparison
                            if (__instance.ConnectedGrabPoint_Base != null)
                            {
                                float slideSpeed2 = __instance.m_slideSpeed;
                                __instance.ForceBreakInteraction();
                                __instance.ConnectedGrabPoint_Base.BeginInteraction(hand);
                                hand.ForceSetInteractable(__instance.ConnectedGrabPoint_Base);
                                __instance.ConnectedGrabPoint_Base.SetSlideSpeed(slideSpeed2);
                            }

                            else
                            {
                                __instance.ForceBreakInteraction();
                            }
                        }
                    }
                    else
                    {
                        __instance.m_isSliding = false;
                        __instance.m_slideSpeed = 0f;
                    }
                }
            }

            return false;
        }




        [HarmonyPatch(typeof(GrappleGun), "Awake")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool GrappleSwapPatch(GrappleGun __instance)
        {
            OurLogger.LogInfo("Awake of grapple gun");

            //Make sure to check that we are not already an extended grapple gun
            if (__instance.gameObject.GetComponent<GrappleGunExtended>() != null)
            {
                OurLogger.LogInfo("We already have a grapple gun components, skipping setup");
                return true;
            }
            
            
            GrappleGunExtended newGrapple = __instance.gameObject.AddComponent<GrappleGunExtended>();
            GrappleGun oldGrapple = __instance.gameObject.GetComponent<GrappleGun>();

            OurLogger.LogInfo("Has old grapple: " + oldGrapple != null);
            OurLogger.LogInfo("Has new grapple: " + newGrapple != null);

            CopyFields(newGrapple, oldGrapple, true);

            foreach(RevolvingShotgunTrigger trigger in __instance.transform.GetComponentsInChildren<RevolvingShotgunTrigger>())
            {
                trigger.GrappleGun = newGrapple;
            }

            OurLogger.LogInfo("Destroying old grapple comp");
            oldGrapple.enabled = false;
            Destroy(oldGrapple);

            newGrapple.DelayedAwake();

            return true;
        }




        [HarmonyPatch(typeof(GrappleGun), "UpdateInputAndAnimate")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool GrappleInputPatch(GrappleGun __instance)
        {
            return false;
        }



        [HarmonyPatch(typeof(GrappleGun), "CanFireCheck")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool GrappleInputPatch(GrappleGun __instance, ref bool __result)
        {
            if (!__instance.IsHeld)
            {
                __result = false;
                return false;
            }
            if (!__instance.IsMagLoaded)
            {
                __result = false;
                return false;
            }
            if (__instance.IsRetracting)
            {
                __result = false;
                return false;
            }
            if (__instance.m_firstShotBolt != null && __instance.m_firstShotBolt.HasStruckLink())
            {
                __result = false;
                return false;
            }
            if (__instance.Chambers[__instance.m_curChamber].IsSpent || !__instance.Chambers[__instance.m_curChamber].IsFull)
            {
                __result = false;
                return false;
            }
            if (__instance.m_curChamber == 0)
            {
                __result = Physics.Raycast(__instance.GetMuzzle().position, __instance.GetMuzzle().forward, out __instance.m_hit, grappleRange, __instance.LM_EnvCheck);
                return false;
            }

            __result = __instance.m_curChamber == 1 && (__instance.m_firstShotBolt != null && __instance.m_firstShotBolt.HasStruck());
            return false;
        }


    }
}
