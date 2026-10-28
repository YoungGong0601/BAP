using DG.Tweening;
using TMPro;
using UnityEngine;

public class DamageTextDrawer : MonoBehaviour
{
    public static DamageTextDrawer Instance { get; private set; }

    [SerializeField] TMP_Text DamageTextPrefab;

    [SerializeField] Canvas canvas;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
    }

    public void DrawDamageText(Vector3 position, float damage, AttackResult result)
    {
        var spawnPosition = Camera.main.WorldToScreenPoint(position + Vector3.up * 1.5f);
        var damageText = Instantiate(DamageTextPrefab, spawnPosition, Quaternion.identity, canvas.transform);
        damageText.transform.position += new Vector3(Random.Range(-30f, 30f), Random.Range(-10f, 10f) + 40f, 0f);

        if (result.HasFlag(AttackResult.Critical))
        {
            damageText.text = "Critical";
            damageText.color = Color.red;
        }
        else if (result.HasFlag(AttackResult.Weak))
        {
            damageText.text = "Weak";
            damageText.color = Color.yellow;
        }
        else if (result.HasFlag(AttackResult.Vulnerable))
        {
            damageText.text = "Vulnerable";
            damageText.color = Color.cyan;
        }
        else if (result.HasFlag(AttackResult.Resist))
        {
            damageText.text = "Resist";
            damageText.color = Color.gray;
        }
        else
            damageText.color = Color.white;

        if (result.HasFlag(AttackResult.Miss))
            damageText.text = "Miss";
        else if (result.HasFlag(AttackResult.Blocked))
            damageText.text = "Blocked";
        else
            damageText.text += $"\n{damage:0}";

        damageText.transform.DOMoveY(damageText.transform.position.y + 20f, 1f).SetEase(Ease.OutCubic).OnComplete(() =>
        {
            Destroy(damageText.gameObject);
        });
    }
}