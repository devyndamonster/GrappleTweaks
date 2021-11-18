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

                    if(m_hand != null)
                    {
                        GM.CurrentPlayerBody.transform.position = GM.CurrentPlayerBody.transform.position + moveDelta;

                        if(Vector3.Angle(GM.CurrentMovementManager.m_armSwingerVelocity, moveDelta) > 90)
                        {
                            GM.CurrentMovementManager.m_armSwingerVelocity = Vector3.ProjectOnPlane(GM.CurrentMovementManager.m_armSwingerVelocity, moveDelta);
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


        /*
        public override Vector3 GetGrabMoveDelta()
        {
            if (DoesFunctionAsGrabPoint())
            {
                float currDistance = Vector3.Distance(transform.position, m_firstShotBolt.transform.position);
                if (currDistance > savedDistance)
                {
                    //Debug.Log("Distance great! Current velocity: " + GM.CurrentMovementManager.m_armSwingerVelocity);
                    GM.CurrentMovementManager.m_armSwingerVelocity = Vector3.zero;
                    return (m_firstShotBolt.transform.position - transform.position).normalized * (currDistance - savedDistance);
                }
            }

            return Vector3.zero;
        }
        */

        private void UpdateMode(FVRViveHand hand)
        {
            if (m_firstShotBolt != null) return;

            if (hand.IsInStreamlinedMode)
            {
                if (hand.Input.AXButtonDown)
                {
                    isInSwingMode = !isInSwingMode;
                    Debug.Log("Toggling swing mode to: " + isInSwingMode);
                }
            }
            else
            {
                if (hand.Input.TouchpadDown && hand.Input.TouchpadAxes.magnitude > 0.2f)
                {
                    isInSwingMode = !isInSwingMode;
                    Debug.Log("Toggling swing mode to: " + isInSwingMode);
                }
            }
        }

    }
}
