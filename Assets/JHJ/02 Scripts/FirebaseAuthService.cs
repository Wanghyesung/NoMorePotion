using Cysharp.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using System;

public class FirebaseAuthService
{

    private bool isInitialized;
    public string UserId { get; private set; }


    public async UniTask SignInAsync()
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
