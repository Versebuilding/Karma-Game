using UnityEngine;
using TMPro;
using System.Collections;

public class NPC : MonoBehaviour, IInteractable
{
    public NPCDialogue dialogueData;
    private DialogueController dialogueUI;
    private int dialogueIndex;
    private bool isTyping;
    private bool isDialogueActive;
    private bool playerInRange;

    private void Start()
    {
        dialogueUI = DialogueController.Instance;
    }

    void Update()
    {
        if (playerInRange)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                Interact();
            }
        }
    }

    public bool CanInteract()
    { 
        return !isDialogueActive;
    }

    public void Interact()
    {
        if (dialogueData == null)
        {
            return;
        }

        if (isDialogueActive)
        {
            NextLine();
        }
        else
        {
            StartDialogue();
        }
    }

    void ChangePanel()
    {
        if (dialogueData.isNpclines[dialogueIndex])
        {
            dialogueUI.SetNPCInfo(dialogueData.npcName);
            dialogueUI.ShowNPCDialogueUI(true);
            dialogueUI.ShowPlayerDialogueUI(false);
        }
        else
        {
            dialogueUI.SetPlayerInfo(dialogueData.playerName);
            dialogueUI.ShowPlayerDialogueUI(true);
            dialogueUI.ShowNPCDialogueUI(false);
        }
    }

    void ChangeDialogue()
    {
        if (dialogueData.isNpclines[dialogueIndex])
        {
            dialogueUI.SetNPCDialogueText(dialogueData.dialoguelines[dialogueIndex]);
        }
        else
        {
            dialogueUI.SetPlayerDialogueText(dialogueData.dialoguelines[dialogueIndex]);
        }
    }

    void StartDialogue()
    {
        isDialogueActive = true;
        ChangePanel();
        DisplayCurrentLine();
    }

    void NextLine()
    {
        if (isTyping)
        {
            StopAllCoroutines();
            ChangePanel();
            ChangeDialogue();
            isTyping = false;
            return;
        }

        foreach (DialogueChoice dialogueChoice in dialogueData.choices)
        {
            if (dialogueChoice.skipTriggerIndexes.Length > 0 && dialogueIndex == dialogueChoice.skipTriggerIndexes[0])
            {
                dialogueIndex = dialogueChoice.skipto[0];
                DisplayCurrentLine();
                return;
            }
        }

        dialogueUI.ClearChoices();
        dialogueUI.ClearChoices2();

        if (dialogueData.enddialoguelines.Length > dialogueIndex && dialogueData.enddialoguelines[dialogueIndex])
        {
            EndDialogue();
            return;
        }

        foreach (DialogueChoice dialogueChoice in dialogueData.choices)
        {
            if (dialogueChoice.dialogueIndex == dialogueIndex)
            {
                DisplayChoices(dialogueChoice);
                return;
            }
        }

        if (++dialogueIndex < dialogueData.dialoguelines.Length)
        {
            DisplayCurrentLine();
        }

        else
        {
            EndDialogue();
        }

    }

    IEnumerator TypeLine()
    {
        isTyping = true;

        if (dialogueData.isNpclines[dialogueIndex])
        {
            dialogueUI.SetNPCDialogueText("");
        }
        else
        {
            dialogueUI.SetPlayerDialogueText("");
        }

        foreach (char letter in dialogueData.dialoguelines[dialogueIndex])
        {
            if (dialogueData.isNpclines[dialogueIndex])
            {
                dialogueUI.SetNPCDialogueText(dialogueUI.NPCdialogueText.text + letter);
            }
            else
            {
                dialogueUI.SetPlayerDialogueText(dialogueUI.PlayerdialogueText.text + letter);
            }

            yield return new WaitForSeconds(dialogueData.typingSpeed);
        }

        isTyping = false;
    }

    void DisplayChoices(DialogueChoice choice)
    {
        for (int i = 0; i < choice.choices.Length; i++)
        {
            int nextIndex = choice.nextDialogueIndexes[i];

            dialogueUI.CreateChoiceButton(choice.choices[i], () =>
            {
                ChoiceOption(nextIndex);

            });

            dialogueUI.CreateChoiceButton2(choice.choices[i], () =>
            {
                ChoiceOption(nextIndex);

            });
        }

        if (dialogueData.isNpclines[dialogueIndex])
        {
            dialogueUI.ShowNPCDialogueUI(false);
            dialogueUI.ShowContainer(true);
            dialogueUI.ShowContainer2(false);
        }
        else
        {
            dialogueUI.ShowPlayerDialogueUI(false);
            dialogueUI.ShowContainer2(true);
            dialogueUI.ShowContainer(false);
        }

    }

    void ChoiceOption(int nextIndex)
    {
        dialogueIndex = nextIndex;
        dialogueUI.ClearChoices();
        dialogueUI.ClearChoices2();
        dialogueUI.ShowContainer(false);
        dialogueUI.ShowContainer2(false);
        DisplayCurrentLine();
    }

    void DisplayCurrentLine()
    {
        StopAllCoroutines();
        ChangePanel();
        StartCoroutine(TypeLine());
    }

    void EndDialogue()
    {
        StopAllCoroutines();
        isDialogueActive = false;
        dialogueUI.SetNPCDialogueText("");
        dialogueUI.ShowNPCDialogueUI(false);
        dialogueUI.SetPlayerDialogueText("");
        dialogueUI.ShowPlayerDialogueUI(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = true;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInRange = false;
        }
    }
}

