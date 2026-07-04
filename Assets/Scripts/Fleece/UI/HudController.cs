using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class HudController : MonoBehaviour
{
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text smacksLabel;
    [SerializeField] private TMP_Text stateLabel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private Button rotateLeftButton;
    [SerializeField] private Button rotateRightButton;
    [SerializeField] private Button smackButton;
    [SerializeField] private Button skipChoiceButton;

    public Button RotateLeftButton => rotateLeftButton;
    public Button RotateRightButton => rotateRightButton;
    public Button SmackButton => smackButton;
    public Button SkipChoiceButton => skipChoiceButton;

    public void Refresh(int totalScore, int targetScore, int remainingSmacks, GameState state)
    {
        if (scoreLabel != null)
        {
            scoreLabel.SetText("{0} / >{1}", totalScore, targetScore);
        }

        if (smacksLabel != null)
        {
            smacksLabel.SetText("拍击 {0}", remainingSmacks);
        }

        if (stateLabel != null)
        {
            stateLabel.text = state.ToString();
        }

        var canRotate = state == GameState.RotationPreview;
        SetInteractable(rotateLeftButton, canRotate);
        SetInteractable(rotateRightButton, canRotate);
        SetInteractable(smackButton, canRotate);
        SetInteractable(skipChoiceButton, state == GameState.PieceChoice);
    }

    public void ShowMessage(string message)
    {
        if (messageLabel != null)
        {
            messageLabel.text = message;
        }

        if (!string.IsNullOrEmpty(message))
        {
            Debug.Log(message);
        }
    }

    private static void SetInteractable(Selectable selectable, bool interactable)
    {
        if (selectable != null)
        {
            selectable.interactable = interactable;
        }
    }
}

