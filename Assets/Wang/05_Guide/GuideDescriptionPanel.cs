using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class GuideDescriptionPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private SOEventText soEventText;

    private CancellationTokenSource revealCancellation;
    private InputAction nextTextAction;
    private int textIndex;
    private int characterCount;
    private bool isShowing;

    public event Action Completed;

    private void Awake()
    {
        nextTextAction = new InputAction("NextGuideText", InputActionType.Button, "<Keyboard>/enter");
        nextTextAction.AddBinding("<Keyboard>/numpadEnter");
        nextTextAction.performed += OnNextText;
    }

    public void Show(SOEventText text)
    {
        Stop();
        soEventText = text;
        textIndex = 0;
        isShowing = true;
        gameObject.SetActive(true);
       
        nextTextAction.Enable();
        ShowText();
    }

    private void ShowText()
    {
        if (textIndex >= soEventText.Texts.Count)
        {
            Stop();
            Completed?.Invoke();
            return;
        }

        SOEventText.EventText line = soEventText.Texts[textIndex];
        descriptionText.SetText(line.text);
        descriptionText.maxVisibleCharacters = int.MaxValue;
        descriptionText.ForceMeshUpdate();
        characterCount = descriptionText.textInfo.characterCount;
        if (line.speed <= 0f)
            return;

        descriptionText.maxVisibleCharacters = 0;
        revealCancellation = new CancellationTokenSource();
        Reveal(line.speed, revealCancellation.Token).Forget();
    }

    private async UniTask Reveal(float speed, CancellationToken token)
    {
        while (descriptionText.maxVisibleCharacters < characterCount)
        {
            bool canceled = await UniTask.Delay(TimeSpan.FromSeconds(speed),
                ignoreTimeScale: true, cancellationToken: token).SuppressCancellationThrow();
            if (canceled)
                return;

            descriptionText.maxVisibleCharacters++;
        }
    }

    private void OnNextText(InputAction.CallbackContext context)
    {
        if (!isShowing)
            return;

        CancelReveal();
        if (descriptionText.maxVisibleCharacters < characterCount)
        {
            descriptionText.maxVisibleCharacters = characterCount;
            return;
        }

        textIndex++;
        ShowText();
    }

    public void Stop()
    {
        CancelReveal();
        if (!isShowing)
            return;

        isShowing = false;
        nextTextAction.Disable();
        gameObject.SetActive(false);
    }

    private void CancelReveal()
    {
        revealCancellation?.Cancel();
        revealCancellation?.Dispose();
        revealCancellation = null;
    }

    private void OnDisable() => Stop();

    private void OnDestroy()
    {
        nextTextAction.performed -= OnNextText;
        nextTextAction.Dispose();
    }
}
