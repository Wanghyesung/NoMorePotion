using TMPro;
using UnityEngine;

// 채팅 한 줄. 왼쪽 칸에 닉네임, 오른쪽 칸에 내용 (내용이 길면 내용 칸 안에서만 줄바꿈)
// 안내 메시지는 닉네임 칸을 숨기고 흐린 색으로 한 줄 전체에 표시
public class ChatLineUI : MonoBehaviour
{
    private static readonly Color MINE_COLOR = new Color32(0xD2, 0xAE, 0x66, 0xFF);    // 내 닉네임 (강조색)
    private static readonly Color OTHER_COLOR = new Color32(0xF2, 0xE8, 0xD5, 0xFF);   // 다른 사람 닉네임 (크림색)
    private static readonly Color TEXT_COLOR = new Color32(0xF2, 0xE8, 0xD5, 0xFF);    // 내용
    private static readonly Color SYSTEM_COLOR = new Color32(0x8A, 0x7F, 0x70, 0xFF);  // 안내 메시지 (흐린 색)

    [SerializeField] private TMP_Text nicknameText;
    [SerializeField] private TMP_Text messageText;

    public void Bind(ChatMessage msg)
    {
        if (msg.Kind == ChatKind.System)
        {
            nicknameText.gameObject.SetActive(false);
            messageText.color = SYSTEM_COLOR;
            messageText.text = NoParse(msg.Text);
            return;
        }

        nicknameText.gameObject.SetActive(true);
        nicknameText.color = msg.IsMine ? MINE_COLOR : OTHER_COLOR;
        nicknameText.text = NoParse(msg.Nickname);

        messageText.color = msg.IsMine ? MINE_COLOR : TEXT_COLOR;   // 내 메시지는 닉네임과 같은 색
        messageText.text = NoParse(msg.Text);
    }

    // 사용자가 입력한 <b>, <size> 같은 태그가 그대로 적용되지 않도록 막음
    private static string NoParse(string s)
    {
        if (string.IsNullOrEmpty(s))
            return string.Empty;

        return $"<noparse>{s.Replace("</noparse>", "")}</noparse>";
    }
}
