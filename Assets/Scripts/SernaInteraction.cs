using UnityEngine;

public class SernaInteraction : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Transform playerCamera;        // Drag Main Camera transform here (or uses Camera.main)
    public Transform uiRoot;              // World-space canvas root (make it a child of Serna)
    public QuickOutline outline;          // QuickOutline component
    public SernaAnimCycler animCycler;    // Handles 3 idles + 3 talk loops

    [Header("Tuning")]
    public float interactionDistance = 3f;
    public bool stopTalkingWhenOutOfRange = true;

    [Header("UI Position")]
    public Vector3 uiOffset = new Vector3(0, 2.0f, 0);

    [Header("Outline Pulse")]
    public bool pulseOutline = true;
    public float pulseSpeed = 2f;
    public float minWidth = 2.5f;
    public float maxWidth = 4.5f;
    public Color colorA = new Color(0.35f, 0.9f, 1f, 1f);   // soft cyan
    public Color colorB = new Color(1f, 0.85f, 0.25f, 1f);  // warm gold

    [Header("Facing")]
    public float faceTurnSpeed = 6f;
    private bool shouldFacePlayer;
    private bool isTalking;

    [Header("Audio")]
    public AudioSource voiceSource;
    public AudioClip talkLoopClip;         // optional ambient talking loop (hums, murmurs)
    public float fadeDuration = 0.25f;     // 0.2–0.5 feels good

    private Coroutine voiceFadeRoutine;

    void Awake()
    {
        // Auto-find if not assigned in Inspector
        if (!outline) outline = GetComponentInChildren<QuickOutline>(true);
        if (!animCycler) animCycler = GetComponentInChildren<SernaAnimCycler>(true);

        if (!playerCamera && Camera.main) playerCamera = Camera.main.transform;
        if (!voiceSource) voiceSource = GetComponent<AudioSource>();
    
    }

    void Start()
    {
        if (uiRoot) uiRoot.gameObject.SetActive(false);
        if (outline) outline.enabled = false;

        // Ensure Serna starts in idle cycle
        if (animCycler) animCycler.SetTalking(false);
        isTalking = false;
    }

    void Update()
    {
        if (!player || !playerCamera || !uiRoot || !outline || !animCycler)
            return;

        float distance = Vector3.Distance(player.position, transform.position);
        bool inRange = distance <= interactionDistance;

        // Show prompt + outline only when close AND not currently talking
        bool showPrompt = inRange && !isTalking;

        uiRoot.gameObject.SetActive(showPrompt);
        outline.enabled = showPrompt;

        if (showPrompt)
        {
            // Keep UI above Serna (if it's a child, this still works fine)
            uiRoot.position = transform.position + uiOffset;

            // Billboard: face camera
            Vector3 lookDir = uiRoot.position - playerCamera.position;
            uiRoot.rotation = Quaternion.LookRotation(lookDir);

            // Pulse outline (width + color)
            if (pulseOutline)
            {
                float t = Mathf.PingPong(Time.time * pulseSpeed, 1f);
                outline.OutlineWidth = Mathf.Lerp(minWidth, maxWidth, t);
                outline.OutlineColor = Color.Lerp(colorA, colorB, t);
            }
        }

        // Press E to toggle talking when close
        if (inRange && Input.GetKeyDown(KeyCode.E))
        {
            ToggleTalking();
        }

        // Optional: walk away stops talking
        if (!inRange && isTalking && stopTalkingWhenOutOfRange)
        {
            StopTalking();
        }
        
        // Face player when talking
        if (shouldFacePlayer)
        {
            FacePlayer();
        }
    }

    void FacePlayer()
    {
        Vector3 direction = player.position - transform.position;
        direction.y = 0f; // keep upright

        if (direction.sqrMagnitude < 0.001f) return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            targetRotation,
            faceTurnSpeed * Time.deltaTime
        );
    }

    void ToggleTalking()
    {
        if (isTalking) StopTalking();
        else StartTalking();
    }

    void StartTalking()
    {
        isTalking = true;
        shouldFacePlayer = true;
        FadeInVoice(talkLoopClip, loop: true);

        animCycler.SetTalking(true);

        if (uiRoot) uiRoot.gameObject.SetActive(false);
        if (outline) outline.enabled = false;

        PlayVoice();
    }
    void StopTalking()
    {
        isTalking = false;
        shouldFacePlayer = false;

        animCycler.SetTalking(false);
        FadeOutVoice();

        StopVoice();
    }

    void PlayVoice()
    {
        if (!voiceSource || !talkLoopClip) return;

        if (!voiceSource.isPlaying)
        {
            voiceSource.clip = talkLoopClip;
            voiceSource.Play();
        }
    }

    void FadeInVoice(AudioClip clip, bool loop)
    {
        if (!voiceSource || !clip) return;

        voiceSource.clip = clip;
        voiceSource.loop = loop;

        if (voiceFadeRoutine != null) StopCoroutine(voiceFadeRoutine);
        voiceFadeRoutine = StartCoroutine(FadeRoutine(targetVolume: 1f, playIfNeeded: true));
    }

    void FadeOutVoice()
    {
        if (!voiceSource) return;

        if (voiceFadeRoutine != null) StopCoroutine(voiceFadeRoutine);
        voiceFadeRoutine = StartCoroutine(FadeRoutine(targetVolume: 0f, playIfNeeded: false));
    }

    System.Collections.IEnumerator FadeRoutine(float targetVolume, bool playIfNeeded)
    {
        float startVol = voiceSource.volume;

        if (playIfNeeded && !voiceSource.isPlaying)
            voiceSource.Play();

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / fadeDuration);
            voiceSource.volume = Mathf.Lerp(startVol, targetVolume, lerp);
            yield return null;
        }

        voiceSource.volume = targetVolume;

        if (Mathf.Approximately(targetVolume, 0f))
            voiceSource.Stop();
    }

    void StopVoice()
    {
        if (voiceSource && voiceSource.isPlaying)
        {
            voiceSource.Stop();
        }
    }
}