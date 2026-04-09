using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewNPCDialogue", menuName = "NPC Dialogue")]
public class NPCDialogue : ScriptableObject
{
    public string[] dialoguelines;
    public float typingSpeed = 0.05f;
    public string npcName;
    public string playerName = "James";
    public DialogueChoice[] choices;
    public bool[] enddialoguelines;
    public bool[] isNpclines;
}

[System.Serializable]
public class DialogueChoice
{
    public int dialogueIndex;
    public string[] choices;
    public int[] nextDialogueIndexes;
    public int[] skipTriggerIndexes;
    public int[] skipto;
    public int[] karmapositive;
    public int[] karmanegative;
    public int[] karmaneutral;
}