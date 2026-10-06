using UnityEngine;
using UnityEngine.UI;

public class MixerUIHelper : MonoBehaviour
{
    [SerializeField] private Image firstImage;
    [SerializeField] private Image secondImage;
    [SerializeField] private Image thirdImage;

    [SerializeField] private Sprite woodSprite;
    [SerializeField] private Sprite brickSprite;
    [SerializeField] private Sprite glassSprite;
    [SerializeField] private Sprite cementSprite;


    private void OnEnable()
    {
        ClearImages();
    }

    public void DisplayItem(int index, CarriableType type)
    {
        Sprite selectedSprite = null;

        switch (type)
        {
            case CarriableType.Wood:
                selectedSprite = woodSprite;
                break;
            case CarriableType.Brick:
                selectedSprite = brickSprite;
                break;
            case CarriableType.Glass:
                selectedSprite = glassSprite;
                break;
            case CarriableType.Cement:
                selectedSprite = cementSprite;
                break;
        }

        if (index == 1)
        {
            firstImage.sprite = selectedSprite;
        }
        else if (index == 2)
        {
            secondImage.sprite = selectedSprite;
        }
        else
        {
            thirdImage.sprite = selectedSprite;
        }
    }

    #region UTILITIES

    public void ClearImages()
    {
        firstImage.sprite = null;
        secondImage.sprite = null;
        thirdImage.sprite = null;
    }

    #endregion
}
