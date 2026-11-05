using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using System;

public class FirebaseAuthService
{

    private bool isInitialized;
    private UniTask? pendingSignIn;     // 진행 중인 로그인 (중복 호출 시 같은 작업을 공유)
    public string UserId { get; private set; }


    public UniTask SignInAsync()
    {
        // 타임아웃 후 재시도해도 이전 로그인이 아직 진행 중이면 새로 시작하지 않고 그 작업을 기다림
        // Preserve : UniTask는 원래 한 번만 await 가능 → 여러 번 await 가능하게 만듦
        if (pendingSignIn == null || pendingSignIn.Value.Status.IsCompleted())
            pendingSignIn = SignInInternalAsync().Preserve();

        return pendingSignIn.Value;
    }

    private async UniTask SignInInternalAsync()
    {
        // Firebase 초기화 (최초 1회)

        if(!isInitialized)
        {
            DependencyStatus status = await FirebaseApp.CheckAndFixDependenciesAsync();

            if(status != DependencyStatus.Available)
                throw new Exception($"Firebase 사용 불가: {status}");

            isInitialized = true;
        }

        FirebaseAuth auth = FirebaseAuth.DefaultInstance;

        // 이미 로그인했었다면 재사용
        if(auth.CurrentUser != null)
        {
            UserId = auth.CurrentUser.UserId;
            return;
        }

        // 새 사용자라면 새로 익명 로그인
        AuthResult result = await auth.SignInAnonymouslyAsync();
        UserId = result.User.UserId;
    }
}
