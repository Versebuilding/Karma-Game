using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;

public class Hud_Controller : MonoBehaviour
{
    [SerializeField]
    private GameObject Orb_1;
    private List<CanvasGroup> lightSet_1;
    [SerializeField]
    private GameObject Orb_2;
    private List<CanvasGroup> lightSet_2;
    [SerializeField]
    private GameObject Orb_3;
    private List<CanvasGroup> lightSet_3;
    private OrbFillUI active_orb;
    private float fill = 0;
    private float time = 0;
    private float Targettime = 15;

    void Start()
    {
        //note this set up ids dependent on the placement of the lights in the Hierachy
        //set each list
        lightSet_1 = new List<CanvasGroup>();
        lightSet_2 = new List<CanvasGroup>();
        lightSet_3 = new List<CanvasGroup>();
        for (int i = 2; i <= 13; i++)// look at indext 2 to 13 for Orb_1.transform
        {
            //store all light sets for later use // advoid reusung "GetComponent"
            Transform lightSet = Orb_1.transform.transform.GetChild(i);
            lightSet_1.Add(lightSet.GetComponent<CanvasGroup>());
        }
        for (int i = 2; i <= 9; i++)// look at indext 2 to 9 for Orb_2.transform (orb 2 has fewer lights
        {
            //store all light sets for later use // advoid reusung "GetComponent"
            Transform lightSet = Orb_2.transform.transform.GetChild(i);
            lightSet_2.Add(lightSet.GetComponent<CanvasGroup>());
        }
        for (int i = 2; i <= 13; i++)// look at indext 2 to 13 for Orb_3.transform
        {
            //store all light sets for later use // advoid reusung "GetComponent"
            Transform lightSet = Orb_3.transform.transform.GetChild(i);
            lightSet_3.Add(lightSet.GetComponent<CanvasGroup>());
        }
        //get the stars for each (orb 2 has less lights
        lightSet_1.Add(Orb_1.transform.transform.GetChild(14).GetChild(0).GetComponent<CanvasGroup>());
        lightSet_2.Add(Orb_2.transform.transform.GetChild(10).GetChild(0).GetComponent<CanvasGroup>());
        lightSet_3.Add(Orb_3.transform.transform.GetChild(14).GetChild(0).GetComponent<CanvasGroup>());
        //set each alpha to 0 to test if the refaces were set 
        ResetLights();
    }

    // Update is called once per frame
    void Update()
    {
        #region test code
        // pure testing code claud can remove and set the witch orn is filling as needed
        if (Input.GetKeyDown(KeyCode.Alpha1)) { Set_active_orb(1); }
        if (Input.GetKeyDown(KeyCode.Alpha2)) { Set_active_orb(2); }
        if (Input.GetKeyDown(KeyCode.Alpha3)) { Set_active_orb(3); }
        //im not sure how your setting your time but for now im going on the idea that your holding the pitch for n anpint of time
        // so im setting up a target time that counts to 0 filling in more lights as it gets closer to 0 
        #endregion

        #region control fill
        //space is for testing right now  claud will want to replace thias with the hum fequcey 
        // low range will fill orb 3 , mid fills orb 2 , and high fills orb 1 
        // as of now im filling based on space being heled were as you want fill to be set based of hum fequcy 
        if (Input.GetKey(KeyCode.Space)) { fill += 0.5f * Time.deltaTime; }
        else { fill -= 0.5f * Time.deltaTime; }
        fill = Mathf.Clamp01(fill);
        #endregion
        if (active_orb != null)
        { active_orb.SetFill(fill); }


        if (fill > 0)
        {
            time -= 1 * Time.deltaTime;
            //set light progress based on time remaining (closer to 0 = more lights
            if (active_orb.gameObject.name == Orb_1.name) { UpdateLights(lightSet_1); }
            if (active_orb.gameObject.name == Orb_2.name) { UpdateLights(lightSet_2); }
            if (active_orb.gameObject.name == Orb_3.name) { UpdateLights(lightSet_3); }
        }

        //if (time <= 0) { Set_active_orb(UnityEngine.Random.Range(0, 3)); }

    }
    /// <summary>
    /// set the current orb 
    /// </summary>
    /// <param name="x"></param>
    private void Set_active_orb(int x)
    {
        fill = 0;
        if (active_orb != null) {  active_orb.SetFill(fill); }
        ResetLights();
        time = Targettime;
        switch (x) 
        {
            case 1: active_orb = Orb_1.GetComponent<OrbFillUI>(); break;
            case 2: active_orb = Orb_2.GetComponent<OrbFillUI>(); break;
            case 3: active_orb = Orb_3.GetComponent<OrbFillUI>(); break;
        }
    }
    /// <summary>
    /// reset all lights when swiching
    /// </summary>
    private void ResetLights ()
    {
        foreach (CanvasGroup lightSet in lightSet_1)
        { lightSet.alpha = 0; }
        foreach (CanvasGroup lightSet in lightSet_2)
        { lightSet.alpha = 0; }
        foreach (CanvasGroup lightSet in lightSet_3)
        { lightSet.alpha = 0; }
    }
    private void UpdateLights(List<CanvasGroup> lightSet)
    {
        // 0 when timer starts, 1 when timer reaches zero
        float progress = 1f - (time / Targettime);
        progress = Mathf.Clamp01(progress);

        // Make sure every light is visible by the time 1 second remains
        if (time <= 1f)
        {
            progress = 1f;
        }

        // Figure out how many lights should currently be on
        int lightsToTurnOn = Mathf.FloorToInt(progress * lightSet.Count);

        // Set each light
        for (int i = 0; i < lightSet.Count; i++)
        {
            if (i < lightsToTurnOn)
            {
                lightSet[i].alpha = 1f;
            }
            else
            {
                lightSet[i].alpha = 0f;
            }
        }
    }
}
