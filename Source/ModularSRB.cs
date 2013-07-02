using System;
using UnityEngine;

public class ModularSRB : PartModule
{

	public override void OnFixedUpdate ()
	{
		if (!HighLogic.LoadedSceneIsFlight)
			return;
		var e = FindEngineModule ();
		if (e == null) {
			Debug.Log ("MSRB: No engine module!");
			return;
		}
		float eVel = e.realIsp * e.G;
		double rProp = (double)((e.requestedThrust / eVel) * TimeWarp.deltaTime);
		if (double.IsNaN (rProp)) // This updates before ModuleEngines, so the first frame
										// it isn't initializied.
			return;
		MovePropellant (rProp, e);
	}

	public void MovePropellant (double mass, ModuleEngines m)
	{
		double amountRequested = mass / m.mixtureDensity;
		foreach (ModuleEngines.Propellant prop in m.propellants) {
			TransferResource (prop.id, amountRequested * prop.ratio);
		}
	}

	ModuleEngines FindEngineModule ()
	{
		foreach (PartModule m in part.Modules) {
			if (m is ModuleEngines)
				return (ModuleEngines)m;
		}
		return null;
	}

	private void TransferResource (int id, double amount)
	{
		double avaliable = 0;
		double total = 0;
		for (Part p = part; IsModularSrb(p, id); p = GetPartAbove(p)) {
			PartResource r = p.Resources.Get (id);
			avaliable += r.amount;
			total += r.maxAmount;
		}
		double toAdd = Math.Min (avaliable, amount);
		double ratio = (avaliable - toAdd) / total;
		if (!part.Resources.Contains (id))
			MakeResource (id, toAdd);

		PartResource partResource = part.Resources.Get (id);
		partResource.amount = toAdd + ratio * partResource.maxAmount;
		partResource.maxAmount = Math.Max (toAdd, partResource.maxAmount);

		for (Part p = GetPartAbove(part); IsModularSrb(p, id); p = GetPartAbove(p)) {
			PartResource r = p.Resources.Get (id);
			r.amount = ratio * r.maxAmount;
		}
	}

	void MakeResource (int id, double toAdd)
	{
		PartResource res = part.gameObject.AddComponent<PartResource> ();
		res.SetInfo (PartResourceLibrary.Instance.GetDefinition (id));
		res.amount = 0;
		res.maxAmount = 0;
		part.Resources.list.Add (res);
	}

	Part GetPartAbove (Part p)
	{
		if (p.topNode == null)
			return null;
		return p.topNode.attachedPart;
	}

	bool IsModularSrb (Part p, int id)
	{
		return (p != null && !p.Modules.Contains ("ModuleEngines") && p.Resources.Contains (id)) ||
			(p == part);
	}
}