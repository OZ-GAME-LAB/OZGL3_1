using UnityEngine;

/// <summary>Forwards actual imported-clip progress to the optional action API.</summary>
public sealed class ProtagonistActionStateCallbacks : StateMachineBehaviour
{
    public ProtagonistExtraAction action;

    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponent<ProtagonistActionAnimator>()?.NotifyEntered(action);

    public override void OnStateUpdate(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponent<ProtagonistActionAnimator>()?.NotifyProgress(action, stateInfo.normalizedTime);

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        => animator.GetComponent<ProtagonistActionAnimator>()?.NotifyExited(action);
}
