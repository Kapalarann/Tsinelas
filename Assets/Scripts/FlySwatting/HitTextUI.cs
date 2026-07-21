using TMPro;
using UnityEngine;

public class HitTextUI : MonoBehaviour
{
    public static HitTextUI Instance;

    [SerializeField] private TMP_Text hitText;

    private readonly string[] words =
    {
        "SPLAT!",
        "SMACK!",
        "WHACK!",
        "BONK!",
        "THWACK!",
        "POW!",
        "SQUISH!",
        "SMUSH!"
    };

    private Coroutine currentRoutine;

    private void Awake()
    {
        Instance = this;
        hitText.gameObject.SetActive(false);
    }

    public void ShowHitText()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(ShowRoutine());
    }

    private System.Collections.IEnumerator ShowRoutine()
    {
        hitText.text = words[Random.Range(0, words.Length)];
        hitText.gameObject.SetActive(true);

        hitText.transform.localScale = Vector3.zero;

        float t = 0;

        while (t < 0.15f)
        {
            t += Time.deltaTime;
            hitText.transform.localScale = Vector3.Lerp(Vector3.zero, Vector3.one, t / 0.15f);
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        hitText.gameObject.SetActive(false);
    }
}