using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class DialogueController : MonoBehaviour
{
    public static DialogueController Instance { get; private set; }

    public GameObject npcdialoguePanel;
    public GameObject playerdialoguePanel;
    public TMP_Text NPCdialogueText, PlayerdialogueText, NPCnameText, PlayernameText;
    public Transform choiceContainer, choiceContainer2;
    public GameObject choicebuttonprefab;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void ShowNPCDialogueUI(bool show)
    {
        npcdialoguePanel.SetActive(show);
    }

    public void ShowPlayerDialogueUI(bool show)
    {
        playerdialoguePanel.SetActive(show); 
    }

    public void ShowContainer(bool show)
    {
        choiceContainer.gameObject.SetActive(show);
    }

    public void ShowContainer2(bool show)
    {
        choiceContainer2.gameObject.SetActive(show);
    }

    public void SetNPCInfo(string name)
    {
        NPCnameText.text = name;
    }

    public void SetPlayerInfo(string name)
    {
        PlayernameText.text = name;
    }

    public void SetNPCDialogueText(string text)
    {
        NPCdialogueText.text = text;
    }

    public void SetPlayerDialogueText(string text)
    {
        PlayerdialogueText.text = text;
    }

    public void ClearChoices()
    {
        foreach (Transform child in choiceContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void ClearChoices2()
    {
        foreach (Transform child in choiceContainer2)
        {
            Destroy(child.gameObject);
        }
    }

    public void CreateChoiceButton(string choicetext, UnityEngine.Events.UnityAction onClick)
    {
        GameObject choicebutton = Instantiate(choicebuttonprefab, choiceContainer);
        choicebutton.GetComponentInChildren<TMP_Text>().text = choicetext;
        choicebutton.GetComponent<Button>().onClick.AddListener(onClick);
    }

    public void CreateChoiceButton2(string choicetext, UnityEngine.Events.UnityAction onClick)
    {
        GameObject choicebutton = Instantiate(choicebuttonprefab, choiceContainer2);
        choicebutton.GetComponentInChildren<TMP_Text>().text = choicetext;
        choicebutton.GetComponent<Button>().onClick.AddListener(onClick);
    }
}

