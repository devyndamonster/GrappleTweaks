using BepInEx;
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

        public void Awake()
        {
            Harmony.CreateAndPatchAll(typeof(GrappleTweaks));

            OurLogger = BepInEx.Logging.Logger.CreateLogSource("GrappleTweaks");
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


    }
}
