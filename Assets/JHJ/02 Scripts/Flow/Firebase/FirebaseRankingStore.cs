using Cysharp.Threading.Tasks;
using Firebase.Database;
using System.Threading;

public class FirebaseRankingStore
{
    private const string PATH = "rankings";

    private DatabaseReference root => FirebaseDatabase.DefaultInstance.GetReference(PATH);


    public string CreateKey()
    {
        return root.Push().Key;
    }

    public async UniTask SaveAsync(string key, RankingEntry entry, CancellationToken ct)
    {
        await root.Child(key).SetValueAsync(entry.ToDictionary()).AsUniTask().AttachExternalCancellation(ct);
        
    }

    public async UniTask<bool> ExistsAsync(string key, CancellationToken ct)
    {
        DataSnapshot snapshot = await root.Child(key).GetValueAsync().AsUniTask().AttachExternalCancellation(ct);
        
        return snapshot.Exists;
    }
}
