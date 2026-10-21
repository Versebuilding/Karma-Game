using UnityEngine;

public class Inventory_Item_Manager : MonoBehaviour
{
    public Inventory_Item currentSelected;

    public void SelectItem(Inventory_Item newItem)
    {
        if (currentSelected != null && currentSelected != newItem)
        {
            currentSelected.Deselect();
        }

        currentSelected = newItem;
        currentSelected.Select();
    }
}