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
	public float BalanceRatio = 0.5f;

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
				((FuelLine)part).flowDirection = FuelLine.FuelFlowDirection.AUTO;
			} else {
				SetState (false, "Part not fuel line");
				// Only happens if someone messes up the config.
				return;
			}
			if (BalanceRatio > 1 || BalanceRatio < 0) {
				SetState (false, "Target balance out of range!");
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
		powerDraw = 0;
		resourceFlow = 0;
		double maxFlowThisTick = FlowRate * TimeWarp.fixedDeltaTime;
		double minVisibleFlow = 0.000005 / TimeWarp.fixedDeltaTime;
		foreach (PartResource aPartRes in balA.Resources) {
			if (aPartRes.info.resourceFlowMode != ResourceFlowMode.STACK_PRIORITY_SEARCH)
				continue;
			PartResource bPartRes = balB.Resources.Get(aPartRes.info.id);
			if (bPartRes == null)
				continue;

			foundRes = true;

			double total = aPartRes.amount + bPartRes.amount;
			double cap = aPartRes.maxAmount + bPartRes.maxAmount;

			double targetRatio = total / cap;

			double aTargetRatio = BalanceRatio * targetRatio;
			double bTargetRatio = (1 - BalanceRatio) * targetRatio;


			double aTargetAmount = aPartRes.maxAmount * aTargetRatio;
			double bTargetAmount = bPartRes.maxAmount * bTargetRatio;

			double flowRequest = aTargetAmount - aPartRes.amount;

			if (Math.Abs (bPartRes.amount - bTargetAmount) < Math.Abs (flowRequest))
				flowRequest = bPartRes.amount - bTargetAmount;

			if (Math.Abs (flowRequest) > maxFlowThisTick)
				flowRequest = maxFlowThisTick * Math.Sign (flowRequest);

			double resourceRequested = Math.Abs (flowRequest) * ResourceUsage;
			double resourceUsed = part.RequestResource (ResourceName, resourceRequested);

			powerDraw += (float) resourceUsed;

			double actualFlow = resourceUsed / ResourceUsage * Math.Sign(flowRequest);

			resourceFlow += (float) Math.Abs (actualFlow);
			aPartRes.amount += actualFlow;	
			bPartRes.amount -= actualFlow;

			if (resourceRequested - resourceUsed > minVisibleFlow) // Ain't floating-point errors fun?
				lowPower = true;
			else if (Math.Abs (flowRequest) >= minVisibleFlow)
				resTransfered = true;

		}

		resourceFlow /= TimeWarp.fixedDeltaTime;
		powerDraw /= TimeWarp.fixedDeltaTime;

		if (!foundRes) {
			SetState (false, "No transferable resources found!");
			return;
		}
		if (lowPower)
			status = "Low " + ResourceName + "!";
		else if (resTransfered)
			status = "Active";
		else
			status = "Idle";
	}
}

