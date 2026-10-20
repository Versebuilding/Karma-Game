
using UnityEngine;

public class Backpack_Label_Manager : MonoBehaviour
{
    public Backpack_Inventory startingTab;
    public Backpack_Inventory[] tabs;
    public GameObject[] pages;

    private void Start()
    {
        SelectTab(startingTab);
    }

    public void SelectTab(Backpack_Inventory selectedTab)
    {
        for (int i = 0; i < tabs.Length; i++)
        {
            bool isSelected = tabs[i] == selectedTab;

            if (isSelected)
                tabs[i].Select();
            else
                tabs[i].Deselect();

            if (i < pages.Length)
                pages[i].SetActive(isSelected);
        }
    }
}
