using System;
using UnityEngine;

public class FuelBalancerLine : PartModule
{
	[KSPField(guiActive = true, guiName = "Status", isPersistant = false)]
	private string status = "Idle";
	[KSPField(guiActive = true, guiName = "Resource Flow", guiFormat = "F5", isPersistant = false)]
	private float resourceFlow = 0;
	[KSPField(guiActive = true, guiName = "Resource Draw",  guiFormat = "F5", isPersistant = false)]
	private float powerDraw = 0;
	[KSPField]
	public float FlowRate;
	[KSPField]
	public float ResourceUsage;
	[KSPField]
	public String ResourceName = "ElectricCharge";
	[KSPField]
	public int Balance = 1;

	[KSPAction("Activate Fuel Balancer")]
	public void ActivateAction (KSPActionParam param)
	{
		SetState (true);
	}

	[KSPAction("Shutdown Fuel Balancer")]
	public void ShutdownAction (KSPActionParam param)
	{
		SetState (false);
	}

	[KSPAction("Toggle Fuel Balancer")]
	public void ToggleAction (KSPActionParam param)
	{
		ToggleEvent ();
	}

	[KSPEvent(guiActive=true, guiName="Toggle Fuel Balancer")]
	public void ToggleEvent ()
	{
		SetState (part.State != PartStates.ACTIVE);
	}

	public void SetState (bool state, String guiString = null)
	{
		if (state) {
			if (guiString == null) {
				guiString = "Idle";
			}
			if (part is FuelLine) {
				if (Balance > 0) {
					((FuelLine)part).flowDirection = FuelLine.FuelFlowDirection.AUTO;
				}
			} else {
				SetState (false, "Part not fuel line");
				// Only happens if someone messes up the config.
				return;
			}
			if (PartResourceLibrary.Instance.GetDefinition (ResourceName) == null) {
				SetState (false, "Resource not found: " + ResourceName);
				return;
			}
			part.force_activate ();
		} else {
			if (guiString == null) {
				guiString = "Inactive";
			}
			powerDraw = 0;
			resourceFlow = 0;
			part.deactivate ();
		}
		status = guiString;
	}

	public override string GetInfo ()
	{
		return ("Flow Rate: " + FlowRate +
			"\nResource Usage: " + ResourceUsage * FlowRate +
		        "\nResource Used: " + ResourceName);
	}

	public override void OnStart (PartModule.StartState state)
	{
		SetState (true); // Start active
	}

	public override void OnFixedUpdate ()
	{
		if (!HighLogic.LoadedSceneIsFlight)
			return;
		if (part == null) {
			SetState (false, "No parent part!");
			// Only time this can happen is if parent part is destroyed?
			return;
		}
		Part balA = part.parent;
		Part balB = ((FuelLine)part).target;

		if (balB == null) {
			SetState (false, "No target part!");
			// Happens if target is destroyed or broken away from the vessel.
			return;
		}
		bool resTransfered = false;
		bool lowPower = false;
		bool foundRes = false;
		double maxFlowThisTick = FlowRate * TimeWarp.fixedDeltaTime;
		double minVisibleFlow = 0.000005 / TimeWarp.fixedDeltaTime;
		powerDraw = 0;
		resourceFlow = 0;
		foreach (PartResource aPartRes in balA.Resources) {
			if (aPartRes.info.resourceFlowMode == ResourceFlowMode.NO_FLOW || aPartRes.info.density == 0) {
				continue;
			}
			PartResource bPartRes = null;
			foreach (PartResource bTemp in balB.Resources) {
				if (aPartRes.resourceName == bTemp.resourceName) {
					bPartRes = bTemp;
				}
			}
			if (bPartRes == null) {
				continue;
			}
			foundRes = true;

			double toTrans;
			bool swap = false;
			if (Balance > 0) {
				PartResource less = aPartRes;
				PartResource more = bPartRes;
				if ((less.amount / less.maxAmount) > (more.amount / more.maxAmount)) {
					PartResource temp = less;
					less = more;
					more = temp;
					swap = true;
				}
				double total = less.amount + more.amount;
				double cap = less.maxAmount + more.maxAmount;
				double targetRatio = total / cap;
				toTrans = Math.Min ((less.maxAmount * targetRatio) - less.amount, 
				                    more.amount - (more.maxAmount * targetRatio));

			} else {
				toTrans = -Math.Min (aPartRes.amount, bPartRes.maxAmount - bPartRes.amount);
			}
			if (toTrans > maxFlowThisTick) {
				toTrans = maxFlowThisTick;
			}

			double resourceRequested = toTrans * ResourceUsage;
			double resourceUsed = part.RequestResource (ResourceName, resourceRequested);
			powerDraw += (float) resourceUsed;
			if (resourceUsed < resourceRequested) {
				toTrans = resourceUsed / ResourceUsage; 
			}
			if (resourceRequested - resourceUsed > minVisibleFlow) // Ain't floating-point errors fun?
				lowPower = true;
			else if (toTrans >= minVisibleFlow)
				resTransfered = true;
			resourceFlow += (float) toTrans;
			if (swap)
				toTrans = -toTrans;
			aPartRes.amount += toTrans;	
			bPartRes.amount -= toTrans;
		}
		resourceFlow /= TimeWarp.fixedDeltaTime;
		powerDraw /= TimeWarp.fixedDeltaTime;
		if (!foundRes) {
			SetState (false, "No transferable resources found!");
			return;
		}
		if (lowPower) {
			status = "Low " + ResourceName + "!";
		}
		else if (resTransfered)
			status = "Active";
		else
			status = "Idle";
	}
}

