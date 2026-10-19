using UnityEngine;
using UnityEngine.UI;

public class Inventory_Item : MonoBehaviour
{
    private Image image;

    public Color dullColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color brightColor = Color.white;

    private void Awake()
    {
        image = GetComponent<Image>();
        image.color = dullColor;
    }

    public void Select()
    {
        image.color = brightColor;
    }

    public void Deselect()
    {
        image.color = dullColor;
    }
}