using UnityEngine;
using UnityEngine.UI;

public class SimpleImageAnimator : MonoBehaviour
{
    [Header("Assign your 7 sprites in order")]
    public Sprite[] frames = new Sprite[7];

    [Header("Frames per second")]
    public float frameRate = 10f;

    private Image image;
    private int currentFrame;
    private float timer;

    void Awake()
    {
        image = GetComponent<Image>();
    }

    void Start()
    {
        if (frames.Length > 0)
            image.sprite = frames[0];
    }

    void Update()
    {
        if (frames.Length == 0) return;

        timer += Time.deltaTime;

        if (timer >= 1f / frameRate)
        {
            timer -= 1f / frameRate;
            currentFrame = (currentFrame + 1) % frames.Length;
            image.sprite = frames[currentFrame];
        }
    }
}