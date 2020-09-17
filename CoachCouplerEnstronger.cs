using System;
using UnityEngine;

namespace PassengerJobsMod
{
    public class CoachCouplerEnstronger : MonoBehaviour
    {
		private Coroutine EnstrongCoro;

		private Coupler FrontCoupler;
		private Coupler RearCoupler;

		private void Start()
		{
			FrontCoupler = transform.Find("[coupler front]").GetComponent<Coupler>();
			RearCoupler = transform.Find("[coupler rear]").GetComponent<Coupler>();

			if( !FrontCoupler )
			{
				Debug.LogError("CoachCouplerEnstronger couldn't find front Coupler component during init", this);
				return;
			}

			if( !RearCoupler )
			{
				Debug.LogError("CoachCouplerEnstronger couldn't find rear Coupler component during init", this);
				return;
			}
			
			SetupListeners(true);
		}

		private void SetupListeners( bool on )
		{
			if( on )
			{
				FrontCoupler.Coupled += OnCoupled;
				FrontCoupler.Uncoupled += OnUncouple;

				RearCoupler.Coupled += OnCoupled;
				RearCoupler.Uncoupled += OnUncouple;
			}
			else
			{
				FrontCoupler.Coupled -= OnCoupled;
				FrontCoupler.Uncoupled += OnUncouple;

				RearCoupler.Coupled -= OnCoupled;
				RearCoupler.Uncoupled += OnUncouple;
			}
		}

		private void OnCoupled( object _, CoupleEventArgs e )
		{
			Coupler coupler = (e.thisCoupler.springyCJ != null) ? e.thisCoupler : e.otherCoupler;

			if( coupler == null )
			{
				Debug.LogError("CoachCouplerEnstronger couldn't find Coupler component during coupling", this);
				return;
			}

			if( coupler.springyCJ == null )
			{
				Debug.LogError("CoachCouplerEnstronger couldn't find the ConfigurableJoint during coupling", this);
				return;
			}

			if( EnstrongCoro != null )
			{
				StopCoroutine(EnstrongCoro);
			}
			
			EnstrongCoro = StartCoroutine(EnstrongJointsAsync(coupler));
		}

		private void OnUncouple( object _, UncoupleEventArgs e )
		{
			if( EnstrongCoro != null )
			{
				StopCoroutine(EnstrongCoro);
			}
			EnstrongCoro = null;
		}

		private System.Collections.IEnumerator EnstrongJointsAsync( Coupler coupler )
		{
			// Springy Joint
			coupler.springyCJ.breakForce = float.PositiveInfinity;

			while( coupler.IsJointAdaptationActive )
			{
				yield return WaitFor.Seconds(0.5f);
			}

            EnstrongCoro = null;
			yield break;
		}
	}
}
