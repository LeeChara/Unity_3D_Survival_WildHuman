using TMPro;
using UnityEngine;

// 사망 중 부활까지 남은 시간을 화면에 표시
public class RespawnUI : MonoBehaviour
{
    // 이 스크립트는 항상 활성 상태인 부모에 두고, 패널만 켜고 끔
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private PlayerHealth playerHealth;

    // 마지막으로 글자에 반영한 초 (매 프레임 문자열을 새로 만들지 않도록)
    private int lastSeconds = -1;

    private void Awake()
    {
        panel.SetActive(false);
    }

    private void OnEnable()
    {
        playerHealth.Died += OnDied;
        playerHealth.Revived += OnRevived;
    }

    private void OnDisable()
    {
        playerHealth.Died -= OnDied;
        playerHealth.Revived -= OnRevived;
    }

    private void Update()
    {
        if (!panel.activeSelf) return;

        int seconds = Mathf.CeilToInt(playerHealth.RespawnRemaining);
        if (seconds == lastSeconds) return;

        lastSeconds = seconds;
        countdownText.text = $"{seconds}초 후 부활합니다";
    }

    private void OnDied()
    {
        lastSeconds = -1;
        panel.SetActive(true);
    }

    private void OnRevived()
    {
        panel.SetActive(false);
    }
}
