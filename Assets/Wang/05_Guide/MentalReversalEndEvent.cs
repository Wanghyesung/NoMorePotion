using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Enzeun.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MentalReversalEndEvent : GuideEndEvent
{
    [SerializeField] private MentalStrengthManager mentalStrengthManager;
    [SerializeField, Range(11, 30)] private int tutorialMentalStrength = 20;
    [SerializeField, Min(1f)] private float experienceSeconds = 5f;

    private int previousMentalStrength;
    private InputAction moveAction;
    private CancellationTokenSource experienceCancellation;

    protected override void OnBegin()
    {
        previousMentalStrength = mentalStrengthManager.mentalStrength;
        SetMentalStrength(tutorialMentalStrength);
        moveAction = InputManager.Instance.MoveAction;
        moveAction.performed += OnMove;
        if (moveAction.ReadValue<Vector2>().sqrMagnitude > 0.01f)
            StartExperience();
    }

    private void OnMove(InputAction.CallbackContext context)
    {
        if (context.ReadValue<Vector2>().sqrMagnitude > 0.01f)
            StartExperience();
    }

    private void StartExperience()
    {
        if (experienceCancellation != null)
            return;

        experienceCancellation = new CancellationTokenSource();
        FinishExperience(experienceCancellation.Token).Forget();
    }

    private async UniTask FinishExperience(CancellationToken token)
    {
        bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(experienceSeconds),
            cancellationToken: token).SuppressCancellationThrow();
        if (!canceled)
            Complete();
    }

    protected override void OnStop()
    {
        experienceCancellation?.Cancel();
        experienceCancellation?.Dispose();
        experienceCancellation = null;
        if (moveAction != null)
            moveAction.performed -= OnMove;
        moveAction = null;
        if (mentalStrengthManager != null)
            SetMentalStrength(previousMentalStrength);
    }

    private void SetMentalStrength(int value)
    {
        int amount = value - mentalStrengthManager.mentalStrength;
        if (amount > 0)
            mentalStrengthManager.IncreaseMentalStrength(amount);
        else if (amount < 0)
            mentalStrengthManager.DecreaseMentalStrength(-amount);
    }
}
