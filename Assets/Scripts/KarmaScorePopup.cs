using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System;

public class KarmaScorePopup : MonoBehaviour
{

    public GameObject karmascore;
    public Image karmabar2;

    public GameObject petal1;
    public GameObject petal2;
    public GameObject petal3;
    public GameObject petal4;
    public GameObject petal5;
    public GameObject petal6;
    public GameObject petal7;

    public SpriteRenderer karmabar;

    private float moveDistance = 0.01f;
    public Transform cameraTransform;
    private float distanceInFront = 5.0f;
    private int count = 1;

    private bool stop = false;
    private int currentPetal = 0;

    public TMP_Text scoretext;
    private int score=0;

    void Start()
    {
        karmascore.SetActive(false);
        karmabar2.fillAmount = 0f;

        score = 0;

        //Automatically uses main camera if camera not assigned to camera transform

        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }
    }


    void Update()
    {
        scoretext.text = score.ToString();

        //Progress bar from HUD increaes

        if (!stop)
        {
            if (Input.GetKey(KeyCode.Z))
            {
                karmabar2.fillAmount = Mathf.Min(karmabar2.fillAmount + moveDistance / 5.1f, 1f);
            }
            else if (Input.GetKey(KeyCode.C))
            {
                karmabar2.fillAmount = Mathf.Max(karmabar2.fillAmount - moveDistance / 5.1f, 0f);
            }

            //Score increases until 100 and then loops (same with karmabar)
            score = Mathf.RoundToInt(karmabar2.fillAmount * 100f);

            if (score == 100)
            {
                score = 0;
                karmabar2.fillAmount = 0f;
                currentPetal = 0;
            }

        }

        UpdateKarmaBarColor();
        UpdatePetalColor();

    }

    //Timer on karma score popup 
    IEnumerator PopUpTimer()
    {
        stop = true;

        karmascore.SetActive(true);

        yield return new WaitForSeconds(3f);

        karmascore.SetActive(false);
        stop = false;
    }

    //When progress bar image is clicked, the karma popup score will show up and disappear when clicked again
    public void ImageClicked()
    {
        if (count == 1)
        {
            karmascore.SetActive(true);
            count = 2;
        }
        else if (count == 2)
        {
            karmascore.SetActive(false);
            count = 1;
        }
        
    }

    //Petals fade an change into their colors based on position of progress bar

    public void UpdatePetalColor()
    {

        if (karmabar.size.x > 0f && karmabar.size.x <= 0.17f)
        {
            float fade = Mathf.InverseLerp(0f, 0.17f, karmabar.size.x);

            petal1.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);

            petal2.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);

            petal7.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 0.17f)
        {
            petal1.GetComponent<SpriteRenderer>().color = Color.white;

            petal2.GetComponent<SpriteRenderer>().color = Color.white;

            petal7.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal1.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            petal2.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            petal7.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 0.17f && karmabar.size.x <= 1.28f)
        {
            float fade = Mathf.InverseLerp(0.17f, 1.28f, karmabar.size.x);

            petal6.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 1.28f)
        {
            petal6.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal6.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 1.28f && karmabar.size.x <= 2.41f)
        {
            float fade = Mathf.InverseLerp(1.28f, 2.41f, karmabar.size.x);

            petal3.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 2.41f)
        {
            petal3.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal3.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 2.41f && karmabar.size.x <= 3.600016f)
        {
            float fade = Mathf.InverseLerp(2.41f, 3.600016f, karmabar.size.x);

            petal4.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 3.600016f)
        {
            petal4.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal4.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

        if (karmabar.size.x > 3.600016f && karmabar.size.x <= 4.900035f)
        {
            float fade = Mathf.InverseLerp(3.600016f, 4.900035f, karmabar.size.x);

            petal5.GetComponent<SpriteRenderer>().color = Color.Lerp(new Color(0.5f, 0.5f, 0.5f, 0.5f), Color.white, fade);
        }
        else if (karmabar.size.x >= 4.900035f)
        {
            petal5.GetComponent<SpriteRenderer>().color = Color.white;
        }
        else
        {
            petal5.GetComponent<SpriteRenderer>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }

    }

    //Updating karmabar color
    
    public void UpdateKarmaBarColor()
    {
        karmabar.size = new Vector2(karmabar2.fillAmount * 5.1f, karmabar.size.y);

        if (karmabar.size.x >= 0.17f && currentPetal < 1 && !stop)
        {
            currentPetal = 1;
            StartCoroutine(PopUpTimer());
        }
        else if (karmabar.size.x >= 1.28f && currentPetal < 2 && !stop)
        {
            currentPetal = 2;
            StartCoroutine(PopUpTimer());
        }
        else if (karmabar.size.x >= 2.41f && currentPetal < 3 && !stop)
        {
            currentPetal = 3;
            StartCoroutine(PopUpTimer());
        }
        else if (karmabar.size.x >= 3.600016f && currentPetal < 4 && !stop)
        {
            currentPetal = 4;
            StartCoroutine(PopUpTimer());
        }
        else if (karmabar.size.x >= 4.900035f && currentPetal < 5 && !stop)
        {
            currentPetal = 5;
            StartCoroutine(PopUpTimer());
        }
    }

    //KarmaScore follows camera

    void LateUpdate()
    {
        Vector3 centerPosition = cameraTransform.position + (cameraTransform.forward * distanceInFront);

        transform.position = centerPosition;

        transform.rotation = cameraTransform.rotation;
    }
}