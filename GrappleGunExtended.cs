using FistVR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace GrappleTweaks
{
    public class GrappleGunExtended : GrappleGun
    {
        public bool isInSwingMode = false;
        public bool isBoltPlanted = false;
        public float savedDistance = 0;
        public float retractSpeed = 1;

        public override void Awake()
        {
            return;
        }

        public void DelayedAwake()
        {
            base.Awake();
        }

        public bool ShouldSwing()
        {
            return isInSwingMode && m_firstShotBolt != null && m_firstShotBolt.HasStruck() && m_secondShotBolt == null;
        }

        public override void UpdateInteraction(FVRViveHand hand)
        {
            UpdateMode(hand);
            UpdateInputAndAnimateReplacement(hand);
            base.UpdateInteraction(hand);
        }

        public override void FVRUpdate()
        {
            
            UpdateBoltState();
            base.FVRUpdate();
            UpdateSwing();
        }


        public void UpdateBoltState()
        {
            //If we do not have a line connecting a wall to this gun, the bolt is not planted
            if (isBoltPlanted && !ShouldSwing())
            {
                isBoltPlanted = false;
            }
            

            //If we do have a line connecting the gun to a wall, and we previously were not planted, we perform planted actions
            if(!isBoltPlanted && ShouldSwing())
            {
                isBoltPlanted = true;
                SetLastGrabHandPos(transform.position);
                savedDistance = Vector3.Distance(m_firstShotBolt.transform.position, transform.position);
            }
        }

        private void UpdateSwing()
        {
            if (ShouldSwing())
            {
                float currDistance = Vector3.Distance(transform.position, m_firstShotBolt.transform.position);
                if (currDistance > savedDistance)
                {
                    Vector3 moveDelta = (m_firstShotBolt.transform.position - transform.position).normalized * (currDistance - savedDistance);

                    transform.position = transform.position + moveDelta;

                    if(m_hand != null || m_quickbeltSlot != null)
                    {
                        GM.CurrentPlayerBody.transform.position = GM.CurrentPlayerBody.transform.position + moveDelta;

                        if(Vector3.Angle(GM.CurrentMovementManager.m_armSwingerVelocity, moveDelta) > 90)
                        {
                            GM.CurrentMovementManager.m_armSwingerVelocity = Vector3.ProjectOnPlane(GM.CurrentMovementManager.m_armSwingerVelocity, moveDelta);
                        }

                        if (Vector3.Angle(GM.CurrentMovementManager.m_twoAxisVelocity, moveDelta) > 90)
                        {
                            GM.CurrentMovementManager.m_twoAxisVelocity = Vector3.ProjectOnPlane(GM.CurrentMovementManager.m_twoAxisVelocity, moveDelta);
                        }
                    }

                    else
                    {
                        if (Vector3.Angle(RootRigidbody.velocity, moveDelta) > 90)
                        {
                            RootRigidbody.velocity = Vector3.ProjectOnPlane(RootRigidbody.velocity, moveDelta);
                        }
                    }
                }
            }
        }



        public void UpdateInputAndAnimateReplacement(FVRViveHand hand)
        {
            if (IsAltHeld)
            {
                return;
            }

            if (m_hasTriggeredUpSinceBegin)
            {
                m_triggerTarget = hand.Input.TriggerFloat;
            }
            if (m_triggerTarget > m_triggerFloat)
            {
                m_triggerFloat = Mathf.MoveTowards(m_triggerFloat, m_triggerTarget, Time.deltaTime * 20f);
            }
            else
            {
                m_triggerFloat = Mathf.MoveTowards(m_triggerFloat, m_triggerTarget, Time.deltaTime * 20f * 2f);
            }
            if (!HasTriggerReset && m_triggerFloat <= TriggerResetThreshold)
            {
                HasTriggerReset = true;
                base.PlayAudioEvent(FirearmAudioEventType.TriggerReset, 1f);
            }
            if (hand.IsInStreamlinedMode)
            {
                if (!isInSwingMode && hand.Input.AXButtonDown)
                {
                    AttemptRetract(true);
                }

                else if (isInSwingMode && hand.Input.AXButtonPressed)
                {
                    RetractSwing();
                }

                else if (hand.Input.BYButtonDown)
                {
                    AttemptRetract(false);
                }
            }
            else if (hand.Input.TouchpadAxes.magnitude > 0.2f)
            {
                if (!isInSwingMode && hand.Input.TouchpadDown && Vector2.Angle(hand.Input.TouchpadAxes, Vector2.down) <= 45f)
                {
                    AttemptRetract(true);
                }

                else if (isInSwingMode && hand.Input.TouchpadPressed && Vector2.Angle(hand.Input.TouchpadAxes, Vector2.down) <= 45f)
                {
                    RetractSwing();
                }

                else if (hand.Input.TouchpadDown && Vector2.Angle(hand.Input.TouchpadAxes, Vector2.up) <= 45f)
                {
                    AttemptRetract(false);
                }
            }
            if (m_triggerFloat >= TriggerBreakThreshold && HasTriggerReset)
            {
                HasTriggerReset = false;
                if (m_canFire)
                {
                    Fire();
                }
            }
        }


        private void RetractSwing()
        {
            if (isBoltPlanted)
            {
                savedDistance = Mathf.Min(savedDistance, Vector3.Distance(m_firstShotBolt.transform.position, transform.position)) - (retractSpeed * Time.deltaTime);
            }
        }



        private void UpdateMode(FVRViveHand hand)
        {
            if (m_firstShotBolt != null) return;

            if (hand.IsInStreamlinedMode)
            {
                if (hand.Input.AXButtonDown)
                {
                    isInSwingMode = !isInSwingMode;

                    Debug.Log("Toggling swing mode to: " + isInSwingMode);
                    if (isInSwingMode)
                    {
                        Light_Green.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", new Color(0, 0, 10, 1));
                    }
                    else
                    {
                        Light_Green.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", new Color(2, 2, 2, 1));
                    }
                }
            }
            else
            {
                if (hand.Input.TouchpadDown && hand.Input.TouchpadAxes.magnitude > 0.2f)
                {
                    isInSwingMode = !isInSwingMode;

                    Debug.Log("Toggling swing mode to: " + isInSwingMode);
                    if (isInSwingMode)
                    {
                        Light_Green.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", new Color(0, 0, 10, 1));
                    }
                    else
                    {
                        Light_Green.GetComponent<MeshRenderer>().material.SetColor("_EmissionColor", new Color(2, 2, 2, 1));
                    }
                }
            }


            

        }

    }
}
