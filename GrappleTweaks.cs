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
                OurLogger.LogInfo("Type mismatch, will not copy!");
                return;
            }


            OurLogger.LogInfo("Looping through component and compying data!");

            BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Default;
            PropertyInfo[] pinfos = type.GetProperties(flags);
            foreach (var pinfo in pinfos)
            {

                OurLogger.LogInfo("Property: " + pinfo.Name);
                if (pinfo.CanWrite)
                {
                    try
                    {
                        OurLogger.LogInfo("Setting value!");
                        pinfo.SetValue(copyComp, pinfo.GetValue(origComp, null), null);
                    }
                    catch {
                        OurLogger.LogInfo("Could not set value!");
                    } 
                }
            }
            FieldInfo[] finfos = type.GetFields(flags);
            foreach (var finfo in finfos)
            {
                OurLogger.LogInfo("Field: " + finfo.Name);
                OurLogger.LogInfo("Setting value!");
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



        [HarmonyPatch(typeof(Speedloader), "OnTriggerEnter")] // Specify target method with HarmonyPatch attribute
        [HarmonyPrefix]
        public static bool TriggerEnterPatch(Speedloader __instance, Collider c)
        {
            if (__instance.QuickbeltSlot == null)
            {
                RevolverCylinder component = c.GetComponent<RevolverCylinder>();
                if (component != null && component.Revolver.RoundType == __instance.Chambers[0].Type && component.CanAccept())
                {
                    __instance.HoveredCylinder = component;
                }
                RevolvingShotgunTrigger component2 = c.GetComponent<RevolvingShotgunTrigger>();
                bool flag = false;
                if (component2 != null && component2.Shotgun != null && component2.Shotgun.EjectDelay <= 0f && c.gameObject.CompareTag("FVRFireArmReloadTriggerWell") && component2.Shotgun.RoundType == __instance.Chambers[0].Type && __instance.SLType == component2.Shotgun.SLType)
                {
                    flag = true;
                }

                if(component2 != null)
                {
                    OurLogger.LogInfo("Component was not null");
                    OurLogger.LogInfo("Grapple gun: " + component2.GrappleGun.gameObject);
                    OurLogger.LogInfo("Grapple gun delay: " + component2.GrappleGun.EjectDelay);
                }
                else
                {
                    OurLogger.LogInfo("Component was null");
                }

                if (component2 != null && component2.GrappleGun != null && component2.GrappleGun.EjectDelay <= 0f && c.gameObject.CompareTag("FVRFireArmReloadTriggerWell") && component2.GrappleGun.RoundType == __instance.Chambers[0].Type && __instance.SLType == component2.GrappleGun.SLType && __instance.Chambers[0].IsLoaded && !__instance.Chambers[0].IsSpent && __instance.Chambers[1].IsLoaded && !__instance.Chambers[1].IsSpent)
                {
                    flag = true;
                    OurLogger.LogInfo("Hovered!");
                }
                else
                {
                    OurLogger.LogInfo("Not hovered");
                }

                if (flag)
                {
                    __instance.HoveredRSTrigger = component2;
                }
            }

            return false;
        }


    }
}
