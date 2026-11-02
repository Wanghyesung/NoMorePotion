using UnityEngine;
using R3;

public class NetworkFlowManager : MonoBehaviour
{
    public static NetworkFlowManager Instance { get; private set; }

    private ReactiveProperty<FlowState> state;
    private Subject<string> error;

    public ReadOnlyReactiveProperty<FlowState> State => state;
    public Observable<string> Error => error;
    public FlowState CurrentState => state.Value;

    //===============================================================

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        state = new ReactiveProperty<FlowState>(FlowState.Title);
        error = new Subject<string>();
        state.Subscribe(s => Debug.Log($"[NetworkFlowManager] 상태 변경: {s}")).AddTo(this);

    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        state?.Dispose();
        error?.Dispose();
    }

    //===============================================================

    #region State

    private void SetState(FlowState newState)
    {
        state.Value = newState;
    }

    private void RaiseError(string message)
    {
        error.OnNext(message);
    }

    #endregion

    //===============================================================

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    #region Debug
    public void Debug_SetState(FlowState newState) => SetState(newState);
    public void Debug_RaiseError(string message) => RaiseError(message);
    #endregion
#endif
}
