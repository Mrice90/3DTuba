# TokenFactory change for rigged tokens (AI-060-RIG-COMBAT)

In `TokenFactory.TryModel`, right after `model.name = "Model";`:

```csharp
// AI-060-RIG-COMBAT: rigged tokens ship an Animator; give it the generated controller so
// PresentationMotion.PlayRig can fire Move/Attack/Summon/Destroy/Ability/Hit triggers.
var rigAnimator = model.GetComponentInChildren<Animator>();
if (rigAnimator != null && rigAnimator.runtimeAnimatorController == null)
    rigAnimator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>(card.model + "_Rig");
if (rigAnimator != null && rigAnimator.GetComponent<RigEvents>() == null) rigAnimator.gameObject.AddComponent<RigEvents>();
```

Animation events call `OnRigEvent(string)` (impact, down, land) on `RigEvents` (Scripts/RigEvents.cs), which sound and VFX timing can subscribe to later.
